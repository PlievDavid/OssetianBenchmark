namespace OssetianBenchmark.Reports;

using System.Text;
using OssetianBenchmark.Models;

public class ReportGenerator
{
    public string Generate(IReadOnlyList<BenchmarkResult> results)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Отчёт по бенчмарку осетинского языка");
        sb.AppendLine();
        sb.AppendLine($"Сгенерировано: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Всего задач: {results.Count}");
        sb.AppendLine();
        sb.AppendLine($"Средний итоговый score: **{results.Average(r => r.FinalScore):P1}**");
        sb.AppendLine();
        sb.AppendLine("## Сводка по вердиктам");
        sb.AppendLine();
        var passed = results.Count(r => r.Verdict() == "PASS");
        var partial = results.Count(r => r.Verdict() == "PARTIAL");
        var failed = results.Count(r => r.Verdict() == "FAIL");
        sb.AppendLine($"- ✅ Пройдено (PASS >= 0.75): {passed}");
        sb.AppendLine($"- ⚠️ Частично (PARTIAL 0.5-0.75): {partial}");
        sb.AppendLine($"- ❌ Провалено (FAIL < 0.5): {failed}");
        sb.AppendLine();

        sb.AppendLine("## Оценки по категориям");
        sb.AppendLine();
        sb.AppendLine("| Категория | Задач | Rule | Judge | Итог |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var group in results.GroupBy(r => r.Category).OrderBy(g => g.Key))
        {
            var ruleAvg = group.Average(r => r.RuleScore);
            var judgeAvg = group.Where(r => r.JudgeScore is not null).Select(r => r.JudgeScore!.Normalized).DefaultIfEmpty(0).Average();
            var finalAvg = group.Average(r => r.FinalScore);
            sb.AppendLine($"| {group.Key} | {group.Count()} | {ruleAvg:P1} | {judgeAvg:P1} | **{finalAvg:P1}** |");
        }
        sb.AppendLine();

        sb.AppendLine("## Сравнение rule-based vs judge");
        sb.AppendLine();
        sb.AppendLine("| Задача | Category | Rule | Judge | Итог |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in results.OrderBy(r => r.TaskId))
        {
            var judge = r.JudgeScore is null ? "—" : r.JudgeScore.Total.ToString();
            sb.AppendLine($"| {r.TaskId} | {r.Category} | {r.RuleScore:P0} | {judge}/8 | **{r.FinalScore:P1}** |");
        }
        sb.AppendLine();

        sb.AppendLine("## 5 худших примеров");
        sb.AppendLine();
        foreach (var r in results.OrderBy(r => r.FinalScore).Take(5))
        {
            sb.AppendLine($"### {r.TaskId} ({r.Category}) — {r.FinalScore:P1} [{r.Verdict()}]");
            sb.AppendLine();
            sb.AppendLine($"**Задача:** {r.Prompt}");
            sb.AppendLine();
            sb.AppendLine($"**Эталон:** {r.ExpectedAnswer}");
            sb.AppendLine();
            sb.AppendLine($"**Ответ модели:** {(string.IsNullOrEmpty(r.ModelResponse) ? "_нет ответа_ (ошибка)_" : r.ModelResponse)}");
            sb.AppendLine();
            sb.AppendLine($"**Rule score:** {r.RuleScore:P1} | **Judge:** {(r.JudgeScore is null ? "—" : r.JudgeScore.Total + "/8")}");
            if (r.JudgeScore is not null && !string.IsNullOrEmpty(r.JudgeScore.Reasoning))
            {
                sb.AppendLine();
                sb.AppendLine($"**Разбор судьи:** {r.JudgeScore.Reasoning}");
            }
            if (!string.IsNullOrEmpty(r.Error))
            {
                sb.AppendLine();
                sb.AppendLine($"**Ошибка:** {r.Error}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
