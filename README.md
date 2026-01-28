# Squirmify

**A comprehensive LLM evaluation framework that separates fact from marketing fiction.**

Squirmify is a rigorous model evaluation tool designed to test, rank, and expose the true capabilities of AI language models. It runs comprehensive tests across multiple dimensions and generates high-quality synthetic training datasets.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Why Squirmify?

Most LLM benchmarks rely on self-reported metrics and marketing claims. Squirmify puts models through real-world stress tests:

- **Instruction Following Tests** - Filters out models that can't follow basic commands
- **Reasoning Tests** - Evaluates multi-step logic, math, and pattern recognition
- **Context Window Stress Tests** - Exposes the gap between claimed vs. actual usable context (e.g., claims 32k but fails at 8k)
- **Conversation Tests** - Tests multi-turn conversational ability across different domains
- **Synthetic Data Generation** - Creates high-quality training datasets with optional "Kiwi" linguistic flavor
- **Auto-Judging System** - Uses top-performing models to score and rank responses

**The result:** Objective, reproducible data about which models actually deliver on their promises.

---

## Features

### Comprehensive Testing Pipeline

1. **Instruction Following Tests** (14 tests)

   - JSON generation & validation
   - Tool calling scenarios
   - Format constraints
   - Basic calculations
   - Exact output matching

2. **Reasoning Tests** (11 tests across 5 categories)

   - Multi-step problems
   - Context retention
   - Logic & syllogisms
   - Math problems
   - Pattern recognition
   - Judge-based scoring (no brittle validators)

3. **Context Window Stress Tests** (4 patterns)

   - Needle in Haystack (8k tokens)
   - Instruction Retention (6k tokens)
   - Code Context Stress (10k tokens)
   - Degradation Mapping (12k tokens)
   - Tracks hallucination vs. honest confusion
   - Exposes recency bias

4. **Conversation Tests** (8 scenarios across 4 domains)

   - Code assistance (debugging, refactoring)
   - Customer support (password reset, feature explanation)
   - Casual chat (hobbies, weekend plans)
   - Instruction following (todo lists, email refinement)
   - 3-4 turn exchanges per scenario

### Intelligent Judging System

- **Base Judge Selection**: Automatically selects the most capable model based on instruction and reasoning performance
- **Auto-Judges**: Top 2-3 models provide independent scoring
- **Multi-dimensional Scoring**: Accuracy, code quality, reasoning, coherence, helpfulness
- **High-Quality Dataset Extraction**: Automatically saves responses scoring above 7.5/10

### Synthetic Data Generation

- Loads base seed prompts
- Generates augmented variations with:
  - Paraphrasing
  - Context suffixes
  - Complexity modifiers
  - Optional "Kiwi" casual language flavor (e.g., "sweet as", "choice", "she'll be right")
- Category-specific system prompts (code, instruction, chat, support)
- Performance tracking (tokens/sec, latency)

---

