// Model Groups
export interface ModelGroup {
  id: number;
  name: string;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface ModelGroupDetail {
  group: ModelGroup;
  models: Model[];
}

// Provider & Model
export interface Provider {
  id: number;
  name: string;
  baseUrl: string;
  authToken?: string;
  useAuth: boolean;
  timeoutMinutes: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface Model {
  id: number;
  providerId: number;
  identifier: string;
  displayName?: string;
  isDisabled: boolean;
  isAvailable: boolean;
  isDeleted: boolean;
  canTest: boolean;
  createdAt: string;
}

// Test Suite Config
export interface TestSuiteConfig {
  id: number;
  name: string;
  description?: string;
  runPromptTests: boolean;
  runContextWindowTests: boolean;
  runConversationTests: boolean;
  runQualificationTests: boolean;
  maxInstructionTests: number;
  maxReasoningTests: number;
  maxConversationTests: number;
  highQualityThreshold: number;
  instructionPassThreshold: number;
  topJudgeCount: number;
  runMcpToolTests: boolean;
  maxMcpToolTests: number;
  echoMcpBaseUrl?: string;
  echoMcpToken?: string;
  fetchSchemasFromEchoMcp: boolean;
  mcpTransportType: string;
  mcpServerUrl?: string;
  mcpServerCommand?: string;
  mcpServerArgs?: string;
  contextWindowLevel: string;
  contextWindowTestType: string;
  contextWindowTargetTokens: number;
  contextWindowProbeCount: number;
  contextWindowCheckpoints: number;
  contextWindowMaxTests: number;
  targetSeedCount: number;
  overwriteSeeds: boolean;
  globalTemperature: number;
  globalTopP: number;
  globalMaxTokens: number;
  maxParallelRequests: number;
  createdAt: string;
  updatedAt?: string;
}

export interface CategorySetting {
  id: number;
  configId: number;
  category: string;
  temperature?: number;
  topP?: number;
  maxTokens?: number;
  systemPrompt?: string;
  weight: number;
}

export interface TestTypeLimit {
  id: number;
  configId: number;
  testType: string;
  category: string;
  maxTests: number;
  temperature?: number;
  topP?: number;
  maxTokens?: number;
  passThreshold?: number;
  minScore?: number;
}

// Test Definitions
export interface InstructionTest {
  id: number;
  category: string;
  prompt: string;
  expectedResult: string;
  validationType: string;
  strictOrder: boolean;
  isActive: boolean;
  excludePatterns?: string;
  allowedValues?: string;
  expectedCount?: number;
  createdAt: string;
}

export interface ReasoningTest {
  id: number;
  category: string;
  description?: string;
  prompt: string;
  correctAnswer: string;
  isActive: boolean;
  createdAt: string;
}

export interface ConversationTest {
  id: number;
  category: string;
  description?: string;
  systemPrompt?: string;
  isActive: boolean;
  createdAt: string;
}

export interface ConversationTurn {
  id: number;
  testId: number;
  turnNumber: number;
  userMessage: string;
  expectedTheme?: string;
}

export interface ConversationJudgingCriterion {
  id: number;
  testId: number;
  criterion: string;
  sortOrder: number;
}

export interface ContextWindowTest {
  id: number;
  name: string;
  description?: string;
  fillerType: string;
  baseTargetTokens: number;
  baseCheckpointCount: number;
  buriedInstruction?: string;
  needleComplexity: string;
  isActive: boolean;
  createdAt: string;
}

export interface ContextWindowCheckpoint {
  id: number;
  testId: number;
  targetTokenPosition: number;
  relativePosition?: number;
  secretWord: string;
  carrierSentence?: string;
  sortOrder: number;
}

export interface McpToolTest {
  id: number;
  category: string;
  description: string;
  toolName: string;
  command: string;
  toolSchema?: string;
  scenarioPrompt: string;
  expectedParams?: string;
  responseValidationType: string;
  expectedResponsePatterns?: string;
  executeTool: boolean;
  isActive: boolean;
  createdAt: string;
}

// Seeds
export interface Seed {
  id: number;
  category: string;
  instruction: string;
  temperature?: number;
  topP?: number;
  maxTokens?: number;
  isAugmented: boolean;
  sourceSeedId?: number;
  createdAt: string;
}

// Benchmark Runs
export interface BenchmarkRun {
  id: number;
  name?: string;
  configId: number;
  providerId: number;
  modelGroupId?: number;
  status: string;
  startedAt?: string;
  completedAt?: string;
  totalModels: number;
  totalTests: number;
  completedTests: number;
  errorCount: number;
  baseJudgeModelId?: number;
  createdAt: string;
}

export interface BenchmarkRunModel {
  id: number;
  runId: number;
  modelId: number;
  status: string;
  qualificationPassed?: boolean;
  instructionPassRate?: number;
  instructionStrictPassRate?: number;
  reasoningAvgScore?: number;
  contextWindowAvgReliability?: number;
  contextWindowAvgAccuracy?: number;
  contextWindowTestCount: number;
  isBaseJudge: boolean;
  isAutoJudge: boolean;
}

// Progress
export interface RunProgress {
  runId: number;
  stage: string;
  currentModelIndex: number;
  totalModels: number;
  currentTestIndex: number;
  totalTests: number;
  currentModel?: string;
  currentTest?: string;
  percentComplete: number;
  recentEvents: string[];
}

export interface LogEvent {
  runId: number;
  timestamp: string;
  level: string;
  message: string;
  modelName?: string;
}

// Analysis
export interface RunAnalysis {
  runId: number;
  runName?: string;
  startedAt?: string;
  completedAt?: string;
  duration: string;
  overall: OverallSummary;
  modelSummaries: ModelSummary[];
  instructionAnalysis: InstructionAnalysis;
  reasoningAnalysis: ReasoningAnalysis;
  conversationAnalysis: ConversationAnalysis;
  contextWindowAnalysis: ContextWindowAnalysis;
  performance: PerformanceAnalysis;
}

export interface OverallSummary {
  totalTests: number;
  completedTests: number;
  modelCount: number;
  instructionPassRate: number;
  reasoningAvgScore: number;
  conversationAvgScore: number;
  generationAvgScore: number;
  highQualityCount: number;
  compositeScore: number;
}

export interface ModelSummary {
  modelId: number;
  modelName: string;
  instructionPassRate: number;
  reasoningAvgScore: number;
  conversationAvgScore: number;
  generationAvgScore: number;
  compositeScore: number;
  avgTokensPerSec: number;
  instructionAvgTokensPerSec: number;
  generationAvgTokensPerSec: number;
  avgLatencyMs: number;
  totalTests: number;
  passedTests: number;
}

export interface InstructionAnalysis {
  totalTests: number;
  passedTests: number;
  strictPassedTests: number;
  passRate: number;
  strictPassRate: number;
  byCategory: CategoryBreakdown[];
  topFailures: FailureReason[];
  byValidationType: ValidationTypeBreakdown[];
}

export interface CategoryBreakdown {
  category: string;
  total: number;
  passed: number;
  passRate: number;
  avgLatencyMs: number;
}

export interface FailureReason {
  reason: string;
  count: number;
  percentage: number;
}

export interface ValidationTypeBreakdown {
  validationType: string;
  total: number;
  passed: number;
  passRate: number;
}

export interface ReasoningAnalysis {
  totalTests: number;
  avgOverallScore: number;
  avgCorrectAnswerScore: number;
  avgLogicalStepsScore: number;
  avgClarityScore: number;
  byCategory: CategoryBreakdown[];
}

export interface ConversationAnalysis {
  totalTests: number;
  avgOverallScore: number;
  avgTopicCoherence: number;
  avgConversationalTone: number;
  avgContextRetention: number;
  avgHelpfulness: number;
  byCategory: CategoryBreakdown[];
}

export interface ContextWindowAnalysis {
  totalTests: number;
  maxReliableTokensAvg: number;
  maxReliableTokensMax: number;
  maxReliableTokensMin: number;
  avgCheckpointAccuracy: number;
  degradationPatterns: Record<string, number>;
  byModel: ModelContextSummary[];
}

export interface ModelContextSummary {
  modelId: number;
  modelName: string;
  maxReliableTokens: number;
  checkpointAccuracy: number;
  degradationPattern: string;
}

export interface PerformanceAnalysis {
  avgTokensPerSec: number;
  maxTokensPerSec: number;
  minTokensPerSec: number;
  instructionAvgTokensPerSec: number;
  generationAvgTokensPerSec: number;
  avgLatencyMs: number;
  p50LatencyMs: number;
  p95LatencyMs: number;
  p99LatencyMs: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  byModel: ModelPerformance[];
}

export interface ModelPerformance {
  modelId: number;
  modelName: string;
  avgTokensPerSec: number;
  instructionAvgTokensPerSec: number;
  generationAvgTokensPerSec: number;
  avgLatencyMs: number;
  totalRequests: number;
}

export interface RunComparison {
  runs: RunComparisonEntry[];
  modelComparisons: ModelComparisonAcrossRuns[];
}

export interface RunComparisonEntry {
  runId: number;
  runName?: string;
  startedAt?: string;
  instructionPassRate: number;
  reasoningAvgScore: number;
  conversationAvgScore: number;
  generationAvgScore: number;
  compositeScore: number;
  avgTokensPerSec: number;
  modelCount: number;
}

export interface ModelComparisonAcrossRuns {
  modelId: number;
  modelName: string;
  runScores: ModelRunScore[];
}

export interface ModelRunScore {
  runId: number;
  instructionPassRate: number;
  reasoningAvgScore: number;
  conversationAvgScore: number;
  compositeScore: number;
  delta?: number;
}
