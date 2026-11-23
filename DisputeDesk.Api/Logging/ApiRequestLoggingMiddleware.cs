using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Serilog.Context;

namespace DisputeDesk.Api.Logging;

internal sealed class ApiRequestLoggingMiddleware(RequestDelegate next, ILogger<ApiRequestLoggingMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public static string CorrelationId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = CorrelationId(context);
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var method = context.Request.Method switch
        {
            "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" => context.Request.Method,
            _ => "OTHER"
        };

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        using var logContext = LogContext.PushProperty("CorrelationId", correlationId);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? LogLevel.Error : LogLevel.Information;
            logger.Log(level, "API {Method} {RoutePattern} returned {StatusCode} in {DurationMs:0.0} ms",
                method, route, statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}