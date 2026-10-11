using OpsAI.Core.DTOs;
using OpsAI.Core.Enums;

namespace OpsAI.Api.Simulation;

public static class EnterpriseSimulator
{
    public static TelemetryIngestRequest CreateScenario(string? scenarioName)
    {
        return scenarioName?.ToLowerInvariant() switch
        {
            "pool_exhaustion" or "pool" => new TelemetryIngestRequest(
                ServiceName: "payments-settlement-svc",
                Environment: "Production",
                Type: TelemetryType.ConnectionPoolExhaustion,
                Title: "TimeoutExpiredException: Connection pool exhausted under traffic spike",
                Message: "System.InvalidOperationException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool. All 100 pooled connections were in use and max pool size was reached.",
                StackTrace: """
                    at Npgsql.NpgsqlConnector.Open(NpgsqlTimeout timeout, Boolean async, CancellationToken cancellationToken)
                    at Npgsql.ConnectorPool.AllocateLong(NpgsqlTimeout timeout, Boolean async, NpgsqlLogContext logContext, CancellationToken cancellationToken)
                    at Microsoft.EntityFrameworkCore.Storage.RelationalConnection.OpenInternalAsync(Boolean errorsExpected, CancellationToken cancellationToken)
                    at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
                    at OpsAI.Payments.SettlementWorker.ProcessBatchAsync(BatchRequest request) in /src/Services/SettlementWorker.cs:line 142
                    """,
                QueryText: null,
                ExecutionDurationMs: 30005.2,
                Metadata: new Dictionary<string, string>
                {
                    { "ActiveConnections", "100" },
                    { "MaxPoolSize", "100" },
                    { "WaitingQueueDepth", "482" },
                    { "Region", "us-east-1" }
                }
            ),

            "deadlock" => new TelemetryIngestRequest(
                ServiceName: "order-fulfillment-engine",
                Environment: "Production",
                Type: TelemetryType.Deadlock,
                Title: "SqlException: Transaction chosen as deadlock victim (Error 1205)",
                Message: "Microsoft.Data.SqlClient.SqlException (0x80131904): Transaction (Process ID 142) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.",
                StackTrace: """
                    at Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken cancellationToken)
                    at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteNonQueryAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
                    at OpsAI.Inventory.ReservationManager.ReserveStockAsync(Guid orderId, List`1 items) in /src/Inventory/ReservationManager.cs:line 88
                    """,
                QueryText: "UPDATE inventory_allocations SET reserved_count = reserved_count + 1 WHERE warehouse_id = 4 AND sku_id = 'SKU-9921';",
                ExecutionDurationMs: 420.0,
                Metadata: new Dictionary<string, string>
                {
                    { "VictimSpid", "142" },
                    { "ContendingSpid", "198" },
                    { "LockResource", "KEY: 8:72057594043236352" }
                }
            ),

            "timeout" => new TelemetryIngestRequest(
                ServiceName: "checkout-gateway-api",
                Environment: "Production",
                Type: TelemetryType.DownstreamTimeout,
                Title: "TaskCanceledException: Downstream tax-calculation-svc timed out after 15,000ms",
                Message: "System.Threading.Tasks.TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing.",
                StackTrace: """
                    at System.Net.Http.HttpClient.HandleFailure(Exception e, Boolean telemetryStarted, HttpResponseMessage response, HttpResourceIdentifier& resourceIdentifier, CancellationTokenSource cts, CancellationToken cancellationToken, HttpRequestException httpException)
                    at System.Net.Http.HttpClient.SendAsyncCore(HttpRequestMessage request, HttpCompletionOption completionOption, Boolean async, Boolean emitTelemetryStartStop, CancellationToken cancellationToken)
                    at OpsAI.Checkout.Clients.TaxServiceClient.CalculateTaxesAsync(TaxQuoteRequest request) in /src/Checkout/Clients/TaxServiceClient.cs:line 64
                    """,
                QueryText: null,
                ExecutionDurationMs: 15002.8,
                Metadata: new Dictionary<string, string>
                {
                    { "TargetEndpoint", "https://tax-svc.internal/v2/quotes" },
                    { "HttpAttempts", "3" },
                    { "CircuitBreakerState", "HalfOpen" }
                }
            ),

            _ => new TelemetryIngestRequest(
                ServiceName: "billing-ledger-svc",
                Environment: "Production",
                Type: TelemetryType.SlowQuery,
                Title: "SlowQueryDetected: Sequential scan on billing_transactions took 8,420ms",
                Message: "PostgreSQL execution threshold alert: Query duration 8,420ms exceeded SLA limit of 2,000ms. Explain plan indicated Seq Scan across 2,400,000 rows.",
                StackTrace: null,
                QueryText: "SELECT b.id, b.account_id, b.amount, b.created_at, b.status_id FROM billing_transactions b WHERE b.created_at >= '2026-01-01' AND b.status_id = 4 ORDER BY b.created_at DESC;",
                ExecutionDurationMs: 8420.5,
                Metadata: new Dictionary<string, string>
                {
                    { "RowsScanned", "2400000" },
                    { "BufferHits", "14820" },
                    { "BufferMisses", "4920" }
                }
            )
        };
    }
}
