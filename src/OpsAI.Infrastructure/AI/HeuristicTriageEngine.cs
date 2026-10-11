using System.Diagnostics;
using System.Text.RegularExpressions;
using OpsAI.Core.Enums;
using OpsAI.Core.Interfaces;
using OpsAI.Core.Models;

namespace OpsAI.Infrastructure.AI;

public sealed partial class HeuristicTriageEngine : ITriageEngine
{
    public Task<TriageResult> TriageAsync(Incident incident, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var combinedText = $"{incident.Title} {incident.Message} {incident.StackTrace} {incident.QueryText}";

        SeverityLevel severity;
        string rootCause;
        string impactScope;
        string remediationPlan;
        string? suggestedSqlFix = null;
        string runbookTags;

        if (incident.Type == TelemetryType.ConnectionPoolExhaustion ||
            combinedText.Contains("connection pool", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("prior to obtaining a connection from the pool", StringComparison.OrdinalIgnoreCase) ||
            combinedText.Contains("Npgsql.NpgsqlException: The connection pool has been exhausted", StringComparison.OrdinalIgnoreCase))
        {
            severity = SeverityLevel.P1_Critical;
            rootCause = "Database Connection Pool Exhaustion: Active worker threads or leaked database connections exceeded Max Pool Size.";
            impactScope = $"Critical: Incoming transactions on service '{incident.ServiceName}' are failing with 500 errors due to database starvation.";
            remediationPlan = "1. Verify application DbContext / Connection lifetime scopes (ensure `using` or scoped DI).\n" +
                              "2. Identify long-running queries blocking connections from returning to the pool.\n" +
                              "3. Temporarily scale `Max Pool Size=200` in the database connection string and enable PgBouncer / Azure PgBouncer.";
            suggestedSqlFix = "-- Check active open connections and state\nSELECT pid, usename, state, query_start, query FROM pg_stat_activity WHERE state != 'idle';";
            runbookTags = "database,connection-pool,pgbouncer,critical-outage";
        }
        else if (incident.Type == TelemetryType.Deadlock ||
                 combinedText.Contains("deadlock victim", StringComparison.OrdinalIgnoreCase) ||
                 combinedText.Contains("error 1205", StringComparison.OrdinalIgnoreCase) ||
                 combinedText.Contains("40P01", StringComparison.OrdinalIgnoreCase))
        {
            severity = SeverityLevel.P2_High;
            rootCause = "Database Transaction Deadlock: Concurrent transactions acquired locks on common database resources in conflicting order.";
            impactScope = $"High: One of the conflicting transactions in '{incident.ServiceName}' was aborted as a deadlock victim.";
            remediationPlan = "1. Align resource access order across all write transactions.\n" +
                              "2. Keep database transactions as short as possible; move network I/O outside transaction blocks.\n" +
                              "3. Introduce retry logic with exponential jitter for deadlock victims.";
            suggestedSqlFix = "-- Inspect recent deadlocks and lock contention\nSELECT blocked_locks.pid AS blocked_pid, blocking_locks.pid AS blocking_pid, blocked_activity.query AS blocked_statement\nFROM pg_catalog.pg_locks blocked_locks\nJOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid\nJOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype AND blocking_locks.database IS NOT DISTINCT FROM blocked_locks.database AND blocking_locks.relation IS NOT DISTINCT FROM blocked_locks.relation AND blocking_locks.page IS NOT DISTINCT FROM blocked_locks.page AND blocking_locks.tuple IS NOT DISTINCT FROM blocked_locks.tuple AND blocking_locks.virtualxid IS NOT DISTINCT FROM blocked_locks.virtualxid AND blocking_locks.transactionid IS NOT DISTINCT FROM blocked_locks.transactionid AND blocking_locks.classid IS NOT DISTINCT FROM blocked_locks.classid AND blocking_locks.objid IS NOT DISTINCT FROM blocked_locks.objid AND blocking_locks.objsubid IS NOT DISTINCT FROM blocked_locks.objsubid AND blocking_locks.pid != blocked_locks.pid;";
            runbookTags = "database,deadlock,concurrency,retry-policy";
        }
        else if (incident.Type == TelemetryType.SlowQuery ||
                 (incident.ExecutionDurationMs.HasValue && incident.ExecutionDurationMs > 2000) ||
                 combinedText.Contains("Seq Scan", StringComparison.OrdinalIgnoreCase))
        {
            severity = SeverityLevel.P2_High;
            rootCause = $"Unindexed Sequential Scan: Query execution duration ({incident.ExecutionDurationMs:F0}ms) exceeded acceptable SLAs.";
            impactScope = $"Moderate to High: Elevated CPU and I/O pressure on the database server, leading to thread starvation.";
            remediationPlan = "1. Run EXPLAIN (ANALYZE, BUFFERS) on the target query.\n" +
                              "2. Create a composite B-Tree or partial index matching the filter and join predicates.\n" +
                              "3. Review query execution plan for unexpected Cartesian joins.";
            suggestedSqlFix = BuildIndexRecommendation(incident.QueryText);
            runbookTags = "database,performance,indexing,slow-query";
        }
        else if (incident.Type == TelemetryType.DownstreamTimeout ||
                 combinedText.Contains("TaskCanceledException", StringComparison.OrdinalIgnoreCase) ||
                 combinedText.Contains("TimeoutException", StringComparison.OrdinalIgnoreCase) ||
                 combinedText.Contains("504 Gateway Timeout", StringComparison.OrdinalIgnoreCase))
        {
            severity = SeverityLevel.P2_High;
            rootCause = "Downstream Microservice Latency / Timeout: Outbound HTTP or gRPC request timed out waiting for upstream dependency.";
            impactScope = $"High: Degraded user experience or partial service outage in '{incident.ServiceName}'.";
            remediationPlan = "1. Check downstream service health and latency telemetry in distributed APM.\n" +
                              "2. Verify Polly circuit breaker state and timeout threshold configuration.\n" +
                              "3. Enable fallback caching or graceful degradation for downstream failures.";
            suggestedSqlFix = null;
            runbookTags = "networking,timeout,circuit-breaker,resilience";
        }
        else
        {
            severity = SeverityLevel.P3_Medium;
            rootCause = $"Unhandled Application Exception: {ExtractFirstLine(incident.Message ?? incident.Title)}";
            impactScope = $"Moderate: Isolated request failure in service '{incident.ServiceName}'.";
            remediationPlan = "1. Review stack trace and isolate throwing method frame.\n" +
                              "2. Validate input boundary guards or null reference checks.\n" +
                              "3. Add automated integration test reproducing the exact failure input.";
            suggestedSqlFix = null;
            runbookTags = "application,exception,bug-fix";
        }

        stopwatch.Stop();

        var result = new TriageResult
        {
            IncidentId = incident.Id,
            Severity = severity,
            RootCause = rootCause,
            ImpactScope = impactScope,
            RemediationPlan = remediationPlan,
            SuggestedSqlFix = suggestedSqlFix,
            RunbookTags = runbookTags,
            AiModelUsed = "Heuristic-Rule-Engine-v1.0",
            TriageDurationMs = stopwatch.Elapsed.TotalMilliseconds,
            TriagedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(result);
    }

    private static string ExtractFirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Unknown Exception";
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? lines[0] : text;
    }

    private static string BuildIndexRecommendation(string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return "-- Recommendation: Run EXPLAIN (ANALYZE, BUFFERS) on the target query and create missing indexes on filter columns.";
        }

        var match = TableNameRegex().Match(queryText);
        var tableName = match.Success ? match.Groups[1].Value : "target_table";

        return $"-- Recommended Index Creation\nCREATE INDEX CONCURRENTLY IF NOT EXISTS idx_{tableName}_lookup \nON {tableName} (created_at DESC, status_id);";
    }

    [GeneratedRegex(@"(?:FROM|JOIN)\s+([a-zA-Z0-9_\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex TableNameRegex();
}
