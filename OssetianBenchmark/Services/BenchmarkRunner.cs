namespace OssetianBenchmark.Services;

using OssetianBenchmark.Models;
using Spectre.Console;

public class BenchmarkRunner
{
    private readonly BenchmarkConfig _config;
    private readonly RuleBasedEvaluator _ruleEvaluator;
    private readonly ChatJudgeEvaluator _judgeEvaluator;
    private readonly ResultsStorage _storage;
    private readonly Dictionary<string, IChatConnector> _connectors;

    public BenchmarkRunner(
        BenchmarkConfig config,
        RuleBasedEvaluator ruleEvaluator,
        ChatJudgeEvaluator judgeEvaluator,
        ResultsStorage storage,
        Dictionary<string, IChatConnector> connectors)
    {
        _config = config;
        _ruleEvaluator = ruleEvaluator;
        _judgeEvaluator = judgeEvaluator;
        _storage = storage;
        _connectors = connectors;
    }

    public async Task RunAsync(List<BenchmarkTask> tasks, CancellationToken ct = default)
    {
        var models = _config.Models;
        var totalSteps = tasks.Count * (models.Count - 1);
        var completed = 0;

        for (var taskIdx = 0; taskIdx < tasks.Count; taskIdx++)
        {
            var task = tasks[taskIdx];
            var judgeIdx = taskIdx % models.Count;
            var judgeModel = models[judgeIdx];
            var candidateModels = models.Where((_, i) => i != judgeIdx).ToList();

            Console.WriteLine();
            Console.WriteLine($"===== Задача {taskIdx + 1}/{tasks.Count}: {task.Id} ({task.Category}) =====");
            Console.WriteLine($"  Судья: {judgeModel} | Кандидаты: {string.Join(", ", candidateModels)}");

            foreach (var candidateModel in candidateModels)
            {
                ct.ThrowIfCancellationRequested();

                if (!_connectors.TryGetValue(candidateModel, out var candidateConnector))
                {
                    Console.WriteLine($"  !! Коннектор '{candidateModel}' не найден, пропуск.");
                    continue;
                }

                Console.WriteLine($"  -> Кандидат: {candidateModel}");
                await candidateConnector.ResetChatAsync(ct).ConfigureAwait(false);

                var result = await RunCandidateAsync(task, candidateModel, ct).ConfigureAwait(false);
                _storage.Save(result);

                if (!string.IsNullOrEmpty(result.Error))
                    Console.WriteLine($"  !! Ошибка: {result.Error}");
                else
                    Console.WriteLine($"  OK Ответ: {result.ModelResponse.Length} символов, RuleScore={result.RuleScore:F2}");

                var done = Interlocked.Increment(ref completed);
                var pct = (double)done / totalSteps * 100;
                Console.WriteLine($"  == Прогресс: {done}/{totalSteps} ({pct:F0}%)");
            }

            Console.WriteLine($"  --- Судейство ({judgeModel}) ---");

            if (!_connectors.TryGetValue(judgeModel, out var judgeConnector))
            {
                Console.WriteLine($"  !! Коннектор судьи '{judgeModel}' не найден.");
            }
            else
            {
                await judgeConnector.ResetChatAsync(ct).ConfigureAwait(false);

                foreach (var candidateModel in candidateModels)
                {
                    ct.ThrowIfCancellationRequested();

                    await judgeConnector.ResetChatAsync(ct).ConfigureAwait(false);
                    await JudgeCandidateAsync(task, candidateModel, judgeModel, judgeConnector, ct).ConfigureAwait(false);
                }
            }

            _storage.SaveBatch(GetResultsForTask(task.Id));

            foreach (var candidateModel in candidateModels)
            {
                var r = _storage.GetAll().FirstOrDefault(x => x.TaskId == task.Id && x.CandidateModel == candidateModel);
                if (r is not null)
                    Console.WriteLine($"  {candidateModel}: Rule={r.RuleScore:F2} Judge={r.AverageJudgeScore:F2} Final={r.FinalScore:F2} [{r.Verdict()}]");
            }
        }
    }

