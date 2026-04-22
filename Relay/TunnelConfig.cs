namespace RelayClient.Relay;

using Microsoft.Azure.Relay;

public sealed class TunnelConfig
{
    public string ConnectionString { get; }
    public string TargetUrl { get; }
    public string PublicUrl { get; }
    public string EntityPath { get; }
    public string DisplayName { get; }

    private TunnelConfig(string connectionString, string targetUrl, string publicUrl,
        string entityPath, string displayName)
    {
        ConnectionString = connectionString;
        TargetUrl = targetUrl;
        PublicUrl = publicUrl;
        EntityPath = entityPath;
        DisplayName = displayName;
    }

    public static TunnelConfig FromEntry(Config.TunnelConfigEntry entry)
    {
        var builder = new RelayConnectionStringBuilder(entry.ConnectionString);

        var entityPath = builder.EntityPath
            ?? throw new InvalidOperationException(
                "Connection string must include EntityPath (e.g. ;EntityPath=my-connection).");

        var publicUrl = $"https://{builder.Endpoint.Host}/{entityPath}";
        var displayName = entityPath;

        return new TunnelConfig(entry.ConnectionString, entry.TargetUrl, publicUrl, entityPath, displayName);
    }
}
