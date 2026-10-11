using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsAI.Core.Interfaces;
using OpsAI.Infrastructure.AI;
using OpsAI.Infrastructure.Channels;
using OpsAI.Infrastructure.Configuration;
using OpsAI.Infrastructure.Data;
using OpsAI.Infrastructure.Webhooks;

namespace OpsAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind Configurations
        services.Configure<AIConfiguration>(configuration.GetSection(AIConfiguration.SectionName));
        services.Configure<WebhookConfiguration>(configuration.GetSection(WebhookConfiguration.SectionName));

        // Configure Database (PostgreSQL if connection string provided, fallback to SQLite for local dev)
        var postgresConnection = configuration.GetConnectionString("PostgresConnection");
        var sqliteConnection = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=opsai.db";

        services.AddDbContext<OpsAIDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(postgresConnection))
            {
                options.UseNpgsql(postgresConnection);
            }
            else
            {
                options.UseSqlite(sqliteConnection);
            }
        });

        // Repositories & Channels
        services.AddScoped<IIncidentRepository, IncidentRepository>();
        services.AddSingleton<IIncidentChannel, IncidentChannel>();

        // AI Triaging Engines
        services.AddSingleton<HeuristicTriageEngine>();
        services.AddSingleton<ITriageEngine, SemanticKernelTriageEngine>();

        // Webhooks with HttpClient
        services.AddHttpClient<IWebhookDispatcher, WebhookDispatcher>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
