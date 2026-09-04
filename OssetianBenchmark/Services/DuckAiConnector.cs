namespace OssetianBenchmark.Services;

using Microsoft.Playwright;

public class DuckAiConnector : PlaywrightConnectorBase
{
    public override string Name => "Luna";
    private const string ChatUrl = "https://duck.ai";

    public DuckAiConnector(BrowserPool pool) : base(pool) { }

    protected override BrowserNewContextOptions GetContextOptions()
    {
        return new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        };
    }

    protected override async Task NavigateToChatAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Открываю {ChatUrl}...");
        await page.GotoAsync(ChatUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 }).ConfigureAwait(false);
        Console.WriteLine($"  [{Name}] Жду 3с...");
        await page.WaitForTimeoutAsync(3000).ConfigureAwait(false);

        try
        {
            var btn = page.GetByRole(AriaRole.Button, new() { Name = "Продолжить" });
            if (await btn.CountAsync() > 0)
            {
                Console.WriteLine($"  [{Name}] Нажимаю «Продолжить»...");
                await btn.First.ClickAsync().ConfigureAwait(false);
                await page.WaitForTimeoutAsync(3000).ConfigureAwait(false);
                return;
            }
        }
        catch (PlaywrightException) { }

        try
        {
            var btn = page.GetByRole(AriaRole.Button, new() { Name = "Continue" });
            if (await btn.CountAsync() > 0)
            {
                Console.WriteLine($"  [{Name}] Нажимаю «Continue»...");
                await btn.First.ClickAsync().ConfigureAwait(false);
                await page.WaitForTimeoutAsync(3000).ConfigureAwait(false);
            }
        }
        catch (PlaywrightException) { }
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

        await page.WaitForTimeoutAsync(1500).ConfigureAwait(false);

        foreach (var name in new[] { "Продолжить", "Continue", "Далее" })
        {
            try
            {
                var btn = page.GetByRole(AriaRole.Button, new() { Name = name });
                if (await btn.CountAsync() > 0)
                {
                    Console.WriteLine($"  [{Name}] Нажимаю «{name}» после отправки...");
                    await btn.First.ClickAsync().ConfigureAwait(false);
        await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
                    break;
                }
            }
            catch (PlaywrightException) { }
        }
    }

    protected override async Task<string> WaitForResponseAsync(IPage page, CancellationToken ct)
    {
        Console.WriteLine($"  [{Name}] Ожидаю генерацию ответа...");
        var appeared = false;

        for (var i = 0; i < 12; i++)
        {
            await page.WaitForTimeoutAsync(1500).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            try
            {
                var stop = page.GetByRole(AriaRole.Button, new() { Name = "Stop generating" });
                var stopRu = page.GetByRole(AriaRole.Button, new() { Name = "Остановить" });
                var isGen = await stop.CountAsync() > 0 || await stopRu.CountAsync() > 0;

                if (isGen) { if (!appeared) Console.WriteLine($"  [{Name}] Генерация началась..."); appeared = true; continue; }
                if (appeared) { Console.WriteLine($"  [{Name}] Генерация завершена."); break; }
            }
            catch (PlaywrightException) { }

            if (i == 10 && !appeared)
                Console.WriteLine($"  [{Name}] Stop generating не появилась, пробую читать...");
        }

        await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);

        var found = await TryReadResponse(page).ConfigureAwait(false);
        if (found is not null) return found;

        Console.WriteLine($"  [{Name}] Селекторы не сработали, дамп HTML...");
        await DumpPageDebugWithSelectors(page, "no_selector_match").ConfigureAwait(false);

        var bodyRaw = await page.EvalOnSelectorAsync("body", "el => el.innerText").ConfigureAwait(false);
        return (bodyRaw?.ToString() ?? "").Trim();
    }

    private static async Task<string?> TryReadResponse(IPage page)
    {
        try
        {
            var text = await page.EvaluateAsync<string>(@"
                (() => {
                    const btns = document.querySelectorAll('[data-copyairesponse]');
                    if (btns.length === 0) return null;
                    const btn = btns[btns.length - 1];
                    // Walk up from the button to find the message container that
                    // includes a 'GPT-...' model label whose next sibling is the response.
                    let el = btn.parentElement;
                    while (el && el !== document.body) {
                        const children = Array.from(el.children || []);
                        // find the label child (starts with 'GPT-' or equals known model names) 
                        const labelIdx = children.findIndex(c => /^GPT[-–]/i.test((c.innerText||'').trim()) || /Masterpiece|Luna$/i.test((c.innerText||'').trim()));
                        if (labelIdx >= 0 && labelIdx + 1 < children.length) {
                            const resp = (children[labelIdx + 1].innerText || '').trim();
                            // response is the sibling right after the model label
                            if (resp.length > 0 && resp.length < 5000) {
                                return resp;
                            }
                            // fall back to the longest text child other than label & chips
                            let best = '';
                            for (const c of children) {
                                const t = (c.innerText || '').trim();
                                if (t.length > best.length && !/^GPT[-–]/i.test(t)) best = t;
                            }
                            return best.length > 1 ? best : null;
                        }
                        el = el.parentElement;
                    }
                    return null;
                })()
            ").ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        catch (PlaywrightException) { }

        return null;
    }
}
