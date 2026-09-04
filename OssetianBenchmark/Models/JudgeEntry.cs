namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class JudgeEntry
{
    [JsonPropertyName("judgeModel")]
    public string JudgeModel { get; set; } = string.Empty;

    [JsonPropertyName("judgeScore")]
    public JudgeScore? JudgeScore { get; set; }
}
