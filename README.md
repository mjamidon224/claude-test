# NetIPConfig — IPv4 Settings Manager for Windows 11

A small Windows Forms desktop app for changing **Internet Protocol Version 4 (TCP/IPv4)**
settings: pick a network adapter, switch it between DHCP and a manual address, and set the
IP address, subnet mask, default gateway and DNS servers.

It does the same job as the *Internet Protocol Version 4 (TCP/IPv4) Properties* dialog in
`ncpa.cpl`, but in one window with no navigating through adapter properties, plus input
validation and a log of exactly what was run.

## What it does

- **Adapter picker** listing every real adapter (Ethernet, Wi-Fi, USB NICs, …) with its
  connection name, hardware description and connection state. Connected adapters sort first.
- **Current configuration** panel showing status, whether the adapter is on DHCP or manual,
  MAC address, IPv4 address, subnet mask, default gateway and DNS servers. Values are read
  from the live stack, falling back to the configured values in the registry so a
  disconnected or disabled adapter still shows what it is set to.
- **IPv4 address**: *Obtain an IP address automatically (DHCP)* or *Use the following IP
  address* with IP / subnet mask / default gateway. A blank gateway box clears the gateway.
- **DNS servers**: automatic, or a preferred and alternate server. Clearing the preferred
  box with manual selected removes the configured servers.
- **Validation before anything is applied**: strict dotted-quad parsing, rejection of
  non-contiguous masks, of loopback / multicast / reserved / network / broadcast addresses,
  and a warning (not a refusal) when the gateway sits outside the chosen subnet.
- **Network scan**: *Scan network...* sweeps the selected adapter's own subnet and lists
  every device that answers, with its IP address, MAC address, reverse-DNS name, ping time
  and a note marking this computer, the default gateway, the DHCP server and DNS servers.
  Results export to CSV.
- **Saved profiles**: name a set of settings ("office static", "lab DHCP", "customer site")
  and recall it from the dropdown. Recalling a profile only fills the fields — nothing
  reaches the adapter until you press Apply. Profiles are not tied to an adapter, so the
  same profile works on a dock, a USB NIC or Wi-Fi.
- **Dark mode**: a toggle in the top right, including the title bar, remembered between
  runs.
- **Convenience**: type `24` in the subnet mask box and it becomes `255.255.255.0`; typing
  an address fills an empty mask box with the class default; *Load current settings* copies
  the adapter's live values back into the fields.
- **Confirmation and log**: a summary dialog before applying, then every `netsh` command and
  its output in the activity log.

Profiles and the dark-mode choice live in
`%APPDATA%\NetIPConfig\settings.json`, written when you change them. A missing or damaged
file simply starts you with defaults, and it is plain JSON if you want to hand-edit or copy
it to another machine.

## Requirements

- Windows 11 (or Windows 10) — the app targets `net8.0-windows`
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run a
  framework-dependent build, or none at all for a self-contained publish
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build
- Administrator rights to apply changes — the app manifest requests elevation, so Windows
  shows a UAC prompt at launch

## Build and run

```powershell
git clone https://github.com/mjamidon224/claude-test.git
cd claude-test
dotnet build -c Release
.\src\NetIPConfig\bin\Release\net8.0-windows\NetIPConfig.exe
```

Or open `NetIPConfig.sln` in Visual Studio 2022 (17.8+) with the **.NET desktop
development** workload and press F5.

### Single-file build with no runtime install

```powershell
.\publish.ps1
```

This writes a self-contained `publish\NetIPConfig.exe` (x64) that runs on a machine with no
.NET installed — handy for a USB stick or a tech's toolkit. Use `-Runtime win-arm64` for Arm
devices, or `-SelfContained:$false` for a much smaller exe that needs the .NET 8 Desktop
Runtime.

> Launch the built `.exe` directly rather than using `dotnet run`. Under `dotnet run` the
> app manifest is not applied, so the process is not elevated; the status bar says so and
> offers a **Restart as administrator** link.

## How the scan works

Each address in the subnet is probed two ways at once, 64 addresses at a time:

- **ICMP echo** (`System.Net.NetworkInformation.Ping`, 600 ms timeout) for a reachability
  check and a round-trip time.
- **ARP request** (`SendARP` in `iphlpapi.dll`) for the MAC address. This is the part that
  matters for coverage: any device on the same layer-2 segment has to answer ARP to
  communicate at all, while plenty of hosts — Windows with its default firewall, printers,
  cameras — drop pings silently. Rows showing `ARP` in the Ping column are real devices
  found this way.

