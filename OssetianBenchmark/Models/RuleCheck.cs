namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class RuleCheck
{
    [JsonPropertyName("mustContain")]
    public List<string> MustContain { get; set; } = new();

    [JsonPropertyName("mustNotContain")]
    public List<string> MustNotContain { get; set; } = new();
}
