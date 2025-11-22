# Seed Augmentation Strategy: Improvement Proposal

**Status:** Proposal
**Author:** Analysis of current implementation
**Date:** 2025-11-22

---

## Executive Summary

The current seed augmentation strategy successfully scales a curated base set (884 seeds) into larger test datasets. However, the augmentation techniques produce **shallow variations** that don't meaningfully expand model evaluation coverage. This document proposes a multi-tier improvement strategy focused on **semantic diversity** over **syntactic variation**.

---

## Current State Analysis

### What's Working ✅

1. **Curated Foundation** - 884 high-quality base seeds across 4 categories
2. **Category Balance** - Weighted distribution (30% code, 20% instruction, 25% chat, 25% support)
3. **Scalability** - Generates target count efficiently
4. **Deduplication** - HashSet prevents exact duplicates

### Critical Issues ⚠️

#### 1. Shallow Augmentation
**Problem:** Current techniques create surface-level variations:

```csharp
// Current approach
"Create a C# extension method..."
→ "Build a C# extension method..."      // Just verb swap
→ "Create a C# extension method... Keep it under 200 words."  // Arbitrary constraint
```

**Impact:** Testing the same capability repeatedly. A model that can "create" can also "build" - no new insight gained.

#### 2. Non-Semantic Context Suffixes
**Problem:** Appended constraints don't change the core task:

```csharp
ContextSuffixes = new[] {
    "Keep it under 200 words.",
    "Prefer bullet points and be concise.",
    "Assume .NET 9 and Blazor."
};
```

**Impact:** Model either ignores the suffix or adds minor formatting changes. Doesn't test different reasoning paths.

#### 3. Kiwi Augmentation is Cosmetic
**Problem:** Lines 474-508 replace phrases like "okay" → "sweet as"

**Impact:** This tests *style adherence*, not capability. Novelty feature without evaluation value.

#### 4. Code Debt
- `NotImplementedException` at line 343
- Unclear weighted/unweighted split (lines 90-112)
- Duplicate method names (`GenerateAugmentedSeedsAsyncOld`)

---

## Proposed Improvement Framework

### Philosophy Shift

| Current Approach | Proposed Approach |
|-----------------|-------------------|
| Syntactic variation | Semantic transformation |
| Surface-level changes | Conceptual diversity |
| Random suffixes | Structured constraints |
| Single-dimension augmentation | Multi-dimensional expansion |

---

## Tier 1: Semantic Augmentation Strategies

### 1.1 Cross-Domain Translation
Transform the **problem domain** while preserving complexity:

**Base Seed:**
```
"Create a C# extension method to convert a string to title case."
```

**Augmented Seeds:**
```csharp
// Language variation
"Create a Python function to convert a string to title case."
"Implement a Rust function for title case conversion."

// Framework variation
"Write a JavaScript utility for title casing in React."
"Build a Blazor component that title-cases user input."

// Paradigm shift
"Design a functional approach to title casing in F#."
"Implement title case conversion using LINQ."
```

**Benefit:** Tests model's language breadth and transfer learning.

---

### 1.2 Constraint-Based Variation
Add **meaningful** constraints that change the solution approach:

**Base Seed:**
```
"Write a unit test using xUnit for a method that validates email addresses."
```

**Augmented Seeds:**
```csharp
// Performance constraint
"Write a unit test for email validation that handles 10,000 inputs/sec."

// Resource constraint
"Write a unit test for email validation without regex (manual parsing only)."

// Architecture constraint
"Write a parameterized xUnit theory that tests email validation across 20 edge cases."

// Security constraint
"Write a unit test that validates email addresses and detects ReDoS vulnerabilities."
```

**Benefit:** Forces different algorithmic approaches and deeper reasoning.

---

### 1.3 Difficulty Graduation
Transform complexity **meaningfully**, not cosmetically:

**Base Seed:**
```
"Implement IAsyncEnumerable to stream large datasets from a database."
```

**Augmented Seeds:**
```csharp
// Simplification
"Explain what IAsyncEnumerable does with a simple example."

// Intermediate
"Implement IAsyncEnumerable with cancellation token support."

// Advanced
"Implement IAsyncEnumerable with backpressure handling for 1GB+ datasets."

// Expert
"Compare IAsyncEnumerable vs Channels vs Reactive Extensions for streaming scenarios."
```

**Benefit:** Maps capability across skill levels rather than repeating same level.

---

### 1.4 Problem Inversion
Flip the task to test understanding:

**Base Seed:**
```
"Explain how dependency injection works in ASP.NET Core."
```

