namespace OssetianBenchmark.Services;

using Microsoft.Playwright;

public abstract class PlaywrightConnectorBase : IChatConnector
{
    protected IPlaywright? PlaywrightInstance;
    protected IBrowser? Browser;
    protected IPage? Page;

    protected BrowserPool Pool { get; }
    public abstract string Name { get; }

    protected PlaywrightConnectorBase(BrowserPool pool)
    {
        Pool = pool;
    }

    public virtual async Task InitializeAsync(CancellationToken ct = default)
    {
        var (pw, browser) = await Pool.AcquireAsync(ct).ConfigureAwait(false);
        PlaywrightInstance = pw;
        Browser = browser;

        var context = await Browser.NewContextAsync(GetContextOptions()).ConfigureAwait(false);
        Page = await context.NewPageAsync().ConfigureAwait(false);
        await NavigateToChatAsync(Page, ct).ConfigureAwait(false);
    }

    protected virtual BrowserNewContextOptions GetContextOptions()
    {
        return new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        };
    }

    protected abstract Task NavigateToChatAsync(IPage page, CancellationToken ct);
    protected abstract Task SendInputAsync(IPage page, string prompt, CancellationToken ct);
    protected abstract Task<string> WaitForResponseAsync(IPage page, CancellationToken ct);

    public virtual async Task<string> CompleteAsync(string userPrompt, string? systemPrompt = null, CancellationToken ct = default)
    {
        if (Page is null)
        {
            throw new InvalidOperationException($"{Name}: page is not initialized.");
        }

        var fullPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? userPrompt
            : $"{systemPrompt}\n\n{userPrompt}";

        await SendInputAsync(Page, fullPrompt, ct).ConfigureAwait(false);
        var response = await WaitForResponseAsync(Page, ct).ConfigureAwait(false);
        return response;
    }

    public virtual async Task ResetChatAsync(CancellationToken ct = default)
    {
        if (Page is not null)
        {
            try { await Page.CloseAsync().ConfigureAwait(false); } catch { }
            Page = null;
        }

        if (Browser is not null)
        {
            var context = await Browser.NewContextAsync(GetContextOptions()).ConfigureAwait(false);
            Page = await context.NewPageAsync().ConfigureAwait(false);
            await NavigateToChatAsync(Page, ct).ConfigureAwait(false);
        }
    }

    protected async Task DumpPageDebug(IPage page, string tag)
    {
        try
        {
            var content = await page.ContentAsync().ConfigureAwait(false);
            var dir = Path.Combine(AppContext.BaseDirectory, "data", "debug");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{Name}_{tag}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            await File.WriteAllTextAsync(file, content).ConfigureAwait(false);
            Console.WriteLine($"  [{Name}] HTML дамп: {file}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{Name}] Ошибка дампа: {ex.Message}");
        }
    }

    protected async Task DumpPageDebugWithSelectors(IPage page, string tag)
    {
        try
        {
            var probe = await page.EvaluateAsync<string>(@"
                (() => {
                    const results = [];
                    const selectors = [
                        '[data-copyairesponse]',
                        '[data-message-copy-content]',
                        '[translate=""no""]',
                        '[data-message-role]',
                        '.MessageItem-styled__UserMessageFooter-sc-53da5397-4',
                        '.FuturisChatMessage-Actions',
                        '.FuturisChatMessage',
                        'div[class*=""MessageItem""]',
                        'div[class*=""AssistantMessage""]',
                        'div[class*=""ChatMessage""]',
                        'div[class*=""MessageFromBot""]',
                        'div[class*=""BotMessage""]',
                        'div[class*=""message_from_bot""]',
                        'div[class*=""message-from-bot""]',
                        'div[class*=""message_from_alice""]',
                        'div[class*=""ai-message""]',
                        'div[class*=""assistant""]',
                        'div[role=""assistant""]',
                        'div[role=""bot""]',
                        'div[role=""response""]',
                        'div[class*=""response""]',
                        'div[class*=""answer""]',
                        'div[class*=""MarkdownRoot""]',
                        'div[class*=""MarkdownText""]',
                        'div[class*=""markdown""]',
                        'div[class*=""copyairesponse""]',
                        'button[class*=""copyairesponse""]',
                    ];
                    for (const s of selectors) {
                        try {
                            const els = document.querySelectorAll(s);
                            if (els.length > 0) {
                                const texts = [];
                                for (let i = 0; i < Math.min(els.length, 3); i++) {
                                    const el = els[i];
                                    const txt = (el.innerText || '').substring(0, 100);
                                    const cls = el.className ? (' class=""' + el.className.substring(0, 100) + '""') : '';
                                    const tag = el.tagName;
                                    texts.push(tag + cls + ': ' + JSON.stringify(txt));
                                }
                                results.push('FOUND ' + els.length + 'x [' + s + ']:\\n  ' + texts.join('\\n  '));
                            }
                        } catch(e) {}
                    }
                    const body = document.body.innerText || '';
                    const bodyLen = body.length;
                    results.push('BODY length: ' + bodyLen);
                    const last300 = body.substring(Math.max(0, bodyLen - 300));
                    results.push('BODY last 300: ' + JSON.stringify(last300));
                    return results.join('\\n\\n');
                })()
            ").ConfigureAwait(false);
            Console.WriteLine($"  [{Name}] === SELECTOR PROBE ===");
            Console.WriteLine($"  [{Name}] {probe}");
            Console.WriteLine($"  [{Name}] === END PROBE ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{Name}] Probe error: {ex.Message}");
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Page is not null)
        {
            try { await Page.CloseAsync().ConfigureAwait(false); } catch { }
            Page = null;
        }
    }
}
