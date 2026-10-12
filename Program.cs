using RelayClient.Config;
using RelayClient.Relay;
using RelayClient.UI;
using Spectre.Console;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: relay [--max-log-rows N]");
    Console.WriteLine("       relay config");
    Console.WriteLine();
    Console.WriteLine("relay config opens ~/.relay-client/config.json in the default editor.");
    return 0;
}

if (args.Length == 1 && args[0] == "config")
{
    try
    {
        var path = ConfigLoader.DefaultPath;
        ConfigLoader.EnsureExists(path);
        ConfigEditor.Open(path);
        AnsiConsole.MarkupLine($"[green]Opened config:[/] {Markup.Escape(path)}");
        return 0;
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Config editor error:[/] {Markup.Escape(ex.Message)}");
        return 1;
    }
}

int maxLogRows = ConsoleUi.DefaultMaxLogRows;
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] != "--max-log-rows"
        || !int.TryParse(args[1], out maxLogRows) || maxLogRows <= 0)
    {
        AnsiConsole.MarkupLine("[red]Argument error:[/] Usage: relay [[--max-log-rows N]] or relay config (N must be a positive integer).");
        return 1;
    }
}

// Ctrl+C → graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Load config
IReadOnlyList<TunnelConfigEntry> configEntries;
try
{
    configEntries = ConfigLoader.Load();
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Config error:[/] {Markup.Escape(ex.Message)}");
    return 1;
}

// Build tunnel configs (parses connection strings, derives public URLs)
List<TunnelConfig> tunnelConfigs;
try
{
    tunnelConfigs = configEntries.Select(TunnelConfig.FromEntry).ToList();
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Invalid connection string:[/] {Markup.Escape(ex.Message)}");
    return 1;
}

// Create tunnels and UI
var tunnels = tunnelConfigs.Select(tc => new RelayTunnel(tc)).ToList();
var ui = new ConsoleUi(tunnels, maxLogRows);

foreach (var tunnel in tunnels)
    tunnel.RequestLogged += (_, entry) => ui.EnqueueLogEntry(entry);

// Start all tunnels concurrently (failures are reported via tunnel.Status, not exceptions)
var tunnelTasks = tunnels.Select(t => t.StartAsync(cts.Token)).ToArray();

// Run UI render loop
var uiTask = ui.RunAsync(cts.Token);

try
{
    await Task.WhenAll(tunnelTasks.Append(uiTask));
}
catch (OperationCanceledException) { /* normal Ctrl+C */ }

// Graceful shutdown
AnsiConsole.MarkupLine("[grey]Shutting down...[/]");
await Task.WhenAll(tunnels.Select(t => t.StopAsync()));
foreach (var tunnel in tunnels)
    await tunnel.DisposeAsync();

AnsiConsole.MarkupLine("[green]All tunnels closed.[/]");
return 0;
