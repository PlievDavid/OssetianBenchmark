namespace OssetianBenchmark.Services;

using OssetianBenchmark.Models;
using Spectre.Console;

public class BenchmarkRunner
{
    private readonly BenchmarkConfig _config;
    private readonly LlmClient _client;
    private readonly RuleBasedEvaluator _ruleEvaluator;
    private readonly LlmJudgeEvaluator _judgeEvaluator;
    private readonly ResultsStorage _storage;

    public BenchmarkRunner(
        BenchmarkConfig config,
        LlmClient client,
        RuleBasedEvaluator ruleEvaluator,
        LlmJudgeEvaluator judgeEvaluator,
        ResultsStorage storage)
    {
        _config = config;
        _client = client;
        _ruleEvaluator = ruleEvaluator;
        _judgeEvaluator = judgeEvaluator;
        _storage = storage;
    }

    public Task RunAsync(List<BenchmarkTask> tasks, CancellationToken ct = default)
    {
        var concurrency = _config.MaxConcurrency > 0 ? _config.MaxConcurrency : 1;
        var completed = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = ct,
        };

        var interactive = AnsiConsole.Profile.Capabilities.Interactive;

        if (!interactive)
        {
            var fallback = new ProgressBar();
            Console.WriteLine(fallback.Render(0, tasks.Count));

            Parallel.ForEachAsync(tasks, options, async (task, token) =>
            {
                var result = await RunSingleAsync(task, token).ConfigureAwait(false);
                _storage.Save(result);

                var done = Interlocked.Increment(ref completed);
                Console.WriteLine(fallback.Render(done, tasks.Count));
            }).GetAwaiter().GetResult();

            return Task.CompletedTask;
        }

        AnsiConsole.Live(new Text(""))
            .Start(ctx =>
            {
                var progress = new ProgressBar();
                ctx.UpdateTarget(new Markup(progress.Render(0, tasks.Count)));
                var semaphore = new SemaphoreSlim(1, 1);

                Parallel.ForEachAsync(tasks, options, async (task, token) =>
                {
                    var result = await RunSingleAsync(task, token).ConfigureAwait(false);
                    _storage.Save(result);

                    Interlocked.Increment(ref completed);
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        ctx.UpdateTarget(new Markup(progress.Render(completed, tasks.Count)));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).GetAwaiter().GetResult();
            });

        return Task.CompletedTask;
    }

    private async Task<BenchmarkResult> RunSingleAsync(BenchmarkTask task, CancellationToken ct)
    {
        var result = new BenchmarkResult
        {
            TaskId = task.Id,
            Category = task.Category,
            Prompt = task.Prompt,
            ExpectedAnswer = task.ExpectedAnswer,
            Verified = task.Verified,
            CompletedAtUtc = DateTime.UtcNow,
        };

        try
        {
            var response = await _client.CompleteAsync(_config.TestedModel, task.Prompt, ct: ct).ConfigureAwait(false);
            result.ModelResponse = response;

            result.RuleScore = _ruleEvaluator.Evaluate(task, response);

            var judge = await _judgeEvaluator.ScoreAsync(task, response, ct).ConfigureAwait(false);
            result.JudgeScore = judge;

            result.FinalScore = 0.4 * result.RuleScore + 0.6 * judge.Normalized;
            result.FinalScore = Math.Clamp(result.FinalScore, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.ModelResponse = "";
            result.RuleScore = 0.0;
            result.JudgeScore = null;
            result.FinalScore = 0.0;
        }

        return result;
    }
}
