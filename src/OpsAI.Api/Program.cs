using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

// Forwarded Headers for Reverse Proxy SSL termination (Render, Cloudflare)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Permissive CORS Policy for Interactive API documentation & web clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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

app.UseForwardedHeaders();
app.UseCors();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        var ex = exceptionHandlerPathFeature?.Error;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "InternalServerError",
            message = ex?.Message,
            details = ex?.InnerException?.Message,
            type = ex?.GetType().Name
        });
    });
});

// Ensure Database and Schema are Created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OpsAIDbContext>();
    if (db.Database.IsNpgsql())
    {
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS opsai;");
        var creator = db.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
        try
        {
            await creator.CreateTablesAsync();
        }
        catch
        {
            // Tables already created in opsai schema
        }
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
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

// Root Redirect to Scalar UI (supports both GET and HEAD)
app.MapMethods("/", ["GET", "HEAD"], () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

// Health Check Endpoints
app.MapHealthChecks("/healthz").WithTags("Health").WithSummary("Liveness probe endpoint");

// Map Incident & Simulation Endpoints
app.MapIncidentEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
