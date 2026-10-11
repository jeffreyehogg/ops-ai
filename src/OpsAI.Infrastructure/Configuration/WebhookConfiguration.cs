namespace OpsAI.Infrastructure.Configuration;

public sealed class WebhookConfiguration
{
    public const string SectionName = "Webhooks";

    public string? SlackWebhookUrl { get; set; }
    public string? DiscordWebhookUrl { get; set; }
    public string? TeamsWebhookUrl { get; set; }
    public bool Enabled { get; set; } = true;
}
