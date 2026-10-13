namespace DiskHog;

/// <summary>One scanned folder with totals that include everything beneath it.</summary>
internal sealed class FolderNode
{
    public FolderNode(string name, string fullPath, FolderNode? parent)
    {
        Name = name;
        FullPath = fullPath;
        Parent = parent;
    }

    public string Name { get; }
    public string FullPath { get; }
    public FolderNode? Parent { get; }

    /// <summary>Bytes in this folder and all of its subfolders.</summary>
    public long Size { get; set; }

    /// <summary>Bytes in files sitting directly in this folder.</summary>
    public long OwnFilesSize { get; set; }

    public int OwnFileCount { get; set; }

    /// <summary>Files in this folder and all of its subfolders.</summary>
    public long FileCount { get; set; }

    /// <summary>Folders beneath this one, at any depth.</summary>
    public long FolderCount { get; set; }

    /// <summary>True when Windows refused to list the folder (usually access denied).</summary>
    public bool Unreadable { get; set; }

    public List<FolderNode> Children { get; set; } = [];
}

/// <summary>A file large enough to be worth listing on its own.</summary>
internal sealed record BigFile(string Name, string Folder, long Size)
{
    public string FullPath => System.IO.Path.Combine(Folder, Name);
}
