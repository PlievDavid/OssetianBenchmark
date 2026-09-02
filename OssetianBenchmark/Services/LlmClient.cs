namespace OssetianBenchmark.Services;

using System.ClientModel;
using OpenAI;
using OpenAI.Chat;
using OssetianBenchmark.Models;

public class LlmClient
{
    private readonly BenchmarkConfig _config;
    private readonly OpenAIClient _client;
    private readonly Random _rand = new();

    public LlmClient(BenchmarkConfig config)
    {
        _config = config;

        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(config.BaseUrl),
        };

        _client = new OpenAIClient(new ApiKeyCredential(config.ApiKey), options);
    }

    public async Task<string> CompleteAsync(
        string model,
        string userPrompt,
        string? systemPrompt = null,
        CancellationToken ct = default)
    {
        var chatClient = _client.GetChatClient(model);

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));
        }
        messages.Add(ChatMessage.CreateUserMessage(userPrompt));

        var completionOptions = new ChatCompletionOptions
        {
            Temperature = (float)_config.Temperature,
        };

        return await ExecuteWithRetryAsync(
            async token =>
            {
                var completion = await chatClient.CompleteChatAsync(messages, completionOptions, token).ConfigureAwait(false);
                return completion.Value.Content[0].Text?.Trim() ?? string.Empty;
            },
            ct).ConfigureAwait(false);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        var attempts = 0;
        TimeSpan delay = TimeSpan.FromSeconds(1);

        while (true)
        {
            attempts++;
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempts <= _config.MaxRetries && IsRetryable(ex))
            {
                var jitter = TimeSpan.FromMilliseconds(_rand.Next(0, 300));
                var wait = delay * Math.Pow(2, attempts - 1) + jitter;
                Console.Error.WriteLine($"    [retry {attempts}/{_config.MaxRetries}] {ex.Message} (waits {wait.TotalSeconds:F1}s)");
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        if (ex is ClientResultException crEx)
        {
            var status = crEx.Status;
            return status == 429 || status >= 500;
        }
        if (ex is HttpRequestException)
        {
            return true;
        }
        if (ex is TaskCanceledException)
        {
            return true;
        }
        return false;
    }
}
