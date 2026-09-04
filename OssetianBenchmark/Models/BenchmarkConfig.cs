namespace OssetianBenchmark.Models;

using System.Text.Json.Serialization;

public class BenchmarkConfig
{
    [JsonPropertyName("ApiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("BaseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("TestedModel")]
    public string TestedModel { get; set; } = string.Empty;

    [JsonPropertyName("JudgeModel")]
    public string JudgeModel { get; set; } = string.Empty;

    [JsonPropertyName("Temperature")]
    public double Temperature { get; set; } = 0.0;

    [JsonPropertyName("MaxConcurrency")]
    public int MaxConcurrency { get; set; } = 5;

    [JsonPropertyName("MaxRetries")]
    public int MaxRetries { get; set; } = 5;

    [JsonPropertyName("TasksFile")]
    public string TasksFile { get; set; } = "data/tasks.json";

    [JsonPropertyName("ResultsFile")]
    public string ResultsFile { get; set; } = "data/results.json";

    [JsonPropertyName("ReportFile")]
    public string ReportFile { get; set; } = "data/report.md";

    [JsonPropertyName("Models")]
    public List<string> Models { get; set; } = new();

    [JsonPropertyName("ExcelFile")]
    public string ExcelFile { get; set; } = "data/benchmark.xlsx";

    [JsonPropertyName("Headless")]
    public bool Headless { get; set; } = false;
}
