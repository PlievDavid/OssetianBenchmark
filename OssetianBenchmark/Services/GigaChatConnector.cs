namespace OssetianBenchmark.Services;

using Microsoft.Playwright;

public class GigaChatConnector : PlaywrightConnectorBase
{
    public override string Name => "GigaChat";
    private const string ChatUrl = "https://giga.chat";

    public GigaChatConnector(BrowserPool pool) : base(pool) { }

    protected override async Task NavigateToChatAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Открываю {ChatUrl}...");
        await page.GotoAsync(ChatUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 }).ConfigureAwait(false);
        Console.WriteLine($"  [{Name}] Жду 3с...");
        await page.WaitForTimeoutAsync(3000).ConfigureAwait(false);

        try
        {
            var cookieBtn = page.GetByRole(AriaRole.Button, new() { Name = "Cookies" });
            if (await cookieBtn.CountAsync() > 0)
            {
                Console.WriteLine($"  [{Name}] Нажимаю Cookies...");
                await cookieBtn.First.ClickAsync().ConfigureAwait(false);
                await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            }
        }
        catch (PlaywrightException ex)
        {
            Console.WriteLine($"  [{Name}] Cookies не найдена: {ex.Message}");
        }
    }

    protected override async Task SendInputAsync(IPage page, string prompt, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Ищу textbox...");
        var textarea = page.GetByRole(AriaRole.Textbox).First;
        Console.WriteLine($"  [{Name}] TextBox видим: {await textarea.IsVisibleAsync()}");

        Console.WriteLine($"  [{Name}] Ввожу промпт ({prompt.Length} символов)...");
        await textarea.FillAsync(prompt).ConfigureAwait(false);
        Console.WriteLine($"  [{Name}] Нажимаю Enter...");
        await textarea.PressAsync("Enter").ConfigureAwait(false);
    }

    protected override async Task<string> WaitForResponseAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Ожидаю ответ...");
        await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);

        var prevLen = 0;
        var stableCount = 0;

        for (var i = 0; i < 20; i++)
        {
            ct.ThrowIfCancellationRequested();

            var found = await TryReadResponse(page).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(found))
            {
                Console.WriteLine($"  [{Name}] Текст ответа: {found.Length} символов");
                if (found.Length == prevLen && i > 2)
                {
                    stableCount++;
                    if (stableCount >= 2) { Console.WriteLine($"  [{Name}] Текст стабилен, завершаю."); return found; }
                }
                else { stableCount = 0; prevLen = found.Length; }
            }

            if (i % 4 == 0 && i > 0)
                Console.WriteLine($"  [{Name}] Жду... ({i * 1.2:F0}с)");

            await page.WaitForTimeoutAsync(1200).ConfigureAwait(false);
        }

        Console.WriteLine($"  [{Name}] Селекторы не сработали, дамп HTML...");
        await DumpPageDebugWithSelectors(page, "no_selector_match").ConfigureAwait(false);

        Console.WriteLine($"  [{Name}] Fallback на body...");
        var bodyRaw = await page.EvalOnSelectorAsync("body", "el => el.innerText").ConfigureAwait(false);
        return (bodyRaw?.ToString() ?? "").Trim();
    }

    private static async Task<string?> TryReadResponse(IPage page)
    {
        try
        {
            // Re-query the live DOM each time via JS to avoid detached/stale nodes
            // during streaming. Return the last assistant MarkdownRoot text.
            var text = await page.EvaluateAsync<string>(@"
                (() => {
                    const sels = [
                        '.MarkdownRoot-styled__MarkdownRoot-sc-ab40cf8d-0',
                        'div[translate=""no""] [data-message-copy-content]',
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
                    // assistant content scope fallback
                    const scope = document.querySelector('div[translate=""no""]');
                    if (scope) {
                        const t = (scope.innerText || '').trim();
                        if (t.length > 0) return t;
                    }
                    return null;
                })()
            ").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        catch (PlaywrightException) { }
        return null;
    }
}
