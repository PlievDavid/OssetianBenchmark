namespace OssetianBenchmark.Services;

using OssetianBenchmark.Models;

public class TaskGenerator
{
    private const int SubDialogueMaxLines = 4;
    private const int TasksPerSubDialogue = 3;

    public List<BenchmarkTask> GenerateFromDialogues(List<RawDialogue> dialogues)
    {
        var tasks = new List<BenchmarkTask>();
        int taskNum = 1;

        foreach (var dialogue in dialogues)
        {
            var lines = dialogue.Lines
                .Select(l => l.Trim())
                .Where(l => l.Length >= 5)
                .ToList();

            if (lines.Count < 3) continue;

            foreach (var sub in SplitSubDialogues(lines))
            {
                foreach (var removeIdx in new[] { 1, 2, 3 })
                {
                    if (removeIdx >= sub.Count) continue;

                    var removedLine = sub[removeIdx];
                    var promptLines = new List<string>();

                    for (int i = 0; i < sub.Count; i++)
                    {
                        promptLines.Add(i == removeIdx
                            ? $"[{i + 1}] _________"
                            : $"[{i + 1}] {sub[i]}");
                    }

                    var task = new BenchmarkTask
                    {
                        Id = $"dlg_{taskNum:D3}",
                        Category = "Dialogue",
                        Type = "open_ended",
                        Prompt =
                            "Заполни пропущенную реплику в диалоге на осетинском языке (ирон æвзаг). " +
                            "Отвечай ТОЛЬКО одной репликой на осетинском — без перевода, без пояснений. " +
                            "Не ищи ответ в интернете.\n\n" +
                            string.Join("\n", promptLines),
                        ExpectedAnswer = removedLine,
                        RuleCheck = new RuleCheck { MustContain = new(), MustNotContain = new() },
                        Verified = false,
                    };

                    tasks.Add(task);
                    taskNum++;
                }
            }
        }

        return tasks;
    }

    private static List<List<string>> SplitSubDialogues(List<string> lines)
    {
        var result = new List<List<string>>();

        for (var start = 0; start < lines.Count; start += SubDialogueMaxLines)
        {
            var sub = lines.Skip(start).Take(SubDialogueMaxLines).ToList();
            if (sub.Count >= 3)
            {
                result.Add(sub);
            }
        }

        return result;
    }
}