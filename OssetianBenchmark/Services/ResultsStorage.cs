namespace OssetianBenchmark.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using OssetianBenchmark.Models;

public class ResultsStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _resultsFile;
    private readonly object _lock = new();
    private readonly List<BenchmarkResult> _results;

    public ResultsStorage(string resultsFile, IEnumerable<BenchmarkTask> tasks)
    {
        _resultsFile = resultsFile;
        _results = new List<BenchmarkResult>();

        if (File.Exists(resultsFile))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<List<BenchmarkResult>>(File.ReadAllText(resultsFile), Options);
                if (existing is not null)
                {
                    _results.AddRange(existing);
                }
            }
            catch (JsonException)
            {
            }
        }
    }

    public void Save(BenchmarkResult result)
    {
        lock (_lock)
        {
            var existing = _results.FirstOrDefault(
                r => r.TaskId == result.TaskId && r.CandidateModel == result.CandidateModel);
            if (existing is not null)
            {
                _results[_results.IndexOf(existing)] = result;
            }
            else
            {
                _results.Add(result);
            }

            var directory = Path.GetDirectoryName(_resultsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_resultsFile, JsonSerializer.Serialize(_results, Options));
        }
    }

    public void SaveBatch(IEnumerable<BenchmarkResult> batch)
    {
        lock (_lock)
        {
            foreach (var result in batch)
            {
                var existing = _results.FirstOrDefault(
                    r => r.TaskId == result.TaskId && r.CandidateModel == result.CandidateModel);
                if (existing is not null)
                {
                    _results[_results.IndexOf(existing)] = result;
                }
                else
                {
                    _results.Add(result);
                }
            }

            var directory = Path.GetDirectoryName(_resultsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_resultsFile, JsonSerializer.Serialize(_results, Options));
        }
    }

    public IReadOnlyList<BenchmarkResult> GetAll()
    {
        lock (_lock)
        {
            return _results.ToList();
        }
    }
}
