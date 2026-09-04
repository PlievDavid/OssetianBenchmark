namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class RawDialogue
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; set; } = string.Empty;

    [JsonPropertyName("lines")]
    public List<string> Lines { get; set; } = new();

    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;
}
