namespace OssetianBenchmark.Reports;

using ClosedXML.Excel;
using OssetianBenchmark.Models;

public class ExcelExporter
{
    public void Export(IReadOnlyList<BenchmarkResult> results, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var workbook = new XLWorkbook();

        var allJudges = results
            .SelectMany(r => r.JudgeScores)
            .Select(j => j.JudgeModel)
            .Distinct()
            .OrderBy(j => j)
            .ToList();

        foreach (var judge in allJudges)
        {
            CreateJudgeSheet(workbook, results, judge);
        }

        CreateRawDataSheet(workbook, results);

        workbook.SaveAs(filePath);
    }

    private static void CreateJudgeSheet(XLWorkbook workbook, IReadOnlyList<BenchmarkResult> results, string judgeModel)
    {
        var ws = workbook.Worksheets.Add($"{judgeModel} (судья)");

        var judgeResults = results
            .Where(r => r.JudgeScores.Any(j => j.JudgeModel == judgeModel))
            .ToList();

        var categories = judgeResults
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var candidateModels = judgeResults
            .Select(r => r.CandidateModel)
            .Where(m => m != judgeModel)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        ws.Cell(1, 1).Value = "Модель \\ Категория";
        for (var i = 0; i < categories.Count; i++)
        {
            ws.Cell(1, 2 + i).Value = categories[i];
        }
        ws.Cell(1, 2 + categories.Count).Value = "ВСЕГО";

        var headerRange = ws.Range(1, 1, 1, 2 + categories.Count);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

        var data = new List<(string CandidateModel, string Category, double Score)>();

        foreach (var result in results)
        {
            var entry = result.JudgeScores.FirstOrDefault(j => j.JudgeModel == judgeModel);
            if (entry?.JudgeScore is null) continue;
            if (result.CandidateModel == judgeModel) continue;

            var ruleScore = result.RuleScore;
            var judgeTotal = entry.JudgeScore.Total;
            var judgeNormalized = judgeTotal / 8.0;
            var perJudgeFinal = result.Type == "open_ended"
                ? 0.5 * result.AlgoScore + 0.5 * judgeNormalized
                : 0.4 * result.RuleScore + 0.6 * judgeNormalized;

            data.Add((result.CandidateModel, result.Category, perJudgeFinal));
        }

        var row = 2;
        foreach (var candidate in candidateModels)
        {
            ws.Cell(row, 1).Value = candidate;
            ws.Cell(row, 1).Style.Font.Bold = true;

            var allScoresForCandidate = new List<double>();

            for (var i = 0; i < categories.Count; i++)
            {
                var catScores = data
                    .Where(d => d.CandidateModel == candidate && d.Category == categories[i])
                    .Select(d => d.Score)
                    .ToList();

                if (catScores.Count > 0)
                {
                    var avg = catScores.Average();
                    ws.Cell(row, 2 + i).Value = avg;
                    ws.Cell(row, 2 + i).Style.NumberFormat.Format = "0.0%";
                    ApplyColorGradient(ws.Cell(row, 2 + i), avg);
                    allScoresForCandidate.Add(avg);
                }
                else
                {
                    ws.Cell(row, 2 + i).Value = "—";
                    ws.Cell(row, 2 + i).Style.Font.FontColor = XLColor.Gray;
                }
            }

            if (allScoresForCandidate.Count > 0)
            {
                var overall = allScoresForCandidate.Average();
                ws.Cell(row, 2 + categories.Count).Value = overall;
                ws.Cell(row, 2 + categories.Count).Style.NumberFormat.Format = "0.0%";
                ws.Cell(row, 2 + categories.Count).Style.Font.Bold = true;
                ApplyColorGradient(ws.Cell(row, 2 + categories.Count), overall);
            }

            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void ApplyColorGradient(IXLCell cell, double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        var r = (int)(255 * (1.0 - clamped));
        var g = (int)(255 * clamped);
        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, r, g, 60);
    }

    private static void CreateRawDataSheet(XLWorkbook workbook, IReadOnlyList<BenchmarkResult> results)
    {
        var ws = workbook.Worksheets.Add("Сырые данные");

        ws.Cell(1, 1).Value = "TaskId";
        ws.Cell(1, 2).Value = "Категория";
        ws.Cell(1, 3).Value = "Промпт";
        ws.Cell(1, 4).Value = "Эталон";
        ws.Cell(1, 5).Value = "Кандидат";
        ws.Cell(1, 6).Value = "Ответ модели";
        ws.Cell(1, 7).Value = "Rule Score";
        ws.Cell(1, 8).Value = "Avg Judge";
        ws.Cell(1, 9).Value = "Final Score";
        ws.Cell(1, 10).Value = "Вердикт";
        ws.Cell(1, 11).Value = "Ошибка";
        ws.Cell(1, 12).Value = "Дата";

        var headerRange = ws.Range(1, 1, 1, 12);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightCyan;

        var row = 2;
        foreach (var r in results.OrderBy(r => r.TaskId).ThenBy(r => r.CandidateModel))
        {
            ws.Cell(row, 1).Value = r.TaskId;
            ws.Cell(row, 2).Value = r.Category;
            ws.Cell(row, 3).Value = r.Prompt;
            ws.Cell(row, 4).Value = r.ExpectedAnswer;
            ws.Cell(row, 5).Value = r.CandidateModel;
            ws.Cell(row, 6).Value = r.ModelResponse;
            ws.Cell(row, 7).Value = r.RuleScore;
            ws.Cell(row, 8).Value = r.AverageJudgeScore;
            ws.Cell(row, 9).Value = r.FinalScore;

            var verdict = r.Verdict();
            ws.Cell(row, 10).Value = verdict;
            ws.Cell(row, 10).Style.Font.FontColor = verdict switch
            {
                "PASS" => XLColor.Green,
                "PARTIAL" => XLColor.Orange,
                _ => XLColor.Red,
            };

            ws.Cell(row, 11).Value = r.Error ?? "";
            ws.Cell(row, 12).Value = r.CompletedAtUtc.ToString("yyyy-MM-dd HH:mm:ss");

            row++;
        }

        ws.Columns().AdjustToContents();
    }
}