    private async Task<BenchmarkResult> RunCandidateAsync(BenchmarkTask task, string candidateModel, CancellationToken ct)
    {
        var result = new BenchmarkResult
        {
            TaskId = task.Id,
            Type = task.Type,
            Category = task.Category,
            Prompt = task.Prompt,
            ExpectedAnswer = task.ExpectedAnswer,
            CandidateModel = candidateModel,
            Verified = task.Verified,
            CompletedAtUtc = DateTime.UtcNow,
        };

        if (!_connectors.TryGetValue(candidateModel, out var connector))
        {
            result.Error = $"Коннектор для модели '{candidateModel}' не найден.";
            return result;
        }

        try
        {
            var prompt = task.Prompt + "\n\nНе ищи ответ в интернете.";

            Console.WriteLine($"  [{candidateModel}] Отправка промпта...");
            var response = await connector.CompleteAsync(prompt, ct: ct).ConfigureAwait(false);
            Console.WriteLine($"  [{candidateModel}] Получен ответ: {response.Length} символов");
            result.ModelResponse = response;

            if (task.Type == "open_ended")
            {
                result.AlgoScore = TextSimilarity.AlgoScore(task.ExpectedAnswer, response);
                result.RuleScore = 0.0;
                Console.WriteLine($"  [{candidateModel}] AlgoScore: {result.AlgoScore:F2}");
            }
            else
            {
                result.RuleScore = _ruleEvaluator.Evaluate(task, response);
                Console.WriteLine($"  [{candidateModel}] RuleScore: {result.RuleScore:F2}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{candidateModel}] ИСКЛЮЧЕНИЕ: {ex.Message}");
            result.Error = $"Ошибка кандидата: {ex.Message}";
            result.RuleScore = 0.0;
        }

        return result;
    }

    private async Task JudgeCandidateAsync(BenchmarkTask task, string candidateModel, string judgeModel, IChatConnector judgeConnector, CancellationToken ct)
    {
        var result = _storage.GetAll().FirstOrDefault(
            r => r.TaskId == task.Id && r.CandidateModel == candidateModel);

        if (result is null || string.IsNullOrEmpty(result.ModelResponse))
        {
            Console.WriteLine($"  [{candidateModel}] Пропуск судейства (нет ответа).");
            return;
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.WriteLine($"  [{candidateModel}] Пропуск судейства (ошибка кандидата).");
            return;
        }

        Console.WriteLine($"  [{judgeModel}] Судит {candidateModel}...");

        try
        {
            var isOpenEnded = task.Type == "open_ended";
            var judgeScore = await _judgeEvaluator.ScoreAsync(judgeConnector, task, result.ModelResponse, isOpenEnded, ct).ConfigureAwait(false);
            result.JudgeScores.Add(new JudgeEntry { JudgeModel = judgeModel, JudgeScore = judgeScore });
            Console.WriteLine($"  [{judgeModel}] Оценка: {judgeScore.Total}/8 (G={judgeScore.Grammar} L={judgeScore.Lexicon} M={judgeScore.Match} S={judgeScore.Style})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{judgeModel}] ОШИБКА судьи: {ex.Message}");
        }

        if (result.JudgeScores.Count > 0)
        {
            var validScores = result.JudgeScores
                .Where(j => j.JudgeScore is not null)
                .Select(j => j.JudgeScore!.Normalized)
                .ToList();

            result.AverageJudgeScore = validScores.Count > 0 ? validScores.Average() : 0.0;
        }

        if (task.Type == "open_ended")
        {
            result.FinalScore = 0.5 * result.AlgoScore + 0.5 * result.AverageJudgeScore;
        }
        else
        {
            result.FinalScore = 0.4 * result.RuleScore + 0.6 * result.AverageJudgeScore;
        }
        result.FinalScore = Math.Clamp(result.FinalScore, 0.0, 1.0);
    }

    private IEnumerable<BenchmarkResult> GetResultsForTask(string taskId)
    {
        return _storage.GetAll().Where(r => r.TaskId == taskId);
    }
}
