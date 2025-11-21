# Squirmify 🔥

**A comprehensive LLM evaluation framework that separates fact from marketing fiction.**

Squirmify is a rigorous model evaluation tool designed to test, rank, and expose the true capabilities of AI language models. It runs comprehensive tests across multiple dimensions and generates high-quality synthetic training datasets.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)

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

### 🎯 Comprehensive Testing Pipeline

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

### 📊 Intelligent Judging System

- **Base Judge Selection**: Automatically selects the most capable model based on instruction and reasoning performance
- **Auto-Judges**: Top 2-3 models provide independent scoring
- **Multi-dimensional Scoring**: Accuracy, code quality, reasoning, coherence, helpfulness
- **High-Quality Dataset Extraction**: Automatically saves responses scoring above 7.5/10

### 🥝 Synthetic Data Generation

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

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [LM Studio](https://lmstudio.ai/) or any OpenAI-compatible API server
- One or more LLM models loaded in your model server

### Setup

1. Clone the repository:
```bash
git clone https://github.com/ChoonForge/Squirmify.git
cd Squirmify
```

2. Build the project:
```bash
cd src
dotnet build
```

3. Configure your model server in `src/Config.cs`:
```csharp
public const string BaseUrl = "http://localhost:1234/v1"; // LM Studio default
```

4. (Optional) Add base seed prompts to `src/base_seeds.jsonl`:
```jsonl
{"instruction":"Create a C# extension method to convert a string to title case.","tags":["code"]}
{"instruction":"Explain how dependency injection works in ASP.NET Core.","tags":["instruction"]}
```

---

## Usage

### Quick Start

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

### Configuration

Edit `src/Config.cs` to customize behavior:

```csharp
// Toggle test suites
public const bool RunPromptTests = true;
public const bool RunContextWindowTests = true;
public const bool RunConversationTests = true;

// Seed generation
public const int TargetSeedCount = 10;

// High-quality threshold
public const double HighQualityThreshold = 7.5;

// Performance
public const int MaxParallelRequests = 1;
```

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
┌─────────────────────────┬──────────────┬─────────────────────┬──────────────┐
│ Model                   │ Max Reliable │ First Hallucination │ Degradation  │
├─────────────────────────┼──────────────┼─────────────────────┼──────────────┤
│ qwen2.5-7b-instruct     │ 8,000        │ 10,000              │ graceful     │
│ llama-3.2-3b-instruct   │ 6,000        │ 7,500               │ moderate     │
│ phi-3.5-mini-instruct   │ 4,000        │ 5,000               │ sudden       │
│ gemma-2-2b-instruct     │ 3,000        │ 3,500               │ catastrophic │
└─────────────────────────┴──────────────┴─────────────────────┴──────────────┘
```

See [`docs/CONTEXT_WINDOW_EXPLAINED.md`](docs/CONTEXT_WINDOW_EXPLAINED.md) for detailed explanations of context window metrics.

---

## Project Structure

```
Squirmify/
├── src/
│   ├── Program.cs              # Main evaluation pipeline
│   ├── Config.cs               # Configuration settings
│   ├── Extensions.cs           # Utility extensions
│   ├── Models/
│   │   ├── DTOs.cs            # Data transfer objects
│   │   └── ConversationTest.cs # Conversation test models
│   ├── Services/
│   │   ├── ModelService.cs              # LLM API client
│   │   ├── TestService.cs               # Instruction tests
│   │   ├── ReasoningTestService.cs      # Reasoning tests
│   │   ├── ContextWindowTestService.cs  # Context stress tests
│   │   ├── ConversationTestService.cs   # Multi-turn conversation tests
│   │   ├── SeedService.cs               # Seed generation & augmentation
│   │   └── JudgingService.cs            # Scoring & judging logic
│   ├── base_seeds.jsonl        # Base prompts for augmentation
│   └── Squirmify.csproj        # .NET project file
├── docs/
│   ├── CONTEXT_WINDOW_EXPLAINED.md      # Context window test details
│   ├── context-window-stress-test-summary.md
│   └── v2 Evaluator.md                  # Design notes
├── output/                     # Generated results (created at runtime)
├── LICENSE
└── README.md
```

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

### Custom System Prompts

Edit category-specific system prompts in `Config.cs`:

```csharp
public static readonly Dictionary<string, CategoryDefaults> CategorySettings = new()
{
    ["code"] = new(0.3, 2500, "You are a senior .NET engineer..."),
    ["instruction"] = new(0.6, 800, "You are a patient teacher..."),
    ["chat"] = new(0.95, 800, "You are a friendly conversational partner..."),
    ["support"] = new(0.95, 1200, "You are a compassionate support specialist...")
};
```

### Adding Custom Instruction Tests

Extend `TestService.cs` with new test cases:

```csharp
new InstructionTest
{
    Prompt = "Your test prompt here",
    ExpectedResult = "Expected output",
    ValidationType = ValidationType.Exact
}
```

### Excluding Models

Add models to the exclude list in `Program.cs`:

```csharp
var exclude = new[] { "qwen2.5-0.5b-instruct", "lfm2-1.2b", "zephyr-7b-beta" };
```

---

## Dependencies

- [Spectre.Console](https://spectreconsole.net/) - Beautiful CLI rendering
- [SharpToken](https://github.com/dmitry-brazhenko/SharpToken) - Token counting
- .NET 9.0

---

## Documentation

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
- Kiwi flavor inspired by New Zealand English 🥝

---

## FAQ

**Q: Why "Squirmify"?**
A: Because models should squirm when their marketing claims are put to the test. Also, it makes them work hard enough to squirm.

**Q: Do I need a powerful GPU?**
A: Squirmify is just the test harness. Your model server (LM Studio, etc.) needs the GPU. Squirmify itself is lightweight.

**Q: Can I use this with OpenAI/Anthropic/etc.?**
A: Currently optimized for LM Studio's OpenAI-compatible API. Support for other providers can be added by extending `ModelService.cs`.

**Q: How long does a full evaluation take?**
A: Depends on model count, seed count, and model speed. Typical run: 10 models × 50 seeds × 4 test suites = ~30-60 minutes.

**Q: What's with the Kiwi language?**
A: Optional New Zealand English flavor for synthetic data generation. Toggle with the `kiwi` parameter in seed generation. Sweet as!

**Q: Can I disable certain test suites?**
A: Yes! Set flags in `Config.cs`: `RunPromptTests`, `RunContextWindowTests`, `RunConversationTests`.

---

**Made with ☕ by [ChoonForge](https://github.com/ChoonForge)**
