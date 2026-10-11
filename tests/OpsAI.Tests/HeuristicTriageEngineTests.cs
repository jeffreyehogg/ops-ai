using FluentAssertions;
using OpsAI.Core.Enums;
using OpsAI.Core.Models;
using OpsAI.Infrastructure.AI;

namespace OpsAI.Tests;

public sealed class HeuristicTriageEngineTests
{
    private readonly HeuristicTriageEngine _engine = new();

    [Fact]
    public async Task TriageAsync_ConnectionPoolExhaustion_ClassifiesAsP1()
    {
        var incident = new Incident
        {
            ServiceName = "billing-api",
            Environment = "Production",
            Type = TelemetryType.ConnectionPoolExhaustion,
            Title = "Timeout expired obtaining connection from pool",
            Message = "The timeout period elapsed prior to obtaining a connection from the pool."
        };

        var result = await _engine.TriageAsync(incident);

        result.Severity.Should().Be(SeverityLevel.P1_Critical);
        result.RootCause.Should().Contain("Connection Pool");
        result.RemediationPlan.Should().Contain("Max Pool Size");
        result.SuggestedSqlFix.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TriageAsync_SlowQuery_GeneratesIndexRecommendation()
    {
        var incident = new Incident
        {
            ServiceName = "reporting-svc",
            Environment = "Production",
            Type = TelemetryType.SlowQuery,
            Title = "Slow query threshold exceeded",
            QueryText = "SELECT * FROM billing_transactions WHERE created_at > NOW()",
            ExecutionDurationMs = 5400.0
        };

        var result = await _engine.TriageAsync(incident);

        result.Severity.Should().Be(SeverityLevel.P2_High);
        result.RootCause.Should().Contain("Unindexed Sequential Scan");
        result.SuggestedSqlFix.Should().Contain("CREATE INDEX CONCURRENTLY");
        result.SuggestedSqlFix.Should().Contain("billing_transactions");
    }

    [Fact]
    public async Task TriageAsync_Deadlock_RecommendsLockAlignment()
    {
        var incident = new Incident
        {
            ServiceName = "inventory-svc",
            Environment = "Production",
            Type = TelemetryType.Deadlock,
            Title = "Transaction was deadlocked",
            Message = "deadlock victim error 1205"
        };

        var result = await _engine.TriageAsync(incident);

        result.Severity.Should().Be(SeverityLevel.P2_High);
        result.RootCause.Should().Contain("Deadlock");
        result.RemediationPlan.Should().Contain("resource access order");
    }

    [Fact]
    public async Task TriageAsync_DownstreamTimeout_RecommendsCircuitBreaker()
    {
        var incident = new Incident
        {
            ServiceName = "order-svc",
            Environment = "Production",
            Type = TelemetryType.DownstreamTimeout,
            Title = "HTTP 504 Gateway Timeout calling payment-provider",
            Message = "TaskCanceledException: The request timed out."
        };

        var result = await _engine.TriageAsync(incident);

        result.Severity.Should().Be(SeverityLevel.P2_High);
        result.RootCause.Should().Contain("Timeout");
        result.RemediationPlan.Should().Contain("circuit breaker");
    }
}
