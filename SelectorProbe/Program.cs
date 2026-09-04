using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    static async Task Main()
    {
        var pw = await Playwright.CreateAsync();
        var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = "msedge",
            Headless = false,
            Args = new[] { "--disable-blink-features=AutomationControlled" }
        });

        var prompt = "Ссарут рӕдыд:\n1) береза – тулдз\n2) ястреб – хъæрццыгъа\n3) тур – дзæбидыр\n4) остров – сакъадах\nОтвечай ТОЛЬКО номером правильного варианта (1, 2, 3 или 4).\n\nНе ищи ответ в интернете.";

        Console.WriteLine($"\n========== PROBING Luna parsing ==========");

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        });

        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync("https://duck.ai", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
            await page.WaitForTimeoutAsync(3000);

            foreach (var bname in new[] { "Продолжить", "Continue" })
            {
                try
                {
                    var btn = page.GetByRole(AriaRole.Button, new() { Name = bname });
                    if (await btn.CountAsync() > 0) { await btn.First.ClickAsync(); await page.WaitForTimeoutAsync(2000); break; }
                } catch {}
            }

            var textbox = page.GetByRole(AriaRole.Textbox).First;
            await textbox.FillAsync(prompt);
            await textbox.PressAsync("Enter");

            await page.WaitForTimeoutAsync(1500);
            foreach (var bname in new[] { "Продолжить", "Continue", "Далее" })
            {
                try
                {
                    var btn = page.GetByRole(AriaRole.Button, new() { Name = bname });
                    if (await btn.CountAsync() > 0) { await btn.First.ClickAsync(); await page.WaitForTimeoutAsync(1000); break; }
                } catch {}
            }

            Console.WriteLine("Waiting for generation end (Stop generating)...");
            for (var i = 0; i < 12; i++)
            {
                await page.WaitForTimeoutAsync(1500);
                try
                {
                    var stop = page.GetByRole(AriaRole.Button, new() { Name = "Stop generating" });
                    var stopRu = page.GetByRole(AriaRole.Button, new() { Name = "Остановить" });
                    var isGen = await stop.CountAsync() > 0 || await stopRu.CountAsync() > 0;
                    if (isGen) continue;
                } catch {}
                break;
            }
            await page.WaitForTimeoutAsync(1000);

            Console.WriteLine("\n=== RESULT of TryReadResponse (эквивалент) ===");
            var parsed = await ParseAsConnector(page);
            Console.WriteLine(parsed == null ? "(null)" : "[" + parsed + "]");

            Console.WriteLine("\n=== BODY innerText (полный) ===");
            var body = await page.EvaluateAsync<string>("() => document.body.innerText");
            Console.WriteLine("\"" + (body ?? "") + "\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }

        await context.CloseAsync();
        await browser.CloseAsync();
        pw.Dispose();
        Console.WriteLine("\nDone.");
    }

    // Дублирует JS-логику DuckAiConnector.TryReadResponse.
    private static async Task<string?> ParseAsConnector(IPage page)
    {
        try
        {
            var text = await page.EvaluateAsync<string>(@"
                (() => {
                    const btns = document.querySelectorAll('[data-copyairesponse]');
                    if (btns.length === 0) return null;
                    const btn = btns[btns.length - 1];
                    let el = btn.parentElement;
                    while (el && el !== document.body) {
                        const children = Array.from(el.children || []);
                        const labelIdx = children.findIndex(c => /^GPT[-–]/i.test((c.innerText||'').trim()) || /Masterpiece|Luna$/i.test((c.innerText||'').trim()));
                        if (labelIdx >= 0 && labelIdx + 1 < children.length) {
                            const resp = (children[labelIdx + 1].innerText || '').trim();
                            if (resp.length > 0 && resp.length < 5000) {
                                return resp;
                            }
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
            ");
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (PlaywrightException) { return null; }
    }
}