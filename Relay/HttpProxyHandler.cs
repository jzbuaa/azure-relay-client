namespace RelayClient.Relay;

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.Relay;
using RelayClient.UI;

public sealed class HttpProxyHandler
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Host", "Keep-Alive", "Transfer-Encoding",
        "TE", "Trailers", "Upgrade", "Proxy-Authorization", "Proxy-Authenticate",
        // Let HttpClient manage Accept-Encoding; it pairs with AutomaticDecompression
        "Accept-Encoding",
    };

    private static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Length", "Content-Encoding", "Content-Language",
        "Content-Location", "Content-MD5", "Content-Range", "Content-Disposition",
        "Content-Transfer-Encoding", "Expires", "Last-Modified", "Allow"
    };

    private readonly HttpClient _httpClient;
    private readonly string _targetUrl;
    private readonly string _entityPath;
    private readonly string _displayName;

    public event EventHandler<RequestLogEntry>? RequestLogged;

    public HttpProxyHandler(string targetUrl, string entityPath, string displayName)
    {
        _targetUrl = targetUrl.TrimEnd('/');
        _entityPath = entityPath;
        _displayName = displayName;

#pragma warning disable CA5359 // intentional — target is always a local dev server
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            // Decompress automatically so we can set an accurate Content-Length on the
            // relay response. Without this the raw compressed bytes pass through but the
            // Content-Encoding header is stripped, giving clients garbage output.
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(100)
        };
#pragma warning restore CA5359
    }

    public void Handle(RelayedHttpListenerContext context)
        => Task.Run(() => HandleAsync(context)).GetAwaiter().GetResult();

    private async Task HandleAsync(RelayedHttpListenerContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.HttpMethod;
        var localPath = ExtractLocalPath(context.Request.Url, _entityPath);
        var targetUri = new Uri(_targetUrl + localPath);
        int statusCode = 0;
        bool isError = false;

        try
        {
            var requestMessage = new HttpRequestMessage(new HttpMethod(method), targetUri);
            requestMessage.Headers.ExpectContinue = false;

            foreach (string? name in context.Request.Headers)
            {
                if (name is null || HopByHopHeaders.Contains(name) || ContentHeaders.Contains(name))
                    continue;
                var values = context.Request.Headers.GetValues(name);
                if (values is not null)
                    requestMessage.Headers.TryAddWithoutValidation(name, values);
            }

            // Read the full request body first so HttpClient can set Content-Length
            // precisely (avoiding Transfer-Encoding: chunked which many servers reject)
            using var bodyStream = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(bodyStream).ConfigureAwait(false);
            var bodyBytes = bodyStream.ToArray();

            if (bodyBytes.Length > 0)
            {
                requestMessage.Content = new ByteArrayContent(bodyBytes);
                var ct = context.Request.Headers["Content-Type"];
                if (!string.IsNullOrEmpty(ct))
                    requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", ct);
            }

            var response = await _httpClient
                .SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead)
                .ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            statusCode = (int)response.StatusCode;

            context.Response.StatusCode = response.StatusCode;
            context.Response.StatusDescription = response.ReasonPhrase ?? statusCode.ToString();

            foreach (var header in response.Headers)
            {
                if (HopByHopHeaders.Contains(header.Key) || ContentHeaders.Contains(header.Key))
                    continue;
                context.Response.Headers[header.Key] = string.Join(", ", header.Value);
            }

            var respContentType = response.Content.Headers.ContentType?.ToString();
            if (!string.IsNullOrEmpty(respContentType))
                context.Response.Headers["Content-Type"] = respContentType;
            context.Response.Headers["Content-Length"] = responseBody.Length.ToString();

            await context.Response.OutputStream.WriteAsync(responseBody).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            isError = true;
            statusCode = 502;
            try
            {
                context.Response.StatusCode = HttpStatusCode.BadGateway;
                context.Response.StatusDescription = "Bad Gateway";
                var body = System.Text.Encoding.UTF8.GetBytes($"relay-client error: {FullChain(ex)}");
                await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
            }
            catch { }
        }
        finally
        {
            context.Response.Close();
            sw.Stop();
            RequestLogged?.Invoke(this, new RequestLogEntry(
                DateTimeOffset.Now, _displayName, method, localPath,
                statusCode, sw.ElapsedMilliseconds, isError || statusCode >= 400));
        }
    }

    private static string FullChain(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null; e = e.InnerException)
            parts.Add(e.Message);
        return string.Join(" → ", parts);
    }

    private static string ExtractLocalPath(Uri relayUrl, string entityPath)
    {
        var path = relayUrl.AbsolutePath;
        var prefix = "/" + entityPath;

        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = path[prefix.Length..];
            if (rest.Length == 0) rest = "/";
            else if (rest[0] != '/') rest = "/" + rest;
            return rest + relayUrl.Query;
        }

        return relayUrl.PathAndQuery;
    }
}
