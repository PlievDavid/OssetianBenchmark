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
        sb.AppendLine($"Всего записей: {results.Count}");

        var models = results.Select(r => r.CandidateModel).Distinct().OrderBy(m => m).ToList();
        sb.AppendLine($"Модели: {string.Join(", ", models)}");
        sb.AppendLine();
        sb.AppendLine($"Средний итоговый score: **{results.Average(r => r.FinalScore):P1}**");
        sb.AppendLine();

        sb.AppendLine("## Сводка по моделям");
        sb.AppendLine();
        sb.AppendLine("| Модель | Задач | Rule | Judge | Итог |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var model in models)
        {
            var modelResults = results.Where(r => r.CandidateModel == model).ToList();
            var ruleAvg = modelResults.Average(r => r.RuleScore);
            var judgeAvg = modelResults.Average(r => r.AverageJudgeScore);
            var finalAvg = modelResults.Average(r => r.FinalScore);
            sb.AppendLine($"| {model} | {modelResults.Count} | {ruleAvg:P1} | {judgeAvg:P1} | **{finalAvg:P1}** |");
        }
        sb.AppendLine();

        sb.AppendLine("## Оценки по категориям × моделям");
        sb.AppendLine();
        sb.AppendLine("| Категория | Модель | Задач | Rule | Judge | Итог |");
        sb.AppendLine("|---|---|---|---|---|---|");
        var categories = results.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
        foreach (var category in categories)
        {
            foreach (var model in models)
            {
                var group = results.Where(r => r.Category == category && r.CandidateModel == model).ToList();
                if (group.Count == 0) continue;
                var ruleAvg = group.Average(r => r.RuleScore);
                var judgeAvg = group.Average(r => r.AverageJudgeScore);
                var finalAvg = group.Average(r => r.FinalScore);
                sb.AppendLine($"| {category} | {model} | {group.Count} | {ruleAvg:P1} | {judgeAvg:P1} | **{finalAvg:P1}** |");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## 5 худших примеров");
        sb.AppendLine();
        foreach (var r in results.OrderBy(r => r.FinalScore).Take(5))
        {
            sb.AppendLine($"### {r.TaskId} ({r.Category}) — {r.CandidateModel} — {r.FinalScore:P1} [{r.Verdict()}]");
            sb.AppendLine();
            sb.AppendLine($"**Задача:** {r.Prompt}");
            sb.AppendLine();
            sb.AppendLine($"**Эталон:** {r.ExpectedAnswer}");
            sb.AppendLine();
            sb.AppendLine($"**Ответ модели:** {(string.IsNullOrEmpty(r.ModelResponse) ? "_нет ответа_" : r.ModelResponse)}");
            sb.AppendLine();
            sb.AppendLine($"**Rule score:** {r.RuleScore:P1} | **Avg Judge:** {r.AverageJudgeScore:F2}");
            if (r.JudgeScores.Count > 0)
            {
                sb.AppendLine();
                foreach (var je in r.JudgeScores)
                {
                    sb.AppendLine($"- Судья {je.JudgeModel}: {je.JudgeScore?.Total}/8 — {je.JudgeScore?.Reasoning}");
                }
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
