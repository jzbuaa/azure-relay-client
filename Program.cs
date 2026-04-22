using RelayClient.Config;
using RelayClient.Relay;
using RelayClient.UI;
using Spectre.Console;

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
    configEntries = ConfigLoader.Load("config.json");
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
var ui = new ConsoleUi(tunnels);

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
