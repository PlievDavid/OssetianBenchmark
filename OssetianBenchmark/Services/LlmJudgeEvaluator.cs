namespace OssetianBenchmark.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using OssetianBenchmark.Models;

public class LlmJudgeEvaluator
{
    private readonly LlmClient _client;
    private readonly BenchmarkConfig _config;

    private const string JudgeSystemPrompt =
        "Ты — эксперт по осетинскому языку (ирон æвзаг). Оцени ответ языковой модели строго по 4 критериям (каждый от 0 до 2): " +
        "GRAMMAR (падежи, эргативность, согласование), LEXICON (отсутствие русских кальк, правильный выбор слов), " +
        "MATCH (соответствие эталонному ответу), STYLE (естественность для носителя). " +
        "Верни СТРОГО один JSON-объект без markdown-обёрток и без пояснений вне JSON.";

    public LlmJudgeEvaluator(LlmClient client, BenchmarkConfig config)
    {
        _client = client;
        _config = config;
    }

    public async Task<JudgeScore> ScoreAsync(
        BenchmarkTask task,
        string modelResponse,
        CancellationToken ct = default)
    {
        var prompt =
            "ЗАДАЧА:\n" + task.Prompt +
            "\n\nЭТАЛОН:\n" + task.ExpectedAnswer +
            "\n\nОТВЕТ МОДЕЛИ:\n" + modelResponse +
            "\n\nВерни СТРОГО JSON без markdown-обёрток:\n" +
            "{\"grammar\":X,\"lexicon\":X,\"match\":X,\"style\":X,\"total\":X,\"reasoning\":\"кратко\"}";

        var raw = await _client.CompleteAsync(_config.JudgeModel, prompt, JudgeSystemPrompt, ct).ConfigureAwait(false);

        return ParseScore(raw);
    }

    private JudgeScore ParseScore(string raw)
    {
        var json = ExtractJson(raw);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            int GetInt(string name, int fallback)
            {
                if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number)
                {
                    return Math.Clamp(v.GetInt32(), 0, 2);
                }
                return fallback;
            }

            var grammar = GetInt("grammar", 0);
            var lexicon = GetInt("lexicon", 0);
            var match = GetInt("match", 0);
            var style = GetInt("style", 0);

            var total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number
                ? Math.Clamp(t.GetInt32(), 0, 8)
                : grammar + lexicon + match + style;

            var reasoning = "";
            if (root.TryGetProperty("reasoning", out var r) && r.ValueKind == JsonValueKind.String)
            {
                reasoning = r.GetString() ?? "";
            }

            return new JudgeScore
            {
                Grammar = grammar,
                Lexicon = lexicon,
                Match = match,
                Style = style,
                Total = total,
                Reasoning = reasoning,
            };
        }
        catch (JsonException)
        {
            return new JudgeScore
            {
                Grammar = 0,
                Lexicon = 0,
                Match = 0,
                Style = 0,
                Total = 0,
                Reasoning = $"Не удалось распарсить JSON судьи: {raw}",
            };
        }
    }

    private static string ExtractJson(string raw)
    {
        var match = Regex.Match(raw, @"\{.*\}", RegexOptions.Singleline);
        return match.Success ? match.Value : raw;
    }
}
