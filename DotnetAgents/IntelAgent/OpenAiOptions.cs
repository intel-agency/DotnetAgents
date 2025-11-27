namespace IntelAgent;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    public string? Endpoint { get; set; }

    public string? FixturePath { get; set; }
}
