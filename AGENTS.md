# AGENTS.md

Контекст на всю рабочую сессию: проект **OssetianBenchmark** — консольный бенчмарк
LLM на осетинском языке (ирон æвзаг) в `C:\Users\ADMIN\Documents\BenchMark`.

> ⚠️ **ЧИТАЙ СНАЧАЛА** — конец документа содержит «ПЛАН ПЕРЕХОДА / СЛЕДУЮЩИЕ ШАГИ»:
> новый концепт (Playwright-чаты) ещё НЕ реализован, есть детальный план работ.

---

## Окружение
- **ОС:** Windows, shell — **PowerShell 5.1** (win32).
- **`.NET SDK 9.0.306`** установлен; проекты целятся в `net8.0`.
- **Microsoft Edge** установлен: `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`.
  Chrome НЕ установлен.
- **Playwright.NET** (Microsoft.Playwright 1.40.0) работает через **системный Edge**:
  `BrowserTypeLaunchOptions.Channel = "msedge"` — **не нужен** `dotnet playwright install` браузеров.

## Команды
- Сборка/запуск: `dotnet build`, `dotnet run` (workdir = папка проекта `OssetianBenchmark`).
- Прогон задач из `data/tasks.json`; результаты/отчёт пишутся в `bin\Debug\net8.0\data\`.
- Команды сборки выполняются в PowerShell; не используй `&&` (не поддерживается) — используй `; if ($?) { ... }`.

## Структура проекта (текущая, концепт «OpenAI-совместимый API»)
```
OssetianBenchmark/
├── OssetianBenchmark.csproj   # net8.0 Exe; пакеты: Microsoft.Extensions.Configuration(.Json/.Binder), OpenAI, Spectre.Console
├── Program.cs                 # оркестрация: конфиг, проверка ключа, запуск runner, генерация отчёта
├── appsettings.json           # секция "Benchmark" — см. ниже ⚠️ содержит живой API-ключ
├── Models/
│   ├── BenchmarkConfig.cs     # конфиг (ApiKey, BaseUrl, TestedModel, JudgeModel, Temperature, MaxConcurrency, MaxRetries, TasksFile, ResultsFile, ReportFile)
│   ├── BenchmarkTask.cs       # id, category, type, prompt, expectedAnswer, ruleCheck(члены), verified
│   ├── BenchmarkResult.cs     # taskId, category, ..., ruleScore, judgeScore, finalScore, verified, completedAtUtc, error; Verdict(): PASS/PARTIAL/FAIL
│   ├── JudgeScore.cs          # grammar, lexicon, match, style, total, reasoning; Normalized=>Total/8
│   └── RuleCheck.cs           # mustContain[], mustNotContain[]
├── Services/
│   ├── LlmClient.cs           # официальный OpenAI SDK; OpenAIClient(Endpoint=BaseUrl); retry w/ exp backoff (IsRetryable: crEx 429/5xx, HttpRequestException, TaskCanceledException)
│   ├── TaskLoader.cs          # читает tasks.json в List<BenchmarkTask>
│   ├── RuleBasedEvaluator.cs  # ruleCheck через regex по ответу (0..1, штраф за mustNotContain)
│   ├── LlmJudgeEvaluator.cs   # судья через CompleteAsync(judgeModel), парсинг JSON судьи (grammar/lexicon/match/style/total/reasoning)
│   ├── BenchmarkRunner.cs     # Parallel.ForEachAsync; AnsiConsole.Live PROGRESS-BAR; инкрементальное сохранение; per-task try/catch (не падает)
│   ├── ResultsStorage.cs      # json, инкрементально, по taskId upsert
│   └── ProgressBar.cs         # простой счётчик
└── Reports/
    └── ReportGenerator.cs     # markdown: категории, rule vs judge, 5 худших, вердикты

Данные: data/tasks.json (20 задач, по 4 на категорию: Ergativity, Postpositions, AntiCalque, Lexicon, Orthography;
часть помечена verified:false).
```

## Формула и судейство
- `FinalScore = 0.4 * RuleScore + 0.6 * (JudgeTotal / 8.0)`.
- Судья оценивает по 4 критериям 0–2: GRAMMAR, LEXICON, MATCH, STYLE (возвращает JSON).
- Вердикты: PASS≥0.75, PARTIAL 0.5–0.75, FAIL<0.5.

## ⚠️ Секреты в репозитории
`appsettings.json` содержит **живой API-ключ Groq**
(`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`) и ранее засвечивался ключ OpenRouter
(`sk-or-v1-...`). Пользователю рекомендовано перевыпустить ключи, т.к. они были в чате.
**Не логируй и не коммить ключи в новых местах.**

---

# ПЛАН ПЕРЕХОДА / НОВЫЙ КОНЦЕПТ (важно)

Решение пользователя: **сменить концепт** — тестировать СРАЗУ НЕСКОЛЬКО моделей через
их **веб-чаты** (не по API), каждая модель поочерёдно выступает судьёй, результат —
**Excel** (сводка судья×модель по типам заданий и категориям + сырые данные).
Транспорт — **только Playwright-чаты, без авторизации**. NuGet ставить разрешено.

## Разведка (выполнено, факты для следующих шагов)
Зонд-проект: `C:\Users\ADMIN\AppData\Local\Temp\opencode\pwprobe\PwProbe.csproj`
(`Microsoft.Playwright` 1.40, системный Edge). Результаты разведки анонимного доступа:

| Модель/сайт | Без входа | Статус / поток |
|---|---|---|
| **Luna — duck.ai** | ✅ | Работает. **Только headed** (headless→капча). Поток: ввод→Enter→клик «Continue»(first-run)→ждать исчез(it «Stop generating»)→читать блок после подписи «GPT-5.6 Luna». |
| **GigaChat — giga.chat** | ✅ | Работает. Поток: принять «Cookies»→ввод→Enter→ждать~20с→читать из блока «Ответ». |
| **Perplexity — perplexity.ai** | ✅ | Работает. Поток: ввод→Enter→ждать→читать ответ после вопроса. |
| **Gemini — gemini.google.com** | ❌ | Ошибка **1090** (аноним не отвечает). |
| **ИИ-поиск Google** | ❌ | Капча «unusual traffic» (`/sorry/index`). |
| Cohere (cohere.com) | ❌ | Только маркетинг, чата нет. |
| You.com | ❌ | Редирект на /signin. |
| Mistral Le Chat, Meta.ai | ⚠️ | Открываются, но поле/ответ ненадёжно (таймаут/textbox 0) — нужна докопка при желании добавить. |

**Решение:** система на **3 моделях: Luna, GigaChat, Perplexity** (все подтвержденно работают без входа).

## Договорённости по судейству
- Каждая модель судит **всех ОСТАЛЬНЫХ** (себя — НЕ судит). На 3 моделях = на задачу:
  3 кандидата-ответа × 2 судьи = 6 оценок.
- Судья-промпт: 4 критерия (GRAMMAR/LEXICON/MATCH/STYLE), возвращает JSON.

## Технологические решения на следующий этап
- Добавить пакет `Microsoft.Playwright` в основной проект + `ClosedXML` (Excel без установленного Excel).
- Браузер: системный Edge, **headed**, `Args=["--disable-blink-features=AutomationControlled"]`,
  для duck.ai кастомный UserAgent (см.зонд: Chrome UA win32).
- Архитектура: абстракция `IChatConnector { string Name; Task<string> CompleteAsync(prompt, systemPrompt, ct) }`.
  Коннекторы: `DuckAiConnector`, `GigaChatConnector`, `PerplexityConnector` (+ `BrowserPool`, база `PlaywrightConnectorBase`).
  Коннектор умеет и отвечать (кандидат), и судить (та же модель с судейским промптом).
- `BenchmarkRunner` переработать под перекрёст; `LlmJudgeEvaluator` заменить на чат-коннектор как судью
  (JSON-парсинг логики судьи переиспользовать).
- Excel: `Reports/ExcelExporter.cs` (**реализован**): на каждого судью — отдельный лист «{судья} (судья)»,
  строки=кандидаты (без самого судьи), столбцы=категории + «ВСЕГО», значения=средний итоговый балл
  цветовым градиентом (красный→зелёный); + лист «Сырые данные».
- Важно про судью: при передаче судье **второго** ответа кандидата **пересоздавать чат** (`ResetChatAsync`) —
  иначе судья иногда не успевает проверить второй ответ.
- `appsettings.json` расширить списком моделей (напр. `"Models": ["Luna","GigaChat","Perplexity"]`).
- Переиспользовать: задачи, RuleBasedEvaluator, JudgeScore, ParseScore (логика судьи), ResultsStorage.

## Риски → митигация
- Капча/изменение вёрстки чатов → headed, одна точка селекторов в коннекторе, ретраи, повторные прогоны.
- Долгие ожидания ответа → разумные таймауты + пул браузеров.
- Токенный бюджет: реализация итеративная (~75–100k+ токенов суммарно) — делать **поэтапно**,
  начиная с минимального ядра (Luna + перекрёст + Excel), затем добавить GigaChat и Perplexity.

## Следующие шаги (порядок)
1. ✅ Добавить в основной csproj пакеты `Microsoft.Playwright` и `ClosedXML`.
2. ✅ Создать `IChatConnector`, `PlaywrightConnectorBase`, `BrowserPool`.
3. ✅ Реализовать `DuckAiConnector` (Luna) и довести до рабочего ответа (поток из разведки).
4. ✅ Переработать `BenchmarkRunner` под перекрёст (кандидат отвечает, чужие судят).
5. ✅ `ExcelExporter` (сводка + сырые данные) + интеграция в `Program.cs`.
6. ✅ Добавить `GigaChatConnector` и `PerplexityConnector`.
7. ✅ E2E-прогон на 3 моделях, отладка селекторов.

## Текущее состояние (реализовано)
- Коннекторы: Luna (duck.ai), GigaChat (giga.chat), Alice (alice алисры) — все работают.
- `BenchmarkRunner`: ротация судьи (судья = taskIdx % N), чужие судят кандидата,
  `ResetChatAsync` между задачами и перед вторым кандидатом у судьи.
- 30 tasks MC (Lexicon), прогон завершён. Затем добавлены 37 OE-задач (диалоги), прогон 67 задач.
- Excel: листы-судьи (Luna/GigaChat/Alice) + «Сырые данные».

## Следующий этап (open-ended, диалоги) — ✅ РЕАЛИЗОВАНО
1. ✅ `Services/TextSimilarity.cs` — Levenshtein + Jaccard (AlgoScore).
2. ✅ `data/raw_dialogues.json` — 3 курируемых диалога от пользователя (Алан/Ирбег 18, Къола/Бибо 20, Ситохы-фырт 5).
3. ✅ `Services/TaskGenerator.cs` — вариант A (без перекрытия), поддиалог ≤4 реплик,
   удаляются строки 2–4 (первая не удаляется), 3 задачи на поддиалог.
4. ✅ Формула OE: `FinalScore = 0.5 * AlgoScore + 0.5 * (JudgeTotal / 8.0)`,
   `AlgoScore = 0.5 * (1 - Levenshtein_norm) + 0.5 * Jaccard_words`.
5. ✅ OE: RuleBasedEvaluator НЕ используется (только MC); `AlgoScore` в `BenchmarkResult`.
6. ✅ Всепромпты OE: «Не ищи ответ в интернете» (добавляется в Runner при отправке).
7. ✅ Перегенерирован `data/tasks.json`: 30 MC + 30 OE = 60 задач.
8. ⏳ Прогнать бенчмарк, обновить README.

## Заметки по реализации OE
- `ResetChatAsync` судьи вынесен внутрь цикла кандидатов (перед КАЖДЫМ судьёй), не только перед вторым.
- `ChatJudgeEvaluator.ScoreAsync` получил флаг `isOpenEnded` (добавляет в промпт «это диалог»).
- Excel: `perJudgeFinal` для OE считается как `0.5*AlgoScore + 0.5*judge`.
- Итоговый прогон ещё не делался после перегенерации (60 задач, диалоги из курируемых данных).