**Augmented Seeds:**
```csharp
// Debugging inversion
"This DI registration is failing. Why? [code sample]"

// Design inversion
"When should you NOT use dependency injection?"

// Critique inversion
"Review this DI setup and suggest improvements: [code sample]"

// Implementation inversion
"Implement a minimal DI container from scratch to demonstrate DI concepts."
```

**Benefit:** Tests comprehension vs. pattern matching.

---

## Tier 2: Category-Specific Strategies

### 2.1 Code Category (Current: 30%)

#### Strategy: **Technology Matrix Expansion**

```csharp
public class CodeAugmentationStrategy
{
    private readonly string[] Languages = { "C#", "F#", "Python", "TypeScript", "Rust" };
    private readonly string[] Frameworks = { "ASP.NET Core", "Blazor", "Minimal APIs", "gRPC" };
    private readonly string[] Paradigms = { "OOP", "Functional", "Async", "Reactive" };

    public IEnumerable<SeedItem> Augment(SeedItem baseSeed)
    {
        // Cross language
        foreach (var lang in Languages)
            yield return TranslateToLanguage(baseSeed, lang);

        // Add architectural constraints
        yield return AddConstraint(baseSeed, "No external dependencies");
        yield return AddConstraint(baseSeed, "Thread-safe implementation required");

        // Difficulty variation
        yield return Simplify(baseSeed);
        yield return AddComplexity(baseSeed, "edge cases + performance optimization");
    }
}
```

---

### 2.2 Support Category (Current: 25%)

#### Strategy: **Emotional Context Variation**

Current support seeds are excellent (lines 848-870) but can be augmented with:

```csharp
// Severity variation
"I'm feeling a bit stressed lately."  // Mild
→ "I'm overwhelmed and can't cope."   // Moderate
→ "I'm having a mental health crisis." // Severe (needs crisis resources)

// Temporal variation
"I just lost my job."                 // Acute
→ "I've been unemployed for 6 months." // Chronic

// Context expansion
"I'm anxious about work."
→ "I'm anxious about work and it's affecting my family life."
→ "I'm anxious about work due to discrimination I'm experiencing."
```

**Critical:** Support augmentation should maintain **authenticity**. Don't artificially manufacture emotional states.

---

### 2.3 Instruction Category (Current: 20%)

#### Strategy: **Pedagogical Depth Variation**

```csharp
// Explanation depth
"Explain middleware pipeline in ASP.NET Core."
→ "Explain middleware pipeline to a beginner."
→ "Explain middleware pipeline with visual diagrams."
→ "Compare ASP.NET Core middleware to Express.js middleware."
→ "Explain the middleware pipeline source code implementation."

// Learning style
"List 5 best practices for async/await."
→ "Show 5 anti-patterns to avoid with async/await."
→ "Create a decision tree for when to use async/await."
→ "Design a code review checklist for async/await."
```

---

### 2.4 Chat Category (Current: 25%)

#### Strategy: **Conversational Depth Layering**

```csharp
// Single turn → Multi-turn
"bruh adulting is a scam"
→ Expand into 3-turn conversation:
   User: "bruh adulting is a scam"
   AI: [response]
   User: "yeah but like how do people even manage"

// Topic branching
"What's your go-to takeaway order?"
→ "What's your go-to takeaway order and why does it hit different?"
→ "If you could only eat one cuisine for life, what would it be?"
```

**Note:** Chat seeds should stay authentic. Don't force educational content into casual chat.

---

## Tier 3: Implementation Roadmap

### Phase 1: Refactor Current System (Week 1)

**Goals:**
1. Remove broken code (`NotImplementedException` at line 343)
2. Consolidate weighted/unweighted methods
3. Make Kiwi augmentation opt-in, not default
4. Add augmentation strategy interface

**Files to modify:**
- `src/Services/SeedService.cs`

```csharp
// New interface
public interface IAugmentationStrategy
{
    IEnumerable<SeedItem> Augment(SeedItem baseSeed);
    string StrategyName { get; }
}

// Strategies
public class SemanticAugmentationStrategy : IAugmentationStrategy { }
public class CrossDomainStrategy : IAugmentationStrategy { }
public class DifficultyGradationStrategy : IAugmentationStrategy { }
```

---

### Phase 2: Implement Semantic Strategies (Week 2-3)

**Priority order:**
1. ✅ Cross-Language Translation (code category)
2. ✅ Difficulty Graduation (all categories)
3. ✅ Constraint Variation (code category)
4. ✅ Problem Inversion (instruction category)

