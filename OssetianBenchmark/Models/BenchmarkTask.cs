namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class BenchmarkTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("expectedAnswer")]
    public string ExpectedAnswer { get; set; } = string.Empty;

    [JsonPropertyName("ruleCheck")]
    public RuleCheck RuleCheck { get; set; } = new();

    [JsonPropertyName("verified")]
    public bool Verified { get; set; } = true;
}