Only devices that are present are listed, which takes one extra step: `SendARP` answers from
the Windows ARP cache when an entry exists, so a machine switched off minutes ago would
otherwise appear with a valid MAC. An address that answers ARP but not ping is therefore
re-checked with `arp -d` run against it first, forcing a real ARP exchange on the wire — if
nothing answers that, the address is not listed. (Without elevation the delete fails and the
cached answer stands, which is no worse than not checking.)

Device names come from a reverse DNS lookup (1.5 s timeout), so they appear only for hosts
with a PTR record or an entry in your DNS server — expect blanks on a home network. There is
no NetBIOS or mDNS query, and no MAC-to-vendor lookup, since that needs a bundled OUI
database.

The scan only covers the adapter's own subnet, because ARP does not cross a router. Runs are
capped at 1024 addresses: a mask wider than /22 prompts first and then scans the first 1024.
A scan of a /24 typically takes 10-20 seconds and can be stopped at any point.

## How it applies changes

Changes go through `netsh.exe` (resolved from `%SystemRoot%\System32`, not the `PATH`),
which is the same mechanism the built-in UI uses and reports a readable message when
Windows rejects something:

```
netsh interface ipv4 set address name="Ethernet" source=dhcp
netsh interface ipv4 set address name="Ethernet" source=static address=192.168.1.50 mask=255.255.255.0 gateway=192.168.1.1
netsh interface ipv4 set dnsservers name="Ethernet" source=dhcp
netsh interface ipv4 set dnsservers name="Ethernet" source=static address=9.9.9.9 register=primary validate=no
netsh interface ipv4 add dnsservers name="Ethernet" address=149.112.112.112 index=2 validate=no
```

Commands run in order and stop at the first failure, so a rejected address never leaves the
DNS half-applied. Reading the current state uses `System.Net.NetworkInformation` plus
`HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces`.

Applying a change to the adapter carrying your current connection drops it briefly — the
confirmation dialog says so. Over Remote Desktop, a bad manual address will lock you out of
the machine.

## Download a prebuilt executable

The **Build Windows executable** GitHub Actions workflow builds on every push. To grab an
executable without building locally, open the
[Actions tab](https://github.com/mjamidon224/claude-test/actions), pick the newest run and
download one of its artifacts:

| Artifact | Size | Needs .NET installed? |
| --- | --- | --- |
| `NetIPConfig-win-x64` | ~60 MB | No — everything is bundled |
| `NetIPConfig-win-arm64` | ~60 MB | No — for Arm devices |
| `NetIPConfig-win-x64-requires-dotnet8` | a few MB | Yes — .NET 8 Desktop Runtime |

Artifacts arrive as a `.zip`; unzip it and run `NetIPConfig.exe`.

Pushing a tag that starts with `v` (for example `git tag v1.0.0 && git push origin v1.0.0`)
also attaches both executables to a GitHub release.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/NetIPConfig/Program.cs` | Entry point, high-DPI setup, unhandled-exception reporting |
| `src/NetIPConfig/MainForm.cs` | Form behaviour: loading adapters, validation, applying |
| `src/NetIPConfig/MainForm.Designer.cs` | Layout |
| `src/NetIPConfig/NetworkQuery.cs` | Reads adapters and their current IPv4 configuration |
| `src/NetIPConfig/NetworkConfigurator.cs` | Builds and runs the `netsh` commands |
| `src/NetIPConfig/IPv4Text.cs` | Dotted-quad parsing, mask/subnet checks |
| `src/NetIPConfig/AdapterInfo.cs` | Adapter snapshot model |
| `src/NetIPConfig/Theme.cs` | Light/dark recolouring, including the title bar |
| `src/NetIPConfig/AppSettings.cs` | Profile and settings models |
| `src/NetIPConfig/SettingsStore.cs` | Loads and saves `settings.json` |
| `src/NetIPConfig/TextPromptDialog.cs` | Name prompt used when saving a profile |
| `src/NetIPConfig/NetworkScanner.cs` | Subnet enumeration, ping + ARP sweep |
| `src/NetIPConfig/ScanForm.cs` | Scan results window and CSV export |
| `src/NetIPConfig/app.manifest` | Requests administrator elevation |

No NuGet packages are referenced; everything comes from the Windows Desktop framework.

## Not yet covered

IPv6, multiple IP addresses or gateways per adapter, gateway metrics, WINS, DHCP
release/renew, scanning a subnet other than the adapter's own, and MAC vendor names. The `netsh` commands for these fit the same
`NetworkConfigurator.BuildCommands` pattern if you want to add them.
