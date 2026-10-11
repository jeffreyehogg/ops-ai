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
        var postgresConnection = configuration.GetConnectionString("PostgresConnection")
            ?? configuration["ConnectionStrings:PostgresConnection"]
            ?? configuration["ConnectionStrings__PostgresConnection"]
            ?? configuration["DATABASE_URL"]
            ?? configuration["POSTGRES_URL"]
            ?? configuration["POSTGRESQL_URL"];

        var sqliteConnection = configuration.GetConnectionString("SqliteConnection");
        if (string.IsNullOrWhiteSpace(sqliteConnection))
        {
            var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            sqliteConnection = isContainer ? "Data Source=/tmp/opsai.db" : "Data Source=opsai.db";
        }

        services.AddDbContext<OpsAIDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(postgresConnection))
            {
                var normalized = NormalizePostgresConnectionString(postgresConnection);
                options.UseNpgsql(normalized);
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

    private static string NormalizePostgresConnectionString(string connection)
    {
        connection = connection.Trim().Trim('"', '\'');

        if (connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connection);
            var userInfo = uri.UserInfo.Split(':');
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var database = uri.AbsolutePath.TrimStart('/');

            var builder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = string.IsNullOrWhiteSpace(database) ? "neondb" : database,
                Username = username,
                Password = password,
                SslMode = Npgsql.SslMode.Require
            };

            return builder.ConnectionString;
        }

        return connection;
    }
}
