namespace OssetianBenchmark.Services;

using System.Text.Json;
using OssetianBenchmark.Models;

public class TaskLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public List<BenchmarkTask> Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Файл задач не найден: {path}", path);
        }

        var json = File.ReadAllText(path);
        var tasks = JsonSerializer.Deserialize<List<BenchmarkTask>>(json, Options);

        if (tasks is null || tasks.Count == 0)
        {
            throw new InvalidDataException($"В файле '{path}' нет ни одной задачи.");
        }

        return tasks;
    }
}
