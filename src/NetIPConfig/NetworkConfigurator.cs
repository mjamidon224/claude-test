using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NetIPConfig;

/// <summary>
/// Applies IPv4 changes by driving netsh.exe. netsh is used rather than WMI because it
/// is the same code path the built-in UI uses, needs no extra package reference, and
/// reports a readable message when something is rejected.
/// </summary>
internal static class NetworkConfigurator
{
    private static readonly Lazy<Encoding> ConsoleEncoding = new(() =>
    {
        // netsh writes in the console's OEM code page. Only a handful of encodings ship
        // with the framework, so fall back to UTF-8 when that code page is unavailable
        // rather than taking a dependency on System.Text.Encoding.CodePages.
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    });

    /// <summary>
    /// A netsh command plus, optionally, a way to tell whether a refusal simply means the
    /// adapter was already configured that way.
    /// </summary>
    private sealed record PlannedCommand(string[] Arguments, Func<bool>? AlreadySatisfied = null);

    /// <summary>
    /// Runs the commands needed for <paramref name="request"/>, stopping at the first real
    /// failure. The returned list is the transcript to show the user.
    /// </summary>
    public static List<CommandResult> Apply(IPv4ChangeRequest request)
    {
        List<CommandResult> results = new();

        foreach (PlannedCommand planned in BuildCommands(request))
        {
            CommandResult result = RunNetsh(planned.Arguments);

            // netsh exits non-zero for "DHCP is already enabled on this interface" and
            // "the object already exists". Re-reading the adapter tells us whether the
            // requested state holds without having to match a localised message.
            if (!result.Success && planned.AlreadySatisfied is not null && planned.AlreadySatisfied())
            {
                result = result with { Tolerated = true };
            }

            results.Add(result);
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }

    private static IEnumerable<PlannedCommand> BuildCommands(IPv4ChangeRequest request)
    {
        string adapter = request.AdapterName;
        string name = $"name={adapter}";

        if (request.UseDhcp)
        {
            yield return new PlannedCommand(
                new[] { "interface", "ipv4", "set", "address", name, "source=dhcp" },
                () => Current(adapter)?.IsDhcpEnabled == true);
        }
        else
        {
            // A blank gateway box means "no default gateway on this adapter".
            string gateway = request.Gateway.Length == 0 ? "none" : request.Gateway;
            yield return new PlannedCommand(
                new[]
                {
                    "interface", "ipv4", "set", "address", name, "source=static",
                    $"address={request.Address}", $"mask={request.SubnetMask}", $"gateway={gateway}",
                },
                () => Current(adapter) is { IsDhcpEnabled: false } current
                      && Matches(current.IPv4Address, request.Address)
                      && Matches(current.SubnetMask, request.SubnetMask));
        }

        if (request.DnsAutomatic)
        {
            yield return new PlannedCommand(
                new[] { "interface", "ipv4", "set", "dnsservers", name, "source=dhcp" },
                () => Current(adapter)?.IsDnsStatic == false);
        }
        else if (request.PreferredDns.Length == 0)
        {
            yield return new PlannedCommand(
                new[] { "interface", "ipv4", "set", "dnsservers", name, "source=static", "address=none" },
                () => Current(adapter) is { } current && current.DnsServers.Count == 0);
        }
        else
        {
            // validate=no keeps netsh from blocking on a server it cannot reach yet.
            yield return new PlannedCommand(
                new[]
                {
                    "interface", "ipv4", "set", "dnsservers", name, "source=static",
                    $"address={request.PreferredDns}", "register=primary", "validate=no",
                },
                () => Current(adapter) is { IsDnsStatic: true } current
                      && current.DnsServers.Count > 0
                      && Matches(current.DnsServers[0], request.PreferredDns));

            if (request.AlternateDns.Length > 0)
            {
                yield return new PlannedCommand(
                    new[]
                    {
                        "interface", "ipv4", "add", "dnsservers", name,
                        $"address={request.AlternateDns}", "index=2", "validate=no",
                    },
                    () => Current(adapter) is { } current
                          && current.DnsServers.Any(s => Matches(s, request.AlternateDns)));
            }
        }
    }

    private static AdapterInfo? Current(string adapterName)
    {
        try
        {
            return NetworkQuery.GetAdapters()
                .FirstOrDefault(a => string.Equals(a.Name, adapterName, StringComparison.Ordinal));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool Matches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.Ordinal);

    private static CommandResult RunNetsh(string[] arguments)
    {
        // Full path so PATH cannot be used to substitute a different netsh.
        string netsh = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        string commandLine = "netsh " + string.Join(" ", arguments.Select(Quote));

        ProcessStartInfo startInfo = new(netsh)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = ConsoleEncoding.Value,
            StandardErrorEncoding = ConsoleEncoding.Value,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return new CommandResult(commandLine, -1, "Could not start netsh.exe.");
            }

            // Start both reads before waiting so a chatty command cannot fill a pipe buffer.
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(60_000))
            {
                return new CommandResult(commandLine, -1, "netsh.exe did not finish within 60 seconds.");
            }

            string output = string.Join(
                Environment.NewLine,
                new[] { stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult() }
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0));

            return new CommandResult(commandLine, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return new CommandResult(commandLine, -1, ex.Message);
        }
    }

    /// <summary>Quotes an argument for display only; the real call uses ArgumentList.</summary>
    private static string Quote(string argument) =>
        argument.Contains(' ') ? $"\"{argument}\"" : argument;
}
