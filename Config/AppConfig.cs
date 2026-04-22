namespace RelayClient.Config;

public sealed class TunnelConfigEntry
{
    public string ConnectionString { get; init; } = "";
    public string TargetUrl { get; init; } = "";
}
