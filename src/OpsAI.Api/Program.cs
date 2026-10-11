using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpsAI.Api.Endpoints;
using OpsAI.Api.Workers;
using OpsAI.Core.Validation;
using OpsAI.Infrastructure;
using OpsAI.Infrastructure.Data;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Dynamic Port binding for Render / Cloud Run environments
var envPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(envPort))
{
    builder.WebHost.UseUrls($"http://+:{envPort}");
}

// Structured Logging via Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

// OpenAPI & Scalar Documentation
builder.Services.AddOpenApi();

// Health Checks
builder.Services.AddHealthChecks();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<TelemetryIngestRequestValidator>();

// Infrastructure (EF Core, Channels, AI Engine, Webhooks)
builder.Services.AddInfrastructure(builder.Configuration);

// Hosted Background Channel Consumer
builder.Services.AddHostedService<IncidentProcessingWorker>();

var app = builder.Build();

// Ensure Database is Created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OpsAIDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// OpenAPI Specification & Scalar Interactive Documentation
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("OpsAI - Enterprise Incident Triaging API")
        .WithTheme(ScalarTheme.Moon)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

// Root Redirect to Scalar UI
app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

// Health Check Endpoints
app.MapHealthChecks("/healthz").WithTags("Health").WithSummary("Liveness probe endpoint");

// Map Incident & Simulation Endpoints
app.MapIncidentEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
