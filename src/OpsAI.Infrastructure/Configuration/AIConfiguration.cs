namespace OpsAI.Infrastructure.Configuration;

public sealed class AIConfiguration
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "Gemini";
    private string? _apiKey;
    public string? ApiKey
    {
        get => _apiKey?.Trim().Trim('"', '\'');
        set => _apiKey = value;
    }
    public string ModelId { get; set; } = "gemini-2.0-flash";
    public string? Endpoint { get; set; }
    public int TimeoutSeconds { get; set; } = 30;

    public string GetEffectiveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(Endpoint))
        {
            return Endpoint;
        }

        return Provider.ToLowerInvariant() switch
        {
            "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/",
            "groq" => "https://api.groq.com/openai/v1",
            "ollama" => "http://localhost:11434/v1",
            "openai" => "https://api.openai.com/v1",
            _ => "https://generativelanguage.googleapis.com/v1beta/openai/"
        };
    }
}
