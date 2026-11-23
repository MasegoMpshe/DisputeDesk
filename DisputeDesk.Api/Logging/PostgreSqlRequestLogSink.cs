using Npgsql;
using Serilog.Core;
using Serilog.Events;

namespace DisputeDesk.Api.Logging;

internal sealed class PostgreSqlRequestLogSink(string connectionString) : IBatchedLogEventSink
{
    public static bool IsRequestCompletion(LogEvent entry) =>
        entry.Properties.TryGetValue("SourceContext", out var source)
        && source is ScalarValue { Value: string category }
        && category == typeof(ApiRequestLoggingMiddleware).FullName;

    public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
    {
        // Keep storage latency bounded so an unavailable log database cannot delay requests or stall shutdown.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            await using var commands = new NpgsqlBatch(connection);
            foreach (var entry in batch)
            {
                var command = new NpgsqlBatchCommand("""
                    INSERT INTO api_request_logs
                        (occurred_at, correlation_id, method, route_pattern, status_code, duration_ms, level)
                    VALUES ($1, $2, $3, $4, $5, $6, $7)
                    """);
                command.Parameters.Add(new NpgsqlParameter<DateTimeOffset> { TypedValue = entry.Timestamp.ToUniversalTime() });
                command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = Value<string>(entry, "CorrelationId") });
                command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = Value<string>(entry, "Method") });
                command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = Value<string>(entry, "RoutePattern") });
                command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = Value<int>(entry, "StatusCode") });
                command.Parameters.Add(new NpgsqlParameter<double> { TypedValue = Value<double>(entry, "DurationMs") });
                command.Parameters.Add(new NpgsqlParameter<string> { TypedValue = entry.Level.ToString() });
                commands.BatchCommands.Add(command);
            }

            await commands.ExecuteNonQueryAsync(timeout.Token);
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException or TimeoutException)
        {
            // Avoid logging through Serilog here because that would feed the failing sink.
            Console.Error.WriteLine("PostgreSQL request log write failed. Consult the console request logs.");
            throw;
        }
    }

    private static T Value<T>(LogEvent entry, string key) => (T)((ScalarValue)entry.Properties[key]).Value!;
}