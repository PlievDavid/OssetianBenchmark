# OssetianBenchmark

Консольный бенчмарк LLM на **осетинском языке** (ирон æвзаг). Модели тестируются через их
**веб-чаты** (Playwright + системный Edge, headed) — без API-ключей и без авторизации.
Судьей поочерёдно выступают сами же модели (перекрёстное судейство). Результат — Markdown-отчёт
и **Excel**-сводка.

Работающие без входа чаты (проверено):

| Модель | Сайт | Примечание |
|---|---|---|
| **Luna** | `duck.ai` | только headed (headless → капча) |
| **GigaChat** | `giga.chat` | принять cookies → ввод → ответ |
| **Alice** | `alice.yandex.ru` | при первом ходе может показывать «Думаю / Анализирую…» |

Список моделей задаётся в `appsettings.json` → `Models`.

## Требования

- **.NET 8 SDK** (`dotnet --version`)
- **Microsoft Edge** (системный) — `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`
  Chrome не требуется и не используется.

Зависимости NuGet: `Microsoft.Playwright`, `ClosedXML`, `Spectre.Console`. Установка браузеров
Playwright **не нужна** — используется системный Edge (`BrowserTypeLaunchOptions.Channel = "msedge"`).

## Запуск

```bash
cd OssetianBenchmark
dotnet run
```

Прогонятся все задачи из `data/tasks.json`: на каждую модель-кандидат отвечает, остальные модели
судят её ответ (судья ротируется: `judgeIdx = taskIdx % Models.Count`). После прогона создаются
`data/report.md` (Markdown) и `data/benchmark.xlsx` (Excel).

Полезные режимы:

| Команда | Что делает |
|---|---|
| `dotnet run` | полный бенчмарк без ограничений |
| `dotnet run -- --from 31 --count 30` | только задачи с локальной позиции 31 по 60 (1-based) |
| `dotnet run -- report` | пересобрать `report.md` + `benchmark.xlsx` из `results.json` без запуска браузеров |
| `dotnet run -- generate` | сгенерировать open-ended задачи из `data/raw_dialogues.json` в `tasks.json` |
| `dotnet run -- extract` | извлечь диалоги с **ironau.ru** в `data/raw_dialogues.json` |

Браузеры запускаются **headed** — не закрывайте окна Edge во время прогона. Весь прогон
сохраняет результаты **инкрементально** (upsert по `taskId` + `candidateModel`), перезапуск не
теряет данные.

## Конфигурация (`appsettings.json`)

```json
{
  "Benchmark": {
    "Models": ["Luna", "GigaChat", "Alice"],
    "TasksFile": "data/tasks.json",
    "ResultsFile": "data/results.json",
    "ReportFile": "data/report.md",
    "ExcelFile": "data/benchmark.xlsx",
    "Headless": false
  }
}
```

| Поле | Описание |
|---|---|
| `Models` | список моделей для перекрёстного судейства (порядок важен для ротации судьи) |
| `TasksFile` | файл задач |
| `ResultsFile` | сырые результаты (инкрементально) |
| `ReportFile` | Markdown-отчёт |
| `ExcelFile` | Excel-сводка |
| `Headless` | флаг headless-запуска (для duck.ai держать `false` — иначе капча) |

Поля `ApiKey` / `BaseUrl` / `TestedModel` / `JudgeModel` унаследованы от старого
API-концепта и в чат-режиме **не используются**. ⚠️ `ApiKey` в файле может содержать живой
ключ — никогда не коммитьте его; при утечке перевыпустите ключ у провайдера.

## Задачи

`data/tasks.json` содержит **60 задач**: 30 MC (Lexicon) + 30 open-ended (Dialogue, `verified: false`).

MC-задача:

```json
{
  "id": "lex_001",
  "category": "Lexicon",
  "type": "multiple_choice",
  "prompt": "Фразеологизм «бонтæ тонын» амоны:\n1) ...\n2) ...",
  "expectedAnswer": "4",
  "ruleCheck": { "mustContain": ["4"], "mustNotContain": [] }
}
```

OE-задача — «заполни пропущенную реплику»: в `prompt` дан диалог из 4 реплик (одна заменена на
`_________`), `expectedAnswer` — эталонная реплика. Источник — курируемые диалоги в
`data/raw_dialogues.json` (поля `source`, `section`, `lines`), генерацию делает `TaskGenerator`
(вариант A: поддиалог ≤ 4 реплик, удаляются строки 2–4, 3 задачи на поддиалог).

## Оценка

**MC:**

```
FinalScore = 0.4 * RuleScore + 0.6 * (JudgeTotal / 8.0)
```

- `RuleScore` — `RuleBasedEvaluator` по `ruleCheck` (regex, 0..1, штраф за `mustNotContain`).
- Судья — LLM-модель по 4 критериям 0–2: `GRAMMAR`, `LEXICON`, `MATCH`, `STYLE` (JSON).

**OE:**

```
FinalScore = 0.5 * AlgoScore + 0.5 * (JudgeTotal / 8.0)
AlgoScore   = 0.5 * (1 - LevenshteinNormalized) + 0.5 * JaccardWords
```

- `AlgoScore` — `Services/TextSimilarity.cs` (Levenshtein + Jaccard по словам) — независимая
  алгоритмическая мера близости к эталону.
- `RuleBasedEvaluator` для OE **не используется**; судье добавляется пометка «это диалог».

Во все промпты добавляется «Не ищи ответ в интернете». Перед каждым судейством чат судьи
пересоздаётся (`ResetChatAsync`), чтобы судья не смешивал кандидатов.

**Вердикты:** `PASS` ≥ 0.75, `PARTIAL` 0.5–0.75, `FAIL` < 0.5.

## Excel-отчёт

`benchmark.xlsx`:

- лист на каждого судью — **«{судья} (судья)»**: строки = кандидаты (без самого судьи),
  столбцы = категории + «ВСЕГО», значения = средний итоговый балл с цветовым градиентом
  (красный → зелёный);
- лист **«Сырые данные»** — все записи из `results.json`.

## Структура проекта

```
OssetianBenchmark/
├── OssetianBenchmark.csproj   # net8.0 Exe; Microsoft.Playwright, ClosedXML, Spectre.Console
├── Program.cs                 # оркестрация: режимы (run / report / generate / extract)
├── appsettings.json           # секция "Benchmark", список Models
├── Models/
│   ├── BenchmarkConfig.cs     # конфиг
│   ├── BenchmarkTask.cs       # задача (id, category, type, prompt, expectedAnswer, ruleCheck)
│   ├── BenchmarkResult.cs     # результат (ruleScore, algoScore, judgeScores, finalScore, ...)
│   ├── JudgeScore.cs          # grammar/lexicon/match/style/total + reasoning
│   ├── RuleCheck.cs           # mustContain / mustNotContain
│   └── RawDialogue.cs         # сырой диалог для генератора
├── Services/
│   ├── IChatConnector.cs      # абстракция чат-коннектора (CompleteAsync/ResetChat)
│   ├── PlaywrightConnectorBase.cs
│   ├── BrowserPool.cs         # переиспользование браузеров/контекстов
│   ├── DuckAiConnector.cs     # Luna (duck.ai)
│   ├── GigaChatConnector.cs   # GigaChat (giga.chat)
│   ├── AliceConnector.cs      # Alice (alice.yandex.ru), чистка от «Думаю/Анализирую…»
│   ├── BenchmarkRunner.cs     # перекрёстное судейство, ResetChat, прогресс-бар
│   ├── ChatJudgeEvaluator.cs  # судья-чат: промпт + парсинг JSON
│   ├── RuleBasedEvaluator.cs  # MC-проверка по ruleCheck
│   ├── TaskLoader.cs / TaskGenerator.cs / DialogueExtractor.cs
│   ├── TextSimilarity.cs      # Levenshtein + Jaccard → AlgoScore
│   └── ResultsStorage.cs      # инкрементальное сохранение в results.json
└── Reports/
    ├── ReportGenerator.cs     # Markdown: сводки, категории, 5 худших
    └── ExcelExporter.cs       # листы-судьи + «Сырые данные»
```

## Известные ограничения

- **Капчи и изменение вёрстки** веб-чатов: селекторы локализованы в каждом коннекторе;
  при изменении сайта правьте только соответствующий коннектор.
- **Luna (duck.ai)** отвечает только в headed-режиме; может появляться first-run-диалог
  («Continue»/«Продолжить»), который коннектор закрывает автоматически.
- **Alice** при первом ходе иногда сначала «думает» («Алиса / Думаю / Анализирую детали запроса…»);
  коннектор ждёт стабилизации, очищает reasoning и возвращает только реплику ответа.
- Ожидание ответа — по признакам завершения генерации (с кнопки «Stop generating» у Luna,
  стабилизация длины у GigaChat/Alice), таймаут — верхняя страховка.

## Результаты (последний запуск)

Сводные цифры актуального прогона 60 задач см. в `data/report.md` и `data/benchmark.xlsx`,
которые генерируются прямо в `bin\Debug\net8.0\data\`.
Результаты последнего прогона: https://drive.google.com/drive/folders/1F_hCFd-m8At9hA6OwCPWyei0dlK10S1T?usp=drive_link
