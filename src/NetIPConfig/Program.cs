namespace NetIPConfig;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Anything unhandled is worth showing rather than losing to a silent exit.
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    private static void ReportFatal(Exception? exception)
    {
        MessageBox.Show(
            exception?.ToString() ?? "An unknown error occurred.",
            "NetIPConfig — unexpected error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