**Success metrics:**
- Augmented seeds have <30% textual similarity to base
- Human reviewers can't easily identify augmentation source
- Model performance variance increases (indicating diverse testing)

---

### Phase 3: Quality Validation (Week 4)

**Validation process:**
1. Generate 100 augmented seeds
2. Manual review by domain expert
3. A/B test: Current vs. New augmentation
4. Measure: Seed diversity score (cosine similarity on embeddings)

**Quality gates:**
- ≥80% of augmented seeds deemed "meaningfully different"
- Average similarity score <0.5
- Category distribution within ±5% of target weights

---

### Phase 4: Production Deployment (Week 5)

**Migration strategy:**
1. Keep current augmentation as fallback
2. Add `Config.UseSemanticAugmentation` flag
3. Run both systems in parallel for 1 week
4. Compare evaluation quality
5. Deprecate old system if new system outperforms

---

## Configuration Changes

### New Config Options

```csharp
// Config.cs additions
public static class Config
{
    // Augmentation Strategy
    public const bool UseSemanticAugmentation = true;
    public const bool EnableKiwiAugmentation = false;  // Make opt-in

    // Quality Controls
    public const double MinSemanticDiversity = 0.5;  // Cosine similarity threshold
    public const int MaxAugmentationsPerSeed = 5;    // Prevent explosion

    // Strategy Weights
    public static readonly Dictionary<string, double> AugmentationStrategyWeights = new()
    {
        ["CrossDomain"] = 0.35,
        ["DifficultyGradation"] = 0.30,
        ["ConstraintVariation"] = 0.20,
        ["ProblemInversion"] = 0.15
    };
}
```

---

## Measurement & Validation

### How to Know If Improvements Work

#### 1. Diversity Metrics
```csharp
// Calculate average cosine similarity between augmented seeds
public double CalculateSeedDiversity(List<SeedItem> seeds)
{
    var embeddings = GetEmbeddings(seeds);
    var similarities = new List<double>();

    for (int i = 0; i < embeddings.Count; i++)
        for (int j = i + 1; j < embeddings.Count; j++)
            similarities.Add(CosineSimilarity(embeddings[i], embeddings[j]));

    return 1.0 - similarities.Average(); // Higher = more diverse
}
```

**Target:** Diversity score ≥0.6 (vs. current ~0.3)

#### 2. Model Discrimination
```csharp
// Better augmentation should reveal more model differences
public double CalculateModelDiscrimination(List<ModelResult> results)
{
    // Variance in model scores across augmented seeds
    // Higher variance = augmentation reveals capability gaps
    return results.GroupBy(r => r.BasePrompt)
                  .Select(g => Variance(g.Select(r => r.Score)))
                  .Average();
}
```

**Target:** Discrimination score ≥0.15 (vs. current ~0.08)

#### 3. Human Evaluation
Sample 50 augmented seeds, ask reviewers:
- "Is this a meaningful variation?" (Yes/No)
- "Does this test a different capability?" (Yes/No)
- "Would you use this in a real evaluation?" (Yes/No)

**Target:** ≥75% "Yes" on all questions

---

## Examples: Before & After

### Example 1: Code Seed

**Current Augmentation:**
```
Base: "Create a C# extension method to convert a string to title case."

Augmented:
1. "Build a C# extension method to convert a string to title case."
2. "Design a C# extension method to convert a string to title case."
3. "Create a C# extension method to convert a string to title case. Keep it under 200 words."
4. "Create a C# extension method to convert a string to title case. Include a minimal code example."
```

**Problem:** 90% similar, same capability tested 4 times.

---

**Proposed Augmentation:**
```
Base: "Create a C# extension method to convert a string to title case."

Augmented:
1. "Implement title case conversion in Python without using built-in methods."
2. "Write a title case extension that handles Unicode/emoji correctly."
3. "Create a title case converter that's culture-aware (Turkish İ/i problem)."
4. "Benchmark three approaches to title casing and explain performance differences."
5. "Write a title case converter as a Source Generator instead of runtime extension."
```

**Improvement:** <40% similar, tests 5 different capabilities (cross-language, edge cases, globalization, performance analysis, metaprogramming).

---

### Example 2: Support Seed

**Current Augmentation:**
```
Base: "I'm feeling overwhelmed with work deadlines. Any quick tips?"

Augmented:
1. "I'm feeling overwhelmed with work deadlines. Any quick tips? Keep it under 150 words."
2. "I'm feeling overwhelmed with work deadlines. Any quick tips? Use a warm, empathetic tone."
3. "I'm feeling overwhelmed with work deadlines. Any quick tips? End with a one-sentence encouragement."
```

