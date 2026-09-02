namespace OssetianBenchmark.Services;

using System.Text.RegularExpressions;
using OssetianBenchmark.Models;

public class RuleBasedEvaluator
{
    public double Evaluate(BenchmarkTask task, string modelResponse)
    {
        var response = modelResponse.Trim().ToLowerInvariant();
        var rules = task.RuleCheck;
        var totalChecks = 0;
        var passedChecks = 0;

        foreach (var pattern in rules.MustContain)
        {
            totalChecks++;
            if (Regex.IsMatch(response, Regex.Escape(pattern.ToLowerInvariant()), RegexOptions.CultureInvariant))
            {
                passedChecks++;
            }
        }

        bool hasForbidden = false;
        foreach (var pattern in rules.MustNotContain)
        {
            totalChecks++;
            if (Regex.IsMatch(response, Regex.Escape(pattern.ToLowerInvariant()), RegexOptions.CultureInvariant))
            {
                hasForbidden = true;
            }
            else
            {
                passedChecks++;
            }
        }

        if (totalChecks == 0)
        {
            return 0.0;
        }

        var score = passedChecks / (double)totalChecks;

        if (hasForbidden)
        {
            score *= 0.5;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }
}
