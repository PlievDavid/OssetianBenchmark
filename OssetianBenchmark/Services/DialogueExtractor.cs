namespace OssetianBenchmark.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OssetianBenchmark.Models;

public class DialogueExtractor : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    private static readonly string[] PageUrls =
    {
        "https://ironau.ru/takazov/texts_iron2.htm",
        "https://ironau.ru/takazov/phrasebook2.htm",
        "https://ironau.ru/takazov/lesson_26.htm",
        "https://ironau.ru/takazov/lesson_27.htm",
        "https://ironau.ru/takazov/lesson_28.htm",
        "https://ironau.ru/takazov/lesson_29.htm",
        "https://ironau.ru/takazov/lesson_30.htm",
        "https://ironau.ru/takazov/lesson_31.htm",
        "https://ironau.ru/takazov/lesson_32.htm",
        "https://ironau.ru/takazov/lesson_33.htm",
        "https://ironau.ru/takazov/lesson_34.htm",
        "https://ironau.ru/takazov/lesson_35.htm",
        "https://ironau.ru/takazov/lesson_36.htm",
        "https://ironau.ru/takazov/lesson_37.htm",
        "https://ironau.ru/takazov/lesson_38.htm",
        "https://ironau.ru/takazov/lesson_39.htm",
        "https://ironau.ru/takazov/lesson_40.htm",
        "https://ironau.ru/takazov/lesson_41.htm",
        "https://ironau.ru/takazov/lesson_42.htm",
    };

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "msedge",
            Args = new[] { "--disable-blink-features=AutomationControlled" },
        }).ConfigureAwait(false);
    }

    public async Task<List<RawDialogue>> ExtractAllAsync(CancellationToken ct = default)
    {
        if (_browser is null)
            throw new InvalidOperationException("Not initialized. Call InitializeAsync first.");

        var allDialogues = new List<RawDialogue>();

        foreach (var url in PageUrls)
        {
            Console.WriteLine($"  [extract] {url}...");
            try
            {
                var dialogues = await ExtractFromPageAsync(url, ct).ConfigureAwait(false);
                Console.WriteLine($"  [extract]   → {dialogues.Count} dialogues");
                allDialogues.AddRange(dialogues);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [extract]   !! Ошибка: {ex.Message}");
            }

            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        return allDialogues;
    }

    private async Task<List<RawDialogue>> ExtractFromPageAsync(string url, CancellationToken ct)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        try
        {
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 }).ConfigureAwait(false);
            await page.WaitForTimeoutAsync(1000).ConfigureAwait(false);

            var extracted = await page.EvaluateAsync<JsonElement>(@"
                () => {
                    const results = [];
                    const body = document.body;
                    if (!body) return results;

                    const allP = body.querySelectorAll('p');
                    let dialogueBuffer = [];
                    let lastWasDialogue = false;

                    for (const p of allP) {
                        // Skip footnotes
                        if (p.closest('div[style*=""mso-element:footnote""]')) continue;

                        const text = p.textContent.trim().replace(/\s+/g, ' ');
                        if (text.length < 2) {
                            // Empty line — might separate dialogues
                            if (dialogueBuffer.length >= 2) {
                                results.push({ lines: [...dialogueBuffer] });
                            }
                            dialogueBuffer = [];
                            lastWasDialogue = false;
                            continue;
                        }

                        // Check if starts with em-dash or dash
                        const isDialogue = text.startsWith('\u2013 ') ||
                                           text.startsWith('\u2014 ') ||
                                           text.startsWith('- ') ||
                                           text.startsWith('\u2011 ');

                        if (isDialogue) {
                            let line = text;
                            if (line.startsWith('\u2013 ')) line = line.substring(2);
                            else if (line.startsWith('\u2014 ')) line = line.substring(2);
                            else if (line.startsWith('- ')) line = line.substring(2);
                            else if (line.startsWith('\u2011 ')) line = line.substring(2);
                            line = line.trim();
                            if (line.length > 0 && line.length < 500) {
                                dialogueBuffer.push(line);
                            }
                            lastWasDialogue = true;
                        } else {
                            // Non-dialogue line
                            if (lastWasDialogue && dialogueBuffer.length >= 2) {
                                // End of dialogue block
                                results.push({ lines: [...dialogueBuffer] });
                            }
                            dialogueBuffer = [];
                            lastWasDialogue = false;
                        }
                    }

                    // Flush remaining
                    if (dialogueBuffer.length >= 2) {
                        results.push({ lines: [...dialogueBuffer] });
                    }

                    return results;
                }
            ").ConfigureAwait(false);

            var dialogues = new List<RawDialogue>();
            foreach (var item in extracted.EnumerateArray())
            {
                var lines = new List<string>();
                foreach (var line in item.GetProperty("lines").EnumerateArray())
                {
                    var text = (line.GetString() ?? "").Trim();
                    if (text.Length > 0)
                        lines.Add(text);
                }

                if (lines.Count < 2) continue;

                // Filter out non-dialogue content
                var allText = string.Join(" ", lines);

                // Skip Russian-Ossetian translation pairs (lesson vocabulary)
                if (allText.Contains("(в смысле:") || allText.Contains("букв.") ||
                    allText.Contains("«всё") || allText.Contains("всё время"))
                    continue;

                // Skip lesson examples with parenthesized Ossetian translations
                var parenCount = 0;
                foreach (var c in allText)
                    if (c == '(') parenCount++;
                if (parenCount >= lines.Count / 2)
                    continue;

                // Skip lines that are just numbered examples
                if (lines.All(l => Regex.IsMatch(l, @"^\d+[\.\)]\s")))
                    continue;

                dialogues.Add(new RawDialogue
                {
                    Source = url,
                    Lines = lines,
                });
            }

            return dialogues;
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    public static void SaveToJson(List<RawDialogue> dialogues, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = JsonSerializer.Serialize(dialogues, options);
        File.WriteAllText(filePath, json);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _browser = null;
        }
        if (_playwright is not null)
        {
            _playwright.Dispose();
            _playwright = null;
        }
    }
}
