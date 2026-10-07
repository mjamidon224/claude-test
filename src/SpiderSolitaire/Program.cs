using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SpiderSolitaire;

internal static class Program
{
    /// <summary>Per-session name, so each signed-in user can still run their own copy.</summary>
    private const string InstanceName = @"Local\SpiderSolitaire-4c1d8f2e";

    private const int ShowRestore = 9;

    [STAThread]
    private static void Main()
    {
        // One copy at a time, as with the Windows game. Two copies would each overwrite the
        // other's statistics and could both resume the same saved game, so a second launch
        // brings the running one forward instead.
        using Mutex instance = new(initiallyOwned: true, InstanceName, out bool firstInstance);
        if (!firstInstance)
        {
            ActivateRunningCopy();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Anything unhandled is worth showing rather than losing to a silent exit.
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    private static void ActivateRunningCopy()
    {
        using Process current = Process.GetCurrentProcess();
        foreach (Process other in Process.GetProcessesByName(current.ProcessName))
        {
            using (other)
            {
                IntPtr window = other.Id == current.Id ? IntPtr.Zero : other.MainWindowHandle;
                if (window == IntPtr.Zero)
                {
                    continue;
                }

                if (IsIconic(window))
                {
                    ShowWindow(window, ShowRestore);
                }

                SetForegroundWindow(window);
                return;
            }
        }
    }

    private static void ReportFatal(Exception? exception)
    {
        MessageBox.Show(
            exception?.ToString() ?? "An unknown error occurred.",
            "Spider Solitaire — unexpected error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
}
