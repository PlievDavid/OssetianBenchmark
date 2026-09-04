namespace OssetianBenchmark.Services;

public interface IChatConnector : IAsyncDisposable
{
    string Name { get; }
    Task InitializeAsync(CancellationToken ct = default);
    Task ResetChatAsync(CancellationToken ct = default);
    Task<string> CompleteAsync(string userPrompt, string? systemPrompt = null, CancellationToken ct = default);
}
