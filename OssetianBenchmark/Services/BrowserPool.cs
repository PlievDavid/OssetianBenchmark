namespace OssetianBenchmark.Services;

using Microsoft.Playwright;

public sealed class BrowserPool : IAsyncDisposable
{
    private readonly List<IPlaywright> _playwrightInstances = new();
    private readonly List<IBrowser> _browsers = new();
    private readonly object _lock = new();

    public async Task<(IPlaywright pw, IBrowser browser)> AcquireAsync(CancellationToken ct = default)
    {
        var pw = await Playwright.CreateAsync().ConfigureAwait(false);

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = false,
            Channel = "msedge",
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
            },
        };

        var browser = await pw.Chromium.LaunchAsync(launchOptions).ConfigureAwait(false);

        lock (_lock)
        {
            _playwrightInstances.Add(pw);
            _browsers.Add(browser);
        }

        return (pw, browser);
    }

    public async ValueTask DisposeAsync()
    {
        List<IBrowser> browsers;
        List<IPlaywright> playwrights;
        lock (_lock)
        {
            browsers = new List<IBrowser>(_browsers);
            _browsers.Clear();
            playwrights = new List<IPlaywright>(_playwrightInstances);
            _playwrightInstances.Clear();
        }

        foreach (var b in browsers)
        {
            try { await b.CloseAsync().ConfigureAwait(false); } catch { }
        }
        foreach (var p in playwrights)
        {
            p.Dispose();
        }

        await ValueTask.CompletedTask;
    }
}
