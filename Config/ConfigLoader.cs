namespace RelayClient.Config;

using System.Text.Json;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<TunnelConfigEntry> Load(string path = "config.json")
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Config file not found: '{path}'. Copy config.json.example to config.json and fill in your values.");

        var json = File.ReadAllText(path);
        var entries = JsonSerializer.Deserialize<List<TunnelConfigEntry>>(json, Options)
            ?? throw new InvalidOperationException("config.json must contain a JSON array.");

        if (entries.Count == 0)
            throw new InvalidOperationException("config.json must contain at least one tunnel entry.");

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (string.IsNullOrWhiteSpace(e.ConnectionString))
                throw new InvalidOperationException($"Entry [{i}]: 'connectionString' is required.");
            if (string.IsNullOrWhiteSpace(e.TargetUrl))
                throw new InvalidOperationException($"Entry [{i}]: 'targetUrl' is required.");
        }

        return entries;
    }
}
