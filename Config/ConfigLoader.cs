namespace RelayClient.Config;

using System.Text.Json;

public static class ConfigLoader
{
    public static string DefaultPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("Cannot determine the user's home directory.");

            return Path.Combine(home, ".relay-client", "config.json");
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void EnsureExists(string path)
    {
        if (File.Exists(path))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var template = typeof(ConfigLoader).Assembly
            .GetManifestResourceStream("RelayClient.Config.Example")
            ?? throw new InvalidOperationException("The example configuration is missing from the installation.");
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        template.CopyTo(file);
    }

    public static IReadOnlyList<TunnelConfigEntry> Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Config file not found: '{path}'. Run 'relay config' to create it and fill in your values.");

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
