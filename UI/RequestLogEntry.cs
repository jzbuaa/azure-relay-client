namespace RelayClient.UI;

public sealed record RequestLogEntry(
    DateTimeOffset Timestamp,
    string TunnelName,
    string Method,
    string Path,
    int StatusCode,
    long LatencyMs,
    bool IsError
);
