using Microsoft.Extensions.Configuration;
using OssetianBenchmark.Models;
using OssetianBenchmark.Reports;
using OssetianBenchmark.Services;
using Spectre.Console;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    AnsiConsole.MarkupLine("[yellow]\nПрерывание...[/]");
};

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var config = configuration.GetSection("Benchmark").Get<BenchmarkConfig>()
    ?? throw new InvalidOperationException("Секция [Benchmark] не найдена в appsettings.json.");

if (string.IsNullOrWhiteSpace(config.ApiKey) || config.ApiKey.Contains("ТВОЙ_API_КЛЮЧ"))
{
    AnsiConsole.MarkupLine("[red]В appsettings.json не задан валидный [bold]ApiKey[/].[/]");
    AnsiConsole.MarkupLine("Открой OssetianBenchmark/appsettings.json и впиши свой ключ в поле [bold]ApiKey[/].");
    return 1;
}

var taskLoader = new TaskLoader();
var tasks = taskLoader.Load(ResolvePath(config.TasksFile));

AnsiConsole.MarkupLine($"[bold]Загружено задач:[/] {tasks.Count}");
AnsiConsole.MarkupLine($"[bold]Провайдер:[/] {config.BaseUrl}");
AnsiConsole.MarkupLine($"[bold]Тестируемая модель:[/] {config.TestedModel}");
AnsiConsole.MarkupLine($"[bold]Модель-судья:[/] {config.JudgeModel}");
AnsiConsole.MarkupLine($"[bold]Параллельность:[/] {config.MaxConcurrency} | [bold]Retry:[/] {config.MaxRetries}");
AnsiConsole.WriteLine();

var client = new LlmClient(config);
var ruleEvaluator = new RuleBasedEvaluator();
var judgeEvaluator = new LlmJudgeEvaluator(client, config);
var resultsFile = ResolvePath(config.ResultsFile);
var storage = new ResultsStorage(resultsFile, tasks);
var runner = new BenchmarkRunner(config, client, ruleEvaluator, judgeEvaluator, storage);

AnsiConsole.MarkupLine("[bold green]Запуск бенчмарка...[/]");
await runner.RunAsync(tasks, cts.Token);

var results = storage.GetAll();
var report = new ReportGenerator().Generate(results);
var reportFile = ResolvePath(config.ReportFile);

var dir = Path.GetDirectoryName(reportFile);
if (!string.IsNullOrEmpty(dir))
{
    Directory.CreateDirectory(dir);
}
File.WriteAllText(reportFile, report);

AnsiConsole.MarkupLine($"[green]Готово! Отчёт сохранён:[/] {reportFile}");
AnsiConsole.MarkupLine($"[green]Результаты:[/] {resultsFile}");
return 0;

static string ResolvePath(string path)
{
    return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
