# Squirmify Settings Reference

Complete reference for all configuration options in `src/config/settings.json`.

---

## Server Settings

Connection settings for your LLM API server.

```json
"server": {
  "baseUrl": "http://localhost:1234/v1",
  "authToken": "",
  "useAuth": false,
  "requestTimeoutMinutes": 10
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `baseUrl` | string | `http://localhost:1234/v1` | OpenAI-compatible API endpoint. LM Studio default is port 1234. |
| `authToken` | string | `""` | API key/token for authenticated endpoints. Leave empty for local servers. |
| `useAuth` | bool | `false` | Enable bearer token authentication. Set `true` for remote APIs. |
| `requestTimeoutMinutes` | int | `10` | Request timeout. Increase for slow models or large context tests. |

**Examples:**
- LM Studio local: `"baseUrl": "http://localhost:1234/v1"`
- Remote server: `"baseUrl": "http://your-server:8080/v1"` with `"useAuth": true`
- OpenRouter: `"baseUrl": "https://openrouter.ai/api/v1"` with your API key

---

## Test Suites

Control which test suites run.

```json
"testSuites": {
  "runPromptTests": true,
  "runContextWindowTests": false,
  "runConversationTests": true,
  "runQualificationTests": true
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `runPromptTests` | bool | `true` | Run seed prompt generation and model responses. |
| `runContextWindowTests` | bool | `false` | Run context window stress tests. Can be slow. |
| `runConversationTests` | bool | `true` | Run multi-turn conversation tests. |
| `runQualificationTests` | bool | `true` | Run instruction/reasoning qualification tests. |

**Typical configurations:**
- Full evaluation: all `true`
- Context window only: only `runContextWindowTests: true`, others `false`
- Quick prompt test: only `runPromptTests: true`

---

## Scoring Settings

Control quality thresholds and judge selection.

```json
"scoring": {
  "highQualityThreshold": 7.5,
  "topJudgeCount": 2
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `highQualityThreshold` | float | `7.5` | Minimum score for responses to be saved to high-quality dataset. Range 1-10. |
| `topJudgeCount` | int | `2` | Number of top-performing models to use as auto-judges. |

---

## Judging Settings

Override automatic judge selection or re-score existing results.

```json
"judging": {
  "scoreOnly": false,
  "scoreOnlyInputFile": "all_results.json",
  "overrideBaseJudge": "",
  "overrideAutoJudges": []
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `scoreOnly` | bool | `false` | Skip model runs, only score existing results from input file. |
| `scoreOnlyInputFile` | string | `"all_results.json"` | File to load when `scoreOnly` is true. |
| `overrideBaseJudge` | string | `""` | Force a specific model as base judge. Empty = auto-select. |
| `overrideAutoJudges` | array | `[]` | Force specific models as auto-judges. Empty = auto-select. |

---

## Context Window Tests

Configure context window stress testing.

```json
"contextWindowTests": {
  "level": "shallow",
  "maxTests": 5,
  "degradationThresholds": {
    "graceful": 100000,
    "moderate": 60000,
    "sudden": 30000
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `level` | string | `"shallow"` | Test intensity: `"shallow"` (0.1x), `"deep"` (0.5x), `"abyss"` (1.0x). |
| `maxTests` | int | `5` | Maximum number of context tests to run per model. |

### Level Multipliers

The level scales both the target token count AND the degradation thresholds:

| Level | Multiplier | Max Tokens | Graceful Threshold |
|-------|------------|------------|-------------------|
| `shallow` | 0.1x | ~12,800 | >10,000 |
| `deep` | 0.5x | ~64,000 | >50,000 |
| `abyss` | 1.0x | ~128,000 | >100,000 |

### Degradation Thresholds

How many tokens a model can reliably handle before accuracy drops:

| Pattern | Threshold | Meaning |
|---------|-----------|---------|
| `graceful` | >100,000 | Excellent - slow, predictable decline |
| `moderate` | >60,000 | Good - noticeable but manageable decline |
| `sudden` | >30,000 | Poor - sharp accuracy drop at threshold |
| `catastrophic` | <30,000 | Fails early despite claims |

---

## Seed Generation

Control synthetic prompt generation.

```json
"seedGeneration": {
  "targetSeedCount": 5,
  "baseSeedsFile": "base_seeds.jsonl",
  "generatedSeedsFile": "seeds.json",
  "overwriteSeeds": true,
  "categoryWeights": {
    "code": 0.25,
    "instruction": 0.25,
    "chat": 0.25,
    "support": 0.25
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `targetSeedCount` | int | `5` | Number of seed prompts to generate per category. |
| `baseSeedsFile` | string | `"base_seeds.jsonl"` | Input file with base prompts for augmentation. |
| `generatedSeedsFile` | string | `"seeds.json"` | Output file for generated/augmented prompts. |
| `overwriteSeeds` | bool | `true` | Regenerate seeds each run. `false` = reuse existing. |
| `categoryWeights` | object | equal | Distribution of prompts across categories. Must sum to 1.0. |

---

## Instruction Tests

Configuration for instruction-following qualification tests.

```json
"instructionTests": {
  "maxModelErrors": 3,
  "temperature": 0.0,
  "topP": 0.8,
  "passThreshold": 0.8,
  "maxTestsPerCategory": 5,
  "categoryLimits": { ... }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `maxModelErrors` | int | `3` | Consecutive errors before flagging a model as broken. |
| `temperature` | float | `0.0` | LLM temperature. Low = deterministic for exact matching. |
| `topP` | float | `0.8` | Nucleus sampling threshold. |
| `passThreshold` | float | `0.8` | Minimum pass rate (0.0-1.0) to qualify for further testing. |
| `maxTestsPerCategory` | int | `5` | Default test limit per category. |
| `categoryLimits` | object | varies | Per-category test limits. Overrides `maxTestsPerCategory`. |

---

## Reasoning Tests

Configuration for reasoning qualification tests.

```json
"reasoningTests": {
  "minScore": 7.0,
  "temperature": 0.3,
  "topP": 0.9,
  "maxTokens": 1000,
  "maxTestsPerCategory": 3,
  "categoryLimits": { ... }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `minScore` | float | `7.0` | Minimum judge score (1-10) to pass reasoning qualification. |
| `temperature` | float | `0.3` | Slightly creative for reasoning explanations. |
| `topP` | float | `0.9` | Nucleus sampling threshold. |
| `maxTokens` | int | `1000` | Max response length for reasoning problems. |
| `maxTestsPerCategory` | int | `3` | Default tests per reasoning category. |
| `categoryLimits` | object | varies | Per-category limits for: multi-step, context, logic, math, pattern, etc. |

---

## Conversation Tests

Configuration for multi-turn conversation tests.

```json
"conversationTests": {
  "temperature": 0.8,
  "topP": 0.9,
  "maxTokens": 1000,
  "maxTestsPerCategory": 3,
  "categoryLimits": { ... }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `temperature` | float | `0.8` | Higher for natural conversation. |
| `topP` | float | `0.9` | Nucleus sampling threshold. |
| `maxTokens` | int | `1000` | Max response length per turn. |
| `maxTestsPerCategory` | int | `3` | Default tests per conversation category. |
| `categoryLimits` | object | varies | Per-category limits for: code, support, chat, instruction, roleplay, etc. |

---

## Generation Settings

Global defaults for prompt responses.

```json
"generation": {
  "globalTemperature": 0.5,
  "globalTopP": 0.9,
  "globalMaxTokens": 512
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `globalTemperature` | float | `0.5` | Default temperature when not category-specific. |
| `globalTopP` | float | `0.9` | Default nucleus sampling. |
| `globalMaxTokens` | int | `512` | Default max response length. |

---

## Category Settings

Per-category generation parameters and system prompts.

```json
"categorySettings": {
  "code": {
    "temperature": 0.3,
    "maxTokens": 2500,
    "systemPrompt": "You are a senior .NET engineer..."
  },
  "instruction": { ... },
  "chat": { ... },
  "support": { ... }
}
```

| Category | Temperature | Max Tokens | Use Case |
|----------|-------------|------------|----------|
| `code` | 0.3 | 2500 | Low temp for correct, consistent code. |
| `instruction` | 0.6 | 800 | Balanced for clear explanations. |
| `chat` | 0.95 | 800 | High temp for natural, varied conversation. |
| `support` | 0.95 | 1200 | High temp for empathetic, natural support. |

**Customizing system prompts:** Edit the `systemPrompt` field to change the persona/behavior for each category.

---

## Performance Settings

Control parallel execution.

```json
"performance": {
  "maxParallelRequests": 4
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `maxParallelRequests` | int | `4` | Concurrent API requests. Increase for faster runs on powerful hardware. |

**Note:** Higher parallelism uses more VRAM. Reduce if you see OOM errors.

---

## Output Settings

Control where results are saved.

```json
"output": {
  "directory": "output"
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `directory` | string | `"output"` | Relative path for result files. |

---

## Local Configuration Override

For personal settings (API keys, custom endpoints), create `settings.local.json` in the same directory. This file is gitignored and won't be committed.

The local file only needs the settings you want to override:

```json
{
  "server": {
    "baseUrl": "http://my-server:8080/v1",
    "authToken": "my-secret-key",
    "useAuth": true
  }
}
```

**Note:** Local override is not yet implemented in the codebase. For now, edit `settings.json` directly but don't commit credentials.

---

## Quick Reference: Common Configurations

### Local LM Studio (default)
```json
"server": {
  "baseUrl": "http://localhost:1234/v1",
  "useAuth": false
}
```

### Context Window Testing Only
```json
"testSuites": {
  "runPromptTests": false,
  "runContextWindowTests": true,
  "runConversationTests": false,
  "runQualificationTests": false
}
```

### Full Abyss-Level Context Test
```json
"contextWindowTests": {
  "level": "abyss",
  "maxTests": 10
}
```

### Fast Shallow Test
```json
"contextWindowTests": {
  "level": "shallow",
  "maxTests": 3
}
```

### More Parallel Requests (powerful GPU)
```json
"performance": {
  "maxParallelRequests": 8
}
```
