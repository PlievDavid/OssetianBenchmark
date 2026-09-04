namespace OssetianBenchmark.Services;

using System.Text.RegularExpressions;

public static class TextSimilarity
{
    public static double Levenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    public static double LevenshteinNormalized(string a, string b)
    {
        var dist = Levenshtein(a, b);
        var max = Math.Max(a.Length, b.Length);
        return max == 0 ? 1.0 : 1.0 - dist / (double)max;
    }

    public static double JaccardWords(string a, string b)
    {
        var setA = TokenizeWords(a);
        var setB = TokenizeWords(b);

        if (setA.Count == 0 && setB.Count == 0) return 1.0;
        if (setA.Count == 0 || setB.Count == 0) return 0.0;

        var intersection = setA.Count(w => setB.Contains(w));
        var union = setA.Count + setB.Count - intersection;
        return union == 0 ? 1.0 : (double)intersection / union;
    }

    public static double AlgoScore(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return 0.0;
        var lev = LevenshteinNormalized(expected, actual);
        var jac = JaccardWords(expected, actual);
        return 0.5 * lev + 0.5 * jac;
    }

    private static List<string> TokenizeWords(string text)
    {
        var words = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{M}]+")
            .Select(m => m.Value)
            .Where(w => w.Length > 0)
            .ToList();
        return words;
    }
}