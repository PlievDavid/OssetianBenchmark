namespace OssetianBenchmark.Services;

using System.Text;

public class ProgressBar
{
    public string Render(int done, int total)
    {
        const int width = 40;
        var pct = total == 0 ? 0.0 : (double)done / total;
        var filled = (int)Math.Floor(pct * width);
        var bar = new string('#', filled) + new string('-', width - filled);
        var spinner = new[] { '|', '/', '-', '\\' }[done % 4];
        return $"{spinner} [{bar}] {done}/{total} ({pct:P0})";
    }
}
