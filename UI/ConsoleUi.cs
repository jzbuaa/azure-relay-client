namespace RelayClient.UI;

using RelayClient.Relay;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class ConsoleUi
{
    public const int DefaultMaxLogRows = 10;

    private readonly IReadOnlyList<RelayTunnel> _tunnels;
    private readonly int _maxLogRows;
    private readonly object _logLock = new();
    private readonly Queue<RequestLogEntry> _log = new();

    public ConsoleUi(IReadOnlyList<RelayTunnel> tunnels, int maxLogRows = DefaultMaxLogRows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLogRows);
        _tunnels = tunnels;
        _maxLogRows = maxLogRows;
    }

    public void EnqueueLogEntry(RequestLogEntry entry)
    {
        lock (_logLock)
        {
            if (_log.Count == _maxLogRows)
                _log.Dequeue();
            _log.Enqueue(entry);
        }
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var layout = BuildLayout();

        await AnsiConsole.Live(layout)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                while (!ct.IsCancellationRequested)
                {
                    layout["tunnels"].Update(BuildTunnelsPanel());
                    layout["log"].Update(BuildLogPanel());
                    ctx.Refresh();

                    try { await Task.Delay(100, ct); }
                    catch (OperationCanceledException) { break; }
                }
            });
    }

    private Layout BuildLayout()
    {
        int tunnelRows = _tunnels.Count + 4; // header + borders + 1 per tunnel
        return new Layout("root")
            .SplitRows(
                new Layout("tunnels").Size(tunnelRows),
                new Layout("log")
            );
    }

    private IRenderable BuildTunnelsPanel()
    {
        var table = new Table()
            .NoBorder()
            .HideHeaders()
            .AddColumn(new TableColumn("").NoWrap())
            .AddColumn(new TableColumn("").NoWrap())
            .AddColumn(new TableColumn("").NoWrap())
            .AddColumn(new TableColumn(""));

        foreach (var tunnel in _tunnels)
        {
            var (dot, color) = tunnel.Status switch
            {
                TunnelStatus.Online => ("●", "green"),
                TunnelStatus.Connecting => ("●", "yellow"),
                TunnelStatus.Error => ("●", "red"),
                _ => ("●", "grey")
            };

            var statusMarkup = tunnel.Status == TunnelStatus.Error
                ? $"[{color}]{dot} {tunnel.Status}[/] [grey]{Markup.Escape(tunnel.ErrorMessage ?? "")}[/]"
                : $"[{color}]{dot} {tunnel.Status}[/]";

            table.AddRow(
                new Markup(statusMarkup),
                new Markup($"[bold]{Markup.Escape(tunnel.Config.PublicUrl)}[/]"),
                new Markup("[grey]→[/]"),
                new Markup($"[dim]{Markup.Escape(tunnel.Config.TargetUrl)}[/]")
            );
        }

        return new Panel(table)
            .Header("[bold] Tunnels [/]")
            .Expand()
            .BorderColor(Color.Blue);
    }

    private IRenderable BuildLogPanel()
    {
        RequestLogEntry[] entries;
        lock (_logLock)
            entries = _log.Reverse().ToArray();

        var table = new Table()
            .Expand()
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[grey]Time[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Tunnel[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Method[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Path[/]"))
            .AddColumn(new TableColumn("[grey]Status[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Latency[/]").NoWrap());

        foreach (var entry in entries)
        {
            var statusColor = entry.StatusCode switch
            {
                >= 500 => "red",
                >= 400 => "red",
                >= 300 => "yellow",
                >= 200 => "green",
                _ => "white"
            };

            var methodColor = entry.Method switch
            {
                "GET" => "cyan",
                "POST" => "green",
                "PUT" => "yellow",
                "DELETE" => "red",
                "PATCH" => "yellow",
                _ => "white"
            };

            table.AddRow(
                new Markup($"[dim]{entry.Timestamp:HH:mm:ss}[/]"),
                new Markup($"[dim]{Markup.Escape(entry.TunnelName)}[/]"),
                new Markup($"[{methodColor}]{Markup.Escape(entry.Method)}[/]"),
                new Markup(Markup.Escape(entry.Path)),
                new Markup($"[{statusColor}]{entry.StatusCode}[/]"),
                new Markup($"[dim]{entry.LatencyMs}ms[/]")
            );
        }

        return new Panel(table)
            .Header("[bold] Request Log [/]")
            .Expand()
            .BorderColor(Color.Blue);
    }
}
