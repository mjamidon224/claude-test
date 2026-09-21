namespace NetIPConfig;

/// <summary>One netsh invocation and what it reported back.</summary>
/// <param name="Tolerated">
/// True when netsh refused the command but the adapter turned out to be in the requested
/// state anyway, which is how it reports "nothing to do".
/// </param>
internal sealed record CommandResult(string CommandLine, int ExitCode, string Output, bool Tolerated = false)
{
    public bool Success => ExitCode == 0 || Tolerated;
}
