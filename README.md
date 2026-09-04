# OssetianBenchmark

Консольный бенчмаркинг LLM на **осетинском языке** через любой OpenAI-совместимый API
(OpenRouter, Groq, Ollama, OpenAI, Gemini, Together и др.).

## Начало работы

### 1. Установите .NET 8 SDK

Со [страницы .NET](https://dotnet.microsoft.com/download) установите **.NET 8 SDK** и проверьте:

```bash
dotnet --version
```

### 2. Вставьте API-ключ

Откройте [`appsettings.json`](appsettings.json) и замените `ТВОЙ_API_КЛЮЧ` на реальный ключ:

```json
"ApiKey": "sk-or-v1-ваш_реальный_ключ"
```

> Получите ключ на [openrouter.ai/keys](https://openrouter.ai/keys) или у вашего провайдера.

### 3. Запустите

```bash
cd OssetianBenchmark
dotnet restore
dotnet run
```

После завершения отчёт появится в `data/report.md`, сырые результаты — в `data/results.json`.

## Как сменить провайдера

Меняйте только `BaseUrl` и `TestedModel` в `appsettings.json` — без перекомпиляции.

| Провайдер | BaseUrl | Пример модели |
|---|---|---|
| OpenRouter | `https://openrouter.ai/api/v1` | `qwen/qwen-2.5-7b-instruct:free` |
| Groq | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` |
| Ollama (локально) | `http://localhost:11434/v1` | `qwen2.5:7b` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| Gemini | `https://generativelanguage.googleapis.com/v1beta/openai/` | `gemini-2.0-flash` |
| Together | `https://api.together.xyz/v1` | `meta-llama/Llama-3.3-70B-Instruct-Turbo` |

Для Ollama локально ключ можно оставить любым (непустым), API его не проверяет.

## Конфигурация

| Поле | Описание |
|---|---|
| `ApiKey` | Ключ API провайдера |
| `BaseUrl` | Базовый URL OpenAI-совместимого API (до `/v1`) |
| `TestedModel` | Модель, которую тестируем |
| `JudgeModel` | Модель-судья (для 2-го уровня оценки) |
| `Temperature` | Должно быть `0` для воспроизводимости |
| `MaxConcurrency` | Сколько задач выполнять параллельно |
| `MaxRetries` | Retry при 429/5xx/таймаутах (exponential backoff) |
| `TasksFile` | Путь к файлу задач |
| `ResultsFile` | Путь к результатам (инкрементальное сохранение) |
| `ReportFile` | Путь к markdown-отчёту |

## Как добавить свои задачи

Задачи лежат в [`data/tasks.json`](data/tasks.json). Формат:

```json
{
  "id": "erg_001",
  "category": "Ergativity",
  "type": "multiple_choice",
  "prompt": "Кæцы хъуыдыйад раст у?\n1) Æз чиныг бакастæн.\n2) Мæнæй чиныг бакаст.",
  "expectedAnswer": "2",
  "ruleCheck": {
    "mustContain": ["2"],
    "mustNotContain": []
  }
}
```

Поля:

- `id` — уникальный код задачи
- `category` — категория (для группировки в отчёте)
- `type` — тип (`multiple_choice`, `free_form` и т.д.)
- `prompt` — текст задачи для модели
- `expectedAnswer` — эталонный ответ (для судьи)
- `ruleCheck.mustContain` — строки, которые должны быть в ответе
- `ruleCheck.mustNotContain` — строки, которых быть не должно
- `verified` (опц.) — `false`, если конструкция не проверена носителем

## Как оцениваются ответы (2 уровня)

1. **RuleBasedEvaluator** — по `ruleCheck` через regex, score 0.0–1.0.
2. **LlmJudgeEvaluator** — модель-судья оценивает по 4 критериям (0–2 каждый):
   `GRAMMAR`, `LEXICON`, `MATCH`, `STYLE`.

Формула итогового балла:

```
FinalScore = 0.4 * RuleScore + 0.6 * (JudgeTotal / 8.0)
```

Вердикты: `PASS` (≥ 0.75), `PARTIAL` (0.5–0.75), `FAIL` (< 0.5).

## Структура проекта

```
OssetianBenchmark/
├── OssetianBenchmark.csproj
├── Program.cs
├── appsettings.json
├── README.md
├── .gitignore
├── Models/        # BenchmarkTask, BenchmarkResult, RuleCheck, JudgeScore, BenchmarkConfig
├── Services/      # LlmClient, TaskLoader, RuleBasedEvaluator, LlmJudgeEvaluator,
│                  # BenchmarkRunner, ResultsStorage, ProgressBar
├── Reports/       # ReportGenerator.cs
└── data/          # tasks.json, results.json (генерится), report.md (генерится)
```
