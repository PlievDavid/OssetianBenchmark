namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class BenchmarkResult
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("expectedAnswer")]
    public string ExpectedAnswer { get; set; } = string.Empty;

    [JsonPropertyName("candidateModel")]
    public string CandidateModel { get; set; } = string.Empty;

    [JsonPropertyName("modelResponse")]
    public string ModelResponse { get; set; } = string.Empty;

    [JsonPropertyName("ruleScore")]
    public double RuleScore { get; set; }

    [JsonPropertyName("algoScore")]
    public double AlgoScore { get; set; }

    [JsonPropertyName("judgeScores")]
    public List<JudgeEntry> JudgeScores { get; set; } = new();

    [JsonPropertyName("averageJudgeScore")]
    public double AverageJudgeScore { get; set; }

    [JsonPropertyName("finalScore")]
    public double FinalScore { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; } = true;

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public string Verdict()
    {
        if (FinalScore >= 0.75) return "PASS";
        if (FinalScore >= 0.5) return "PARTIAL";
        return "FAIL";
    }
}
