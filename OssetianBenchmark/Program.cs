using System.Text.Json;
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

if (config.Models.Count == 0)
{
    AnsiConsole.MarkupLine("[red]В appsettings.json не задан список моделей в поле [bold]Models[/].[/]");
    return 1;
}

if (args.Length > 0 && args[0] == "extract")
{
    AnsiConsole.MarkupLine("[bold yellow]Режим: извлечение диалогов с ironau.ru[/]");
    var extractor = new DialogueExtractor();
    await extractor.InitializeAsync(cts.Token).ConfigureAwait(false);
    var dialogues = await extractor.ExtractAllAsync(cts.Token).ConfigureAwait(false);
    var rawFile = Path.IsPathRooted("data/raw_dialogues.json")
        ? "data/raw_dialogues.json"
        : Path.Combine(AppContext.BaseDirectory, "data/raw_dialogues.json");
    DialogueExtractor.SaveToJson(dialogues, rawFile);
    AnsiConsole.MarkupLine($"[green]Извлечено диалогов: {dialogues.Count} → {rawFile}[/]");
    await extractor.DisposeAsync().ConfigureAwait(false);
    return 0;
}

if (args.Length > 0 && args[0] == "generate")
{
    AnsiConsole.MarkupLine("[bold yellow]Режим: генерация open-ended задач из диалогов[/]");
    var rawFile = Path.IsPathRooted("data/raw_dialogues.json")
        ? "data/raw_dialogues.json"
        : Path.Combine(AppContext.BaseDirectory, "data/raw_dialogues.json");

    if (!File.Exists(rawFile))
    {
        AnsiConsole.MarkupLine($"[red]Файл {rawFile} не найден. Сначала выполните: dotnet run -- extract[/]");
        return 1;
    }

    var rawJson = File.ReadAllText(rawFile);
    var dialogues = JsonSerializer.Deserialize<List<RawDialogue>>(rawJson) ?? new();
    AnsiConsole.MarkupLine($"[bold]Загружено диалогов:[/] {dialogues.Count}");

    var generator = new TaskGenerator();
    var newTasks = generator.GenerateFromDialogues(dialogues);
    AnsiConsole.MarkupLine($"[bold]Сгенерировано задач:[/] {newTasks.Count}");

    var tasksFile = Path.IsPathRooted(config.TasksFile)
        ? config.TasksFile
        : Path.Combine(AppContext.BaseDirectory, config.TasksFile);

    var existingTasks = new TaskLoader().Load(tasksFile);
    var existingIds = existingTasks.Select(t => t.Id).ToHashSet();

    var toAdd = newTasks.Where(t => !existingIds.Contains(t.Id)).ToList();
    var allTasks = existingTasks.Concat(toAdd).ToList();

    var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    File.WriteAllText(tasksFile, JsonSerializer.Serialize(allTasks, options));
    AnsiConsole.MarkupLine($"[green]Итого задач в tasks.json: {allTasks.Count} (добавлено {toAdd.Count} open-ended)[/]");
    return 0;
}

if (args.Length > 0 && args[0] == "report")
{
    AnsiConsole.MarkupLine("[bold yellow]Режим: генерация отчёта из существующих результатов (без прогона)[/]");
    var resultsFile = ResolvePath(config.ResultsFile);
    var storage = new ResultsStorage(resultsFile, new List<BenchmarkTask>());
    var results = storage.GetAll();

    AnsiConsole.MarkupLine($"[bold]Записей:[/] {results.Count} ({results.Select(r => r.TaskId).Distinct().Count()} задач)");

    var report = new ReportGenerator().Generate(results);
    var reportFile = ResolvePath(config.ReportFile);
    var dir = Path.GetDirectoryName(reportFile);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
    File.WriteAllText(reportFile, report);
    AnsiConsole.MarkupLine($"[green]Отчёт:[/] {reportFile}");

    var excelFile = ResolvePath(config.ExcelFile);
    new ExcelExporter().Export(results, excelFile);
    AnsiConsole.MarkupLine($"[green]Excel:[/] {excelFile}");

    AnsiConsole.MarkupLine("[bold green]Готово![/]");
    return 0;
}

