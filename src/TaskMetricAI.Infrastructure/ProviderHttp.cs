using System.Net;
using System.Text.Json;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure;

public static class ProviderHttp
{
    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, int maxBytes, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.Forbidden &&
                 (response.Headers.RetryAfter is not null || response.Headers.Contains("X-RateLimit-Remaining") &&
                  response.Headers.GetValues("X-RateLimit-Remaining").Contains("0"))))
                throw new OperationException(429, "provider_rate_limited");
            throw new OperationException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => 403,
                HttpStatusCode.NotFound => 404,
                _ => 502
            }, "provider_request_failed");
        }
        Require.That(response.Content.Headers.ContentLength is not > 4_000_000, 422, "provider_response_too_large");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(block, ct)) > 0)
        {
            Require.That(buffer.Length + count <= maxBytes, 422, "provider_response_too_large");
            await buffer.WriteAsync(block.AsMemory(0, count), ct);
        }
        try { return JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 64 }); }
        catch (JsonException) { throw new OperationException(502, "provider_invalid_response"); }
    }
}
