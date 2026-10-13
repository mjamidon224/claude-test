using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace DiskHog;

/// <summary>
/// Walks a folder tree and totals file sizes. Uses FindFirstFileEx with the basic info level
/// and large fetch, which returns sizes straight from the directory listing — no per-file
/// stat call — and fans out across subfolders on the thread pool near the top of the tree.
/// </summary>
internal sealed class DiskScanner
{
    /// <summary>Files at least this big also land in the "biggest files" list.</summary>
    public const long BigFileThreshold = 50L * 1024 * 1024;

    /// <summary>Folders shallower than this scan their subfolders in parallel.</summary>
    private const int ParallelDepth = 6;

    private const uint AttributeDirectory = 0x10;
    private const uint AttributeReparsePoint = 0x400;
    private const uint AttributeOffline = 0x1000;
    private const uint AttributeRecallOnDataAccess = 0x400000;

    private const uint ReparseTagMountPoint = 0xA0000003;
    private const uint ReparseTagSymlink = 0xA000000C;

    private const int FindExInfoBasic = 1;
    private const int FindExSearchNameMatch = 0;
    private const int FindFirstExLargeFetch = 2;

    private static readonly IntPtr InvalidHandle = new(-1);

    private long _files;
    private long _folders;
    private long _bytes;
    private int _unreadable;
    private string _currentFolder = "";

    public long FilesScanned => Interlocked.Read(ref _files);
    public long FoldersScanned => Interlocked.Read(ref _folders);
    public long BytesScanned => Interlocked.Read(ref _bytes);
    public int UnreadableFolders => Volatile.Read(ref _unreadable);
    public string CurrentFolder => Volatile.Read(ref _currentFolder);

    public ConcurrentBag<BigFile> BigFiles { get; } = [];

    public FolderNode Scan(string root, CancellationToken cancellation)
    {
        // Directory listing blocks on I/O, so give the pool enough threads up front instead
        // of waiting for it to inject them one at a time.
        ThreadPool.GetMinThreads(out int workers, out int io);
        ThreadPool.SetMinThreads(Math.Max(workers, Environment.ProcessorCount * 4), io);

        var node = new FolderNode(root, root, parent: null);
        ScanFolder(node, depth: 0, cancellation);
        return node;
    }

    private void ScanFolder(FolderNode node, int depth, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        Volatile.Write(ref _currentFolder, node.FullPath);

        List<FolderNode>? subfolders = null;
        long ownSize = 0;
        int ownFiles = 0;

        IntPtr handle = FindFirstFileExW(
            SearchPattern(node.FullPath), FindExInfoBasic, out Win32FindData data,
            FindExSearchNameMatch, IntPtr.Zero, FindFirstExLargeFetch);

        if (handle == InvalidHandle)
        {
            node.Unreadable = true;
            Interlocked.Increment(ref _unreadable);
        }
        else
        {
            try
            {
                do
                {
                    string name = data.FileName;

                    if ((data.FileAttributes & AttributeDirectory) != 0)
                    {
                        if (name is "." or ".." || IsLinkToElsewhere(data))
                        {
                            continue;
                        }

                        (subfolders ??= []).Add(new FolderNode(name, Combine(node.FullPath, name), node));
                    }
                    else if ((data.FileAttributes & (AttributeRecallOnDataAccess | AttributeOffline)) == 0)
                    {
                        // Cloud-only placeholders (OneDrive "online-only") report their full
                        // size but take no local space, so they are left out.
                        long size = ((long)data.FileSizeHigh << 32) | data.FileSizeLow;
                        ownSize += size;
                        ownFiles++;

                        if (size >= BigFileThreshold)
                        {
                            BigFiles.Add(new BigFile(name, node.FullPath, size));
                        }
                    }
                }
                while (FindNextFileW(handle, out data));
            }
            finally
            {
                FindClose(handle);
            }
        }

        Interlocked.Add(ref _files, ownFiles);
        Interlocked.Add(ref _bytes, ownSize);
        Interlocked.Increment(ref _folders);

        long totalSize = ownSize;
        long totalFiles = ownFiles;
        long totalFolders = 0;

        if (subfolders is not null)
        {
            if (depth < ParallelDepth && subfolders.Count > 1)
            {
                Parallel.ForEach(subfolders, child => ScanFolder(child, depth + 1, cancellation));
            }
            else
            {
                foreach (FolderNode child in subfolders)
                {
                    ScanFolder(child, depth + 1, cancellation);
                }
            }

            foreach (FolderNode child in subfolders)
            {
                totalSize += child.Size;
                totalFiles += child.FileCount;
                totalFolders += 1 + child.FolderCount;
            }

            node.Children = subfolders;
        }

        node.OwnFilesSize = ownSize;
        node.OwnFileCount = ownFiles;
        node.Size = totalSize;
        node.FileCount = totalFiles;
        node.FolderCount = totalFolders;
    }

    /// <summary>
    /// Junctions and symlinks point at data counted somewhere else (or loop back on
    /// themselves), so they are skipped. Other reparse points — OneDrive folders, for one —
    /// hold real content and are walked.
    /// </summary>
    private static bool IsLinkToElsewhere(in Win32FindData data) =>
        (data.FileAttributes & AttributeReparsePoint) != 0
        && data.Reserved0 is ReparseTagMountPoint or ReparseTagSymlink;

    private static string Combine(string folder, string name) =>
        folder.EndsWith('\\') ? folder + name : folder + "\\" + name;

    /// <summary>Builds a \\?\ search pattern so paths past 260 characters still work.</summary>
    private static string SearchPattern(string folder)
    {
        string trimmed = folder.TrimEnd('\\');
        if (trimmed.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return trimmed + @"\*";
        }

        return trimmed.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + trimmed[2..] + @"\*"
            : @"\\?\" + trimmed + @"\*";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Win32FindData
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string AlternateFileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr FindFirstFileExW(
        string fileName, int infoLevel, out Win32FindData findData,
        int searchOp, IntPtr searchFilter, int additionalFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextFileW(IntPtr findHandle, out Win32FindData findData);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr findHandle);
}