var taskLoader = new TaskLoader();
var tasks = taskLoader.Load(ResolvePath(config.TasksFile));

AnsiConsole.MarkupLine($"[bold]Загружено задач:[/] {tasks.Count}");
AnsiConsole.MarkupLine($"[bold]Модели:[/] {string.Join(", ", config.Models)}");
AnsiConsole.MarkupLine($"[bold]Параллельность:[/] {config.MaxConcurrency}");
AnsiConsole.WriteLine();

var pool = new BrowserPool();
var connectors = new Dictionary<string, IChatConnector>();

try
{
    foreach (var modelName in config.Models)
    {
        AnsiConsole.MarkupLine($"[bold]Инициализация коннектора:[/] {modelName}");
        try
        {
            var connector = CreateConnector(modelName, pool);
            await connector.InitializeAsync(cts.Token).ConfigureAwait(false);
            connectors[modelName] = connector;
            AnsiConsole.MarkupLine($"[green]  {modelName} готов.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]  {modelName} не удалось инициализировать: {ex.Message}[/]");
        }
    }

    if (connectors.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]Ни один коннектор не инициализирован. Завершение.[/]");
        return 1;
    }

    var ruleEvaluator = new RuleBasedEvaluator();
    var judgeEvaluator = new ChatJudgeEvaluator();
    var resultsFile = ResolvePath(config.ResultsFile);
    var storage = new ResultsStorage(resultsFile, tasks);

    var runner = new BenchmarkRunner(config, ruleEvaluator, judgeEvaluator, storage, connectors);

    AnsiConsole.MarkupLine("[bold green]Запуск бенчмарка (перекрёстное судейство)...[/]");
    var runTasks = tasks;
    int taskFrom = 0, taskCount = 0;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--from" && i + 1 < args.Length && int.TryParse(args[i + 1], out var from))
            taskFrom = from;
        else if (args[i] == "--count" && i + 1 < args.Length && int.TryParse(args[i + 1], out var count))
            taskCount = count;
        else if (int.TryParse(args[i], out var limit) && limit > 0 && limit < tasks.Count)
            taskCount = limit;
    }
    if (taskFrom > 0 || taskCount > 0)
    {
        var start = Math.Max(0, taskFrom - 1);
        var end = taskCount > 0 ? Math.Min(tasks.Count, start + taskCount) : tasks.Count;
        runTasks = tasks.Skip(start).Take(end - start).ToList();
        AnsiConsole.MarkupLine($"[yellow]Режим: задачи {start + 1}–{end} ({runTasks.Count} шт.).[/]");
    }
    await runner.RunAsync(runTasks, cts.Token).ConfigureAwait(false);

    var results = storage.GetAll();
    var report = new ReportGenerator().Generate(results);
    var reportFile = ResolvePath(config.ReportFile);

    var dir = Path.GetDirectoryName(reportFile);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
    File.WriteAllText(reportFile, report);
    AnsiConsole.MarkupLine($"[green]Отчёт:[/] {reportFile}");

    var excelFile = ResolvePath(config.ExcelFile);
    new ExcelExporter().Export(results, excelFile);
    AnsiConsole.MarkupLine($"[green]Excel:[/] {excelFile}");

    AnsiConsole.MarkupLine("[bold green]Готово![/]");
}
finally
{
    foreach (var connector in connectors.Values)
    {
        await connector.DisposeAsync().ConfigureAwait(false);
    }
    await pool.DisposeAsync().ConfigureAwait(false);
}

return 0;

static IChatConnector CreateConnector(string modelName, BrowserPool pool)
{
    return modelName.ToLowerInvariant() switch
    {
        "luna" or "duck" or "duckai" => new DuckAiConnector(pool),
        "gigachat" or "giga" => new GigaChatConnector(pool),
        "alice" or "yandex" or "aliceai" => new AliceConnector(pool),
        _ => throw new InvalidOperationException($"Неизвестная модель '{modelName}'. Допустимы: Luna, GigaChat, Alice."),
    };
}

static string ResolvePath(string path)
{
    return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
}