**Problem:** Constraints don't change the support need. Still same emotional state.

---

**Proposed Augmentation:**
```
Base: "I'm feeling overwhelmed with work deadlines. Any quick tips?"

Augmented:
1. "I have three deadlines tomorrow and I'm paralyzed. What's the first step?" [Acute crisis]
2. "I've been overwhelmed at work for months. Is this burnout?" [Chronic pattern]
3. "My boss keeps adding deadlines. How do I push back?" [Boundary setting]
4. "Work stress is affecting my sleep. Any immediate coping strategies?" [Cross-domain impact]
5. "I'm overwhelmed but scared to ask for help. Where do I start?" [Stigma barrier]
```

**Improvement:** Each tests different support skills (crisis intervention, burnout recognition, assertiveness coaching, sleep hygiene, stigma reduction).

---

## Migration Checklist

### For Developers

- [ ] Review `SeedService.cs` lines 90-344 for refactoring opportunities
- [ ] Create `IAugmentationStrategy` interface
- [ ] Implement `SemanticAugmentationStrategy`
- [ ] Add diversity measurement tools
- [ ] Update tests to validate semantic diversity
- [ ] Add configuration toggles for A/B testing
- [ ] Document new augmentation strategies in code comments

### For Project Leads

- [ ] Decide on augmentation strategy priorities (cross-domain vs. difficulty vs. inversion)
- [ ] Set diversity score targets (recommended: 0.6+)
- [ ] Budget time for human validation review
- [ ] Plan A/B test window (recommended: 1-2 weeks)
- [ ] Define success criteria for keeping/deprecating old system

### For Evaluators

- [ ] Generate sample augmented seeds from new system
- [ ] Conduct blind comparison with current augmentation
- [ ] Rate seeds on: meaningfulness, diversity, evaluation value
- [ ] Provide feedback on category-specific strategies

---

## Risk Assessment

### Low Risk
- ✅ Backward compatibility (keep old system as fallback)
- ✅ Incremental rollout (feature flag controlled)
- ✅ No changes to base seeds (only augmentation logic)

### Medium Risk
- ⚠️ **Increased complexity** - More augmentation strategies = more code to maintain
  - *Mitigation:* Interface-based design, unit tests for each strategy

- ⚠️ **Longer generation time** - Semantic augmentation is more CPU-intensive
  - *Mitigation:* Cache augmented seeds, generate async, set timeouts

### High Risk
- 🚨 **Quality regression** - New augmentation could produce nonsense
  - *Mitigation:* Manual review gate, diversity score thresholds, gradual rollout

---

## Success Criteria (3-Month Evaluation)

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Seed Diversity Score | ~0.3 | ≥0.6 | Cosine similarity analysis |
| Model Discrimination | ~0.08 | ≥0.15 | Score variance across models |
| Human Quality Rating | ~60% | ≥80% | Blind review of 100 samples |
| Augmentation Strategies | 4 | 8+ | Count of implemented strategies |
| Code Debt | Medium | Low | Remove NotImplemented, consolidate methods |

---

## Future Enhancements (Beyond Initial Scope)

### 1. LLM-Assisted Augmentation
Use a small model to generate semantic variations:
```
Prompt: "Generate 5 variations of this task that test different skills: [base seed]"
```

### 2. Adversarial Augmentation
Create intentionally difficult variations:
```
"Create edge cases that commonly trip up LLMs for: [base seed]"
```

### 3. Multi-Modal Augmentation
For code seeds, include:
- Broken code to debug
- Code review scenarios
- Performance profiling challenges

### 4. Conversation Threading
Link related seeds into multi-turn conversations rather than one-shots.

---

## Conclusion

The current augmentation strategy is **structurally sound but tactically weak**. By shifting from syntactic variation to semantic transformation, Squirmify can achieve:

1. **Higher quality evaluation** - Diverse seeds reveal model capabilities more accurately
2. **Better ROI on base seeds** - 884 seeds → 5,000+ meaningfully different tests
3. **Reduced redundancy** - Stop testing the same capability with different phrasing
4. **Cleaner codebase** - Remove Kiwi gimmicks, consolidate methods, clear interfaces

**Recommended Action:** Implement Phase 1 (refactor) immediately, then pilot one semantic strategy (cross-domain for code category) to validate approach before full rollout.

---

**Questions or feedback?** Open an issue or PR on the repo.
