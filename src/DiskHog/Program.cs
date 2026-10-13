namespace DiskHog;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Anything unhandled is worth showing rather than losing to a silent exit.
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);

        // An optional path argument scans that folder or drive straight away.
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }

    private static void ReportFatal(Exception? exception)
    {
        MessageBox.Show(
            exception?.ToString() ?? "An unknown error occurred.",
            "DiskHog — unexpected error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
