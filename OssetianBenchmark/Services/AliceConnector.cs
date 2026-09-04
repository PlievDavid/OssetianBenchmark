namespace OssetianBenchmark.Services;

using Microsoft.Playwright;

public class AliceConnector : PlaywrightConnectorBase
{
    public override string Name => "Alice";
    private const string ChatUrl = "https://alice.yandex.ru";

    public AliceConnector(BrowserPool pool) : base(pool) { }

    protected override async Task NavigateToChatAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Открываю {ChatUrl}...");
        await page.GotoAsync(ChatUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 }).ConfigureAwait(false);
        Console.WriteLine($"  [{Name}] Жду 4с...");
        await page.WaitForTimeoutAsync(4000).ConfigureAwait(false);
    }

    protected override async Task SendInputAsync(IPage page, string prompt, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Ищу textarea...");
        var textarea = page.Locator("textarea.AliceInput-Textarea");
        if (await textarea.CountAsync() == 0)
        {
            textarea = page.Locator("textarea");
        }
        var first = textarea.First;
        Console.WriteLine($"  [{Name}] Textarea видима: {await first.IsVisibleAsync()}");

        Console.WriteLine($"  [{Name}] Ввожу ({prompt.Length} символов)...");
        await first.FillAsync(prompt).ConfigureAwait(false);
        Console.WriteLine($"  [{Name}] Enter...");
        await first.PressAsync("Enter").ConfigureAwait(false);
    }

    protected override async Task<string> WaitForResponseAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Ожидаю ответ...");
        await page.WaitForTimeoutAsync(2000).ConfigureAwait(false);

        var prevLen = 0;
        var stableCount = 0;

        for (var i = 0; i < 45; i++)
        {
            ct.ThrowIfCancellationRequested();

            var found = await TryReadResponse(page).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(found))
            {
                Console.WriteLine($"  [{Name}] Текст: {found.Length} символов");
                if (found.Length == prevLen && i > 2)
                {
                    stableCount++;
                    if (stableCount >= 4) { Console.WriteLine($"  [{Name}] Стабильно."); return CleanAliceText(found); }
                }
                else { stableCount = 0; prevLen = found.Length; }
            }

            if (i % 4 == 0 && i > 0)
                Console.WriteLine($"  [{Name}] Жду... ({i * 1.2:F0}с)");

            await page.WaitForTimeoutAsync(1200).ConfigureAwait(false);
        }

        Console.WriteLine($"  [{Name}] Дамп HTML...");
        await DumpPageDebugWithSelectors(page, "no_match").ConfigureAwait(false);

        var bodyRaw = await page.EvalOnSelectorAsync("body", "el => el.innerText").ConfigureAwait(false);
        return CleanAliceText((bodyRaw?.ToString() ?? ""));
    }

    private static string CleanAliceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (text ?? "").Trim();

        var paragraphs = text
            .Replace("\r", "")
            .Split('\n')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var result = new List<string>();
        foreach (var p in paragraphs)
        {
            if (IsAliceThinking(p) || p.Equals("Алиса", StringComparison.OrdinalIgnoreCase) || p.Equals("Думаю", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result.Add(p);
        }

        if (result.Count == 0) return "";

        var isJudge = paragraphs.Any(p => p.Contains("\"grammar\"", StringComparison.OrdinalIgnoreCase) || p.Contains("total\"", StringComparison.OrdinalIgnoreCase));

        var echoMarkers = !isJudge && result.Any(p => p.Contains("[1]") || p.Contains("[2]") || p.Contains("[3]") || p.Contains("[4]") || p.Contains("________"));
        if (echoMarkers)
        {
            var clean = result
                .Where(p => !p.Contains('[') && !p.Contains("________") && p.Length <= 200)
                .ToList();
            return clean.Count == 0 ? "" : string.Join(" ", clean);
        }

        var textOnly = string.Join(" ", result);
        var last = result[^1];
        if (last.Length >= 3 && last.Length <= 200 && !IsAliceThinking(last))
        {
            if (textOnly.Contains("контекст", StringComparison.OrdinalIgnoreCase) || textOnly.Contains("Основной критерий", StringComparison.OrdinalIgnoreCase) || textOnly.Contains("пропущенную", StringComparison.OrdinalIgnoreCase))
            {
                return last;
            }
        }
        return textOnly;
    }

    private static bool IsAliceThinking(string p)
    {
        if (string.IsNullOrWhiteSpace(p) || p.Length < 5 || p.Length > 500) return false;
        return p.StartsWith("Анализирую ", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Важно ", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Нужно ", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Основной критерий", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Заполни пропущенную", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Отвечай ТОЛЬКО", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Оцениваю ", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Разбираю ", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Посмотрю ", StringComparison.OrdinalIgnoreCase)
            || p.Contains("контекст предыдущих сообщений", StringComparison.OrdinalIgnoreCase)
            || p.Contains("Пропущенную реплику", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> TryReadResponse(IPage page)
    {
        try
        {
            // Re-query the live DOM each time via JS to avoid detached/stale nodes.
            // Return the last assistant Markdown/FuturisMarkdown text.
            var text = await page.EvaluateAsync<string>(@"
                (() => {
                    const sels = [
                        '.FuturisMarkdown',
                        '.MarkdownText',
                        'div[data-message-role]:not([data-message-role=""user""]) .MarkdownText',
                    ];
                    for (const s of sels) {
                        try {
                            const els = document.querySelectorAll(s);
                            if (els.length > 0) {
                                const t = (els[els.length - 1].innerText || '').trim();
                                if (t.length > 0) return t;
                            }
                        } catch(e) {}
                    }
                    // data-message-role container that is NOT user (assistant)
                    const msgs = document.querySelectorAll('[data-message-role]');
                    for (let i = msgs.length - 1; i >= 0; i--) {
                        const role = msgs[i].getAttribute('data-message-role');
                        if (role && role !== 'user') {
                            const t = (msgs[i].innerText || '').trim();
                            if (t.length > 0) return t;
                        }
                    }
                    return null;
                })()
            ").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) return CleanAliceText(text);
        }
        catch (PlaywrightException) { }
        return null;
    }
}
