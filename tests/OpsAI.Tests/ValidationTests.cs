using FluentAssertions;
using OpsAI.Core.DTOs;
using OpsAI.Core.Enums;
using OpsAI.Core.Validation;

namespace OpsAI.Tests;

public sealed class ValidationTests
{
    private readonly TelemetryIngestRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_ShouldPassValidation()
    {
        var request = new TelemetryIngestRequest(
            ServiceName: "order-service",
            Environment: "Production",
            Type: TelemetryType.UnhandledException,
            Title: "NullReferenceException in OrderProcessor",
            Message: "Object reference not set to an instance of an object",
            StackTrace: "at OrderProcessor.Execute()",
            QueryText: null,
            ExecutionDurationMs: 120.5,
            Metadata: null
        );

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Production", "Some Title")]
    [InlineData("service", "", "Some Title")]
    [InlineData("service", "Production", "")]
    public void MissingRequiredFields_ShouldFailValidation(string service, string env, string title)
    {
        var request = new TelemetryIngestRequest(
            ServiceName: service,
            Environment: env,
            Type: TelemetryType.UnhandledException,
            Title: title,
            Message: null,
            StackTrace: null,
            QueryText: null,
            ExecutionDurationMs: null,
            Metadata: null
        );

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NegativeDuration_ShouldFailValidation()
    {
        var request = new TelemetryIngestRequest(
            ServiceName: "service",
            Environment: "Production",
            Type: TelemetryType.SlowQuery,
            Title: "Query slow",
            Message: null,
            StackTrace: null,
            QueryText: "SELECT 1",
            ExecutionDurationMs: -5.0,
            Metadata: null
        );

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(TelemetryIngestRequest.ExecutionDurationMs));
    }
}
