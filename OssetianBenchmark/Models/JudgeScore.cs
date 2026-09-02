namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class JudgeScore
{
    [JsonPropertyName("grammar")]
    public int Grammar { get; set; }

    [JsonPropertyName("lexicon")]
    public int Lexicon { get; set; }

    [JsonPropertyName("match")]
    public int Match { get; set; }

    [JsonPropertyName("style")]
    public int Style { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = string.Empty;

    public double Normalized => Total / 8.0;
}