## Installation

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [LM Studio](https://lmstudio.ai/) or any OpenAI-compatible API server
- One or more LLM models loaded in your model server

### Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/ChoonForge/Squirmify.git
   cd Squirmify
   ```

2. Build the solution:

   ```bash
   dotnet build
   ```

3. Run the Web UI or Console app (see Usage below)

4. Configure providers and migrate tests via the UI or CLI

---

## Usage

Squirmify has two interfaces: a **Blazor Web UI** and a **Console CLI**.

### Web UI (Recommended)

```bash
dotnet run --project src/Squirmify.Web
```

Open http://localhost:5105 in your browser. The Web UI provides:
- **Dashboard** - Overview and quick actions
- **Providers** - Add/manage LLM server endpoints
- **Models** - View/toggle models per provider
- **Configuration** - Create/edit test suite configs
- **Runs** - Start benchmarks, view progress, re-run previous tests
- **Results** - Detailed results with filtering

### Console CLI

```bash
dotnet run --project src/Squirmify.Console
```

**Interactive mode** (no arguments): Shows a menu for managing providers, configs, and running benchmarks.

**CLI Commands:**

```bash
# List configured providers
dotnet run --project src/Squirmify.Console -- providers

# List test configurations
dotnet run --project src/Squirmify.Console -- configs

# List recent benchmark runs
dotnet run --project src/Squirmify.Console -- runs
dotnet run --project src/Squirmify.Console -- runs --count 20

# Show run status (latest or specific run)
dotnet run --project src/Squirmify.Console -- status
dotnet run --project src/Squirmify.Console -- status 5

# Add a new provider
dotnet run --project src/Squirmify.Console -- add-provider --name "LM Studio" --url "http://localhost:1234/v1"
dotnet run --project src/Squirmify.Console -- add-provider --name "OpenRouter" --url "https://openrouter.ai/api/v1" --token "sk-..."

# Run benchmark (headless)
dotnet run --project src/Squirmify.Console -- run --provider 1 --config 1 --name "Nightly Run"

# Migrate JSON tests to SQLite database
dotnet run --project src/Squirmify.Console -- migrate <config-path>

# Show help
dotnet run --project src/Squirmify.Console -- help
```

### First-Time Setup

1. **Start the Web UI** and add a Provider (e.g., LM Studio at `http://localhost:1234/v1`)
2. **Migrate existing tests** (if you have JSON test files):
   ```bash
   dotnet run --project src/Squirmify.Console -- migrate src/config
   ```
3. **Create a Test Configuration** in the Web UI
4. **Start a Benchmark Run**

### Legacy Quick Start

For the original single-file console app:

```bash
cd src
dotnet run
```

Squirmify will automatically:

1. Load all available models from your server
2. Run instruction following tests
3. Run reasoning tests on qualified models
4. Select a base judge
5. Run context window stress tests (if enabled)
6. Run conversation tests (if enabled)
7. Generate/load seed prompts
8. Run all qualified models through prompts
9. Score responses with base judge
10. Select auto-judges and re-score
11. Generate comprehensive reports
12. Extract high-quality dataset

### Output Files

All results are saved to `output/`:

- `all_results.json` - All model responses with base judge scores
- `final_results.json` - Results with auto-judge scores
- `reasoning_results.json` - Reasoning test responses and scores
- `conversation_results.json` - Multi-turn conversation exchanges
- `context_window_results.json` - Context window stress test results
- `high_quality_dataset.jsonl` - High-scoring responses for training
- `seeds.json` - Generated augmented seed prompts

---

## Test Types Explained

### 1. Instruction Following Tests

**Purpose**: Gate-keeper tests to filter out models that can't follow basic instructions. Models must pass 80%+ to proceed to further testing.

**Categories**:

| Category | What It Tests | Example |
|----------|---------------|---------|
| `json` | Can the model output valid JSON? | "Output a JSON object with name and age fields" |
| `tool_calling` | Can it format tool/function calls correctly? | "Call the weather API with city=Auckland" |
| `format` | Can it follow specific output formats? | "List 5 items, one per line, numbered" |
| `calculation` | Can it perform basic math? | "What is 15% of 240?" |
| `exact` | Can it output exactly what's requested? | "Say only: Hello World" |
| `word_count` | Can it follow length constraints? | "Describe in exactly 50 words" |

**Validation Types**:
- `exact` - Response must match expected output exactly
- `words` - Response must contain specific words
- `lines` - Response must have specific number of lines
- `json` - Response must be valid JSON matching structure
- `numeric` - Response must match a numeric value
- `boolean` - Response must be true/false

**Config**: `config/tests/instruction_tests.json`

---

### 2. Reasoning Tests

**Purpose**: Evaluate a model's ability to think through problems, show its work, and arrive at correct answers. Uses judge-based scoring rather than brittle validators.

**Categories**:

| Category | What It Tests | Example |
|----------|---------------|---------|
| `multi-step` | Complex problems requiring sequential reasoning | "A store has 3 shelves with 4 boxes each..." |
| `context` | Using provided context to answer questions | "Given this passage, what year did X happen?" |
| `logic` | Syllogisms, deduction, inference | "All A are B. All B are C. Is all A also C?" |
| `math` | Mathematical problem solving | "If train A leaves at 9am going 60mph..." |
| `pattern` | Pattern recognition and extrapolation | "What comes next: 2, 6, 12, 20, ?" |

**Scoring Dimensions** (1-10 each):
- **Correct Answer**: Did they get the right answer?
- **Logical Steps**: Did they show their reasoning/work?
- **Clarity**: Was the explanation clear and easy to follow?
- **Overall Score**: Holistic assessment

**Config**: `config/tests/reasoning_tests.json`

---

### 3. Context Window Stress Tests

**Purpose**: Expose the gap between claimed context window size and actual usable context. Many models claim 32k+ tokens but fail catastrophically at 8k.

**Test Patterns**:

| Pattern | Description |
|---------|-------------|
| Stealth Needle Storm | Buries multiple "secret words" throughout long context and asks model to recall them |
| Lost in the Middle | Tests recall at different positions (beginning, middle, end) to detect recency bias |
| Buried Instruction | Places a critical instruction deep in filler content |

**What Gets Measured**:
- **Max Reliable Context**: Largest context size where model maintains accuracy
- **Degradation Pattern**: How does accuracy fall off?
  - `graceful` - Slow, predictable decline (>100k tokens)
  - `moderate` - Noticeable decline (60k-100k tokens)
  - `sudden` - Sharp drop at a threshold (30k-60k tokens)
  - `catastrophic` - Immediate failure (<30k tokens)
- **Checkpoint Recall**: Can it remember info from specific positions?
- **Hallucination Detection**: Does it make up answers vs. honestly admitting confusion?

**Config**: `config/tests/context_window_tests.json`

---

### 4. Conversation Tests

**Purpose**: Evaluate multi-turn conversational ability. Real applications involve back-and-forth exchanges, not one-shot prompts.

**Categories**:

| Category | Scenarios |
|----------|-----------|
| `code` | Debugging help, refactoring suggestions, code review |
| `support` | Password reset, feature explanation, troubleshooting |
| `chat` | Hobbies discussion, weekend plans, casual conversation |
| `instruction` | Todo list building, email refinement, step-by-step tasks |

**Each test includes**:
- 3-4 turn exchanges
- System prompt defining the persona
- Expected themes for each turn
- Judging criteria specific to the scenario

**Scoring Dimensions** (1-10 each):
- **Topic Coherence**: Stayed on topic, didn't drift
- **Conversational Tone**: Natural, appropriate, friendly
- **Context Retention**: Remembered earlier turns
- **Helpfulness**: Moved conversation forward productively

**Config**: `config/tests/conversation_tests.json`

---

## How Judging Works

Squirmify uses a sophisticated multi-judge system to score model responses objectively.

### Judge Selection Pipeline

```
1. All models take instruction tests (quality gate)
         ↓
2. Passing models (80%+) take reasoning tests
         ↓
3. Best performer becomes "Base Judge"
         ↓
4. Top 2-3 performers become "Auto-Judges"
         ↓
5. All qualified models run through prompts
         ↓
6. Base Judge scores all responses
         ↓
7. Auto-Judges independently re-score
         ↓
8. Final scores are aggregated
```

### Why Multiple Judges?

- **Reduces bias**: Single-judge scoring can be biased toward similar models
- **Increases reliability**: Multiple perspectives catch edge cases
- **Validates consistency**: High agreement = reliable scores

### Judge Prompt Templates

Judges receive structured prompts with:
- The original task/prompt
- The correct answer (for reference)
- The model's response
- Scoring rubric with dimensions
- Required JSON output format

Example judge output:
```json
{
  "overall_score": 8,
  "correct_answer": 9,
  "logical_steps": 7,
  "clarity": 8,
  "reasoning": "Correct answer with clear steps, minor formatting issues"
}
```

### Scoring Thresholds

| Threshold | Meaning |
|-----------|---------|
| 7.5+ | High-quality (saved to training dataset) |
| 7.0+ | Passes reasoning qualification |
| 80%+ | Passes instruction qualification |

---

## Configuration

All configuration is externalized to JSON files in the `config/` directory.

**For a complete reference of all settings, see [docs/SETTINGS.md](docs/SETTINGS.md).**

### Main Settings (`config/settings.json`)

```json
{
  "server": {
    "baseUrl": "http://localhost:1234/v1",
    "authToken": "",
    "useAuth": false,
    "requestTimeoutMinutes": 10
  },
  "testSuites": {
    "runPromptTests": true,
    "runContextWindowTests": false,
    "runConversationTests": true
  },
  "seedGeneration": {
    "targetSeedCount": 5
  },
  "scoring": {
    "highQualityThreshold": 7.5,
    "topJudgeCount": 2
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `server.baseUrl` | LLM API endpoint | `http://localhost:1234/v1` |
| `server.useAuth` | Enable API key auth | `false` |
| `server.requestTimeoutMinutes` | Request timeout | `10` |
| `testSuites.runPromptTests` | Enable prompt testing | `true` |
| `testSuites.runContextWindowTests` | Enable context tests | `false` |
| `testSuites.runConversationTests` | Enable conversation tests | `true` |
| `seedGeneration.targetSeedCount` | Number of seeds to generate | `5` |
| `scoring.highQualityThreshold` | Score for HQ dataset | `7.5` |
| `scoring.topJudgeCount` | Number of auto-judges | `2` |

### Test Configuration Files

| File | Purpose |
|------|---------|
| `config/tests/instruction_tests.json` | Instruction following test definitions |
| `config/tests/reasoning_tests.json` | Reasoning test definitions + judge prompts |
| `config/tests/conversation_tests.json` | Multi-turn conversation scenarios |
| `config/tests/context_window_tests.json` | Context window stress tests + filler content |

### Prompt Configuration

| File | Purpose |
|------|---------|
| `config/prompts/system_prompts.json` | Category-specific system prompts |

### Seed Augmentation

| File | Purpose |
|------|---------|
| `config/augmentation/seed_augmentation.json` | Kiwi phrases, suffixes, verb paraphrases |

---

## Project Structure

```
Squirmify/
├── src/
│   ├── Squirmify.Core/              # Shared library
│   │   ├── Entities/                # Database entity classes
│   │   ├── Interfaces/              # Repository & service interfaces
│   │   └── DTOs/                    # Data transfer objects
│   │
│   ├── Squirmify.Data/              # Data access layer
│   │   ├── Database/
│   │   │   └── DatabaseInitializer.cs
│   │   └── Repositories/            # SQLite repositories (Dapper)
│   │
│   ├── Squirmify.Services/          # Business logic
│   │   ├── Evaluation/              # LLM client, test runners
│   │   └── Orchestration/           # Benchmark orchestrator
│   │
│   ├── Squirmify.Console/           # CLI application
│   │   ├── Program.cs               # Interactive menu + CLI commands
│   │   └── DataMigrator.cs          # JSON to SQLite migration
│   │
│   ├── Squirmify.Web/               # Blazor Server web UI
│   │   ├── Components/
│   │   │   ├── Layout/
│   │   │   └── Pages/               # Dashboard, Runs, Results, etc.
│   │   └── wwwroot/
│   │
│   ├── config/                      # Test definition JSON files
│   │   ├── tests/
│   │   │   ├── instruction_tests.json
│   │   │   ├── reasoning_tests.json
│   │   │   └── conversation_tests.json
│   │   ├── prompts/
│   │   └── augmentation/
│   │
│   └── Program.cs                   # Legacy single-file console app
│
├── Dockerfile
├── docker-compose.yml
├── Squirmify.sln
├── LICENSE
└── README.md
```

### Architecture

- **SQLite Database**: Fully normalized schema for providers, models, test definitions, runs, and results
- **Dapper ORM**: Lightweight data access
- **Blazor Server**: Real-time web UI with SignalR
- **Console CLI**: For automation and scripting

---

## Example Output

### Model Performance Summary

```
┌──────────────────────────┬───────────┬─────────┬────┬──────────┐
│ Model                    │ Avg Score │ Avg t/s │ HQ │ Best Cat │
├──────────────────────────┼───────────┼─────────┼────┼──────────┤
│ qwen2.5-14b-instruct     │ 8.2       │ 41.1    │ 67 │ Code     │
│ hermes-2-pro-llama-3-8b  │ 8.0       │ 78.6    │ 50 │ Code     │
│ llama-3-groq-8b-tool-use │ 8.0       │ 73.3    │ 50 │ Code     │
└──────────────────────────┴───────────┴─────────┴────┴──────────┘
```

### Context Window Reality Check

```
| Model                   | Reliable | Degradation  | Accuracy           |
| ----------------------- | -------- | ------------ | ------------------ |
| qwen/qwen3-30b-a3b-2507 | 108,000  | graceful     | 96.9%              |
| hermes-3-llama-3.2-3b   | 54,666   | catastrophic | 90.4%              |
| baidu/ernie-4.5-21b-a3b | 16,000   | catastrophic | 50.0%              |
| qwen2.5-3b-instruct     | 0        | catastrophic | 0.0%               |
| google/gemma-3n-e4b     | 0        | catastrophic | 0.0%               |
| lfm2-8b-a1b             | 0        | catastrophic | 0.3%               |
```

See [`docs/CONTEXT_WINDOW_EXPLAINED.md`](docs/CONTEXT_WINDOW_EXPLAINED.md) for detailed explanations of context window metrics.

---

## How It Works

### 1. Instruction Tests (Quality Gate)

Models must pass basic instruction following tests (80%+ pass rate) to proceed. This filters out models that can't handle simple tasks.

### 2. Reasoning Tests (Judge-Based)

Remaining models face 11 reasoning challenges. A judge model scores each response on correctness, logic, and clarity.

### 3. Base Judge Selection

The system intelligently selects the best-performing model as the primary judge, considering both instruction following and reasoning scores.

### 4. Context Window Stress Tests

Tests inject anchor words and checkpoints throughout long contexts, then probe at 25%, 50%, 75%, and 100% of target lengths. Tracks:

- Max reliable context length
- When hallucination starts
- Checkpoint recall accuracy
- Degradation pattern (graceful, moderate, sudden, catastrophic)

### 5. Conversation Tests

8 multi-turn scenarios (3-4 exchanges each) across code, support, chat, and instruction domains. Judge evaluates coherence, tone, context retention, and helpfulness.

### 6. Seed Generation & Prompt Pipeline

- Loads base seeds
- Generates augmented variations (paraphrasing, complexity, Kiwi flavor)
- Runs all qualified models through prompts
- Records responses with performance metrics

### 7. Auto-Judging

Top 2-3 models (based on instruction + reasoning performance) independently score all responses. This provides multiple perspectives and reduces bias.

### 8. Report Generation

Final summaries show:

- Model rankings by average score
- Performance metrics (tokens/sec, latency)
- High-quality response counts
- Best category for each model
- Category-level performance

---

## Advanced Usage

### Adding Custom Tests

Add new tests by editing the JSON config files:

**Instruction Test** (`config/tests/instruction_tests.json`):
```json
{
  "prompt": "Your test prompt here",
  "expectedResult": "Expected output",
  "validationType": "exact",
  "category": "custom"
}
```

**Reasoning Test** (`config/tests/reasoning_tests.json`):
```json
{
  "category": "logic",
  "description": "Test description",
  "prompt": "Your reasoning question",
  "correctAnswer": "The expected answer"
}
```

**Conversation Test** (`config/tests/conversation_tests.json`):
```json
{
  "category": "support",
  "description": "Password reset scenario",
  "systemPrompt": "You are a helpful support agent",
  "turns": [
    {"userMessage": "I forgot my password", "expectedTheme": "password reset"}
  ],
  "judgingCriteria": ["empathy", "clear instructions"]
}
```

### Customizing Judge Prompts

Edit `config/tests/reasoning_tests.json` to modify the `judgeSystemPrompt` and `judgePromptTemplate` fields.

### Excluding Models

Add models to the exclude list in `Program.cs`:

```csharp
var exclude = new[] { "qwen2.5-0.5b-instruct", "lfm2-1.2b", "zephyr-7b-beta" };
```

---

## Dependencies

- [Spectre.Console](https://spectreconsole.net/) - Beautiful CLI rendering
- [SharpToken](https://github.com/dmitry-brazhenko/SharpToken) - Token counting
- .NET 10.0

---

## Documentation

- [Settings Reference](docs/SETTINGS.md) - Complete configuration reference with all options explained
- [Context Window Test Explained](docs/CONTEXT_WINDOW_EXPLAINED.md) - Deep dive into context window stress testing
- [Context Window Stress Test Summary](docs/context-window-stress-test-summary.md) - Summary and analysis
- [V2 Evaluator Design](docs/v2%20Evaluator.md) - Design notes and implementation details

---

## Contributing

Contributions welcome! Areas of interest:

- Additional test scenarios (reasoning, instruction, conversation)
- Support for other model APIs (OpenRouter, Anthropic, etc.)
- Performance optimizations
- New evaluation metrics
- Documentation improvements

Please open an issue or submit a pull request.

---

## License

MIT License - see [LICENSE](LICENSE) for details

---

## Acknowledgments

- Built with [LM Studio](https://lmstudio.ai/) for local model testing
- Inspired by the need for honest, reproducible LLM benchmarks
- Kiwi flavor inspired by New Zealand English

---

## FAQ

**Q: Why "Squirmify"?**
A: Because models should squirm when their marketing claims are put to the test. Also, it makes them work hard enough to squirm.

**Q: Do I need a powerful GPU?**
A: Squirmify is just the test harness. Your model server (LM Studio, etc.) needs the GPU. Squirmify itself is lightweight.

**Q: Can I use this with OpenAI/Anthropic/etc.?**
A: Currently optimized for LM Studio's OpenAI-compatible API. Support for other providers can be added by extending `ModelService.cs`.

**Q: How long does a full evaluation take?**
A: Depends on model count, seed count, and model speed. Typical run: 10 models x 50 seeds x 4 test suites = ~30-60 minutes.

**Q: What's with the Kiwi language?**
A: Optional New Zealand English flavor for synthetic data generation. Toggle with the `kiwi` parameter in seed generation. Sweet as!

**Q: Can I disable certain test suites?**
A: Yes! Edit `config/settings.json` and set `runPromptTests`, `runContextWindowTests`, or `runConversationTests` to `false`.

**Q: How do I add my own tests?**
A: Edit the JSON files in `config/tests/`. See the "Adding Custom Tests" section above.

---

**Made by [ChoonForge](https://github.com/ChoonForge)**
