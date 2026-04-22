namespace RelayClient.Relay;

using Microsoft.Azure.Relay;
using RelayClient.UI;

public enum TunnelStatus { Connecting, Online, Offline, Error }

public sealed class RelayTunnel : IAsyncDisposable
{
    private readonly TunnelConfig _config;
    private readonly HybridConnectionListener _listener;
    private readonly HttpProxyHandler _proxyHandler;

    public TunnelStatus Status { get; private set; } = TunnelStatus.Connecting;
    public string? ErrorMessage { get; private set; }

    public event EventHandler<RequestLogEntry>? RequestLogged;
    public event EventHandler<TunnelStatus>? StatusChanged;

    public TunnelConfig Config => _config;

    public RelayTunnel(TunnelConfig config)
    {
        _config = config;
        _listener = new HybridConnectionListener(config.ConnectionString);
        _proxyHandler = new HttpProxyHandler(config.TargetUrl, config.EntityPath, config.DisplayName);
        _proxyHandler.RequestLogged += (_, entry) => RequestLogged?.Invoke(this, entry);

        _listener.Connecting += (_, _) => SetStatus(TunnelStatus.Connecting);
        _listener.Online += (_, _) => SetStatus(TunnelStatus.Online);
        _listener.Offline += (_, _) => SetStatus(TunnelStatus.Offline);

        _listener.RequestHandler = _proxyHandler.Handle;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _listener.OpenAsync(ct);
            SetStatus(TunnelStatus.Online);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SetStatus(TunnelStatus.Error);
        }
    }

    public async Task StopAsync()
    {
        try { await _listener.CloseAsync(); }
        catch { /* best effort */ }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void SetStatus(TunnelStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }
}
