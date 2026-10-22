using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CompanyRegistry.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation id: the caller's X-Correlation-ID if it is sane, otherwise a new one.
/// The id is echoed back in the response header and attached to every log line written during the request.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope("CorrelationId {CorrelationId}", correlationId);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // The hosting platform polls /health every few seconds; keep those out of the normal log.
            var level = context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug : LogLevel.Information;
            logger.Log(
                level,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    // Only accept ids that are safe to write into logs and headers.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
