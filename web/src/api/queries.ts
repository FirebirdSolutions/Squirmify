import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type {
  Provider, Model, ModelGroup, ModelGroupDetail, TestSuiteConfig, BenchmarkRun,
  InstructionTest, ReasoningTest, ConversationTest, ConversationTurn, ConversationJudgingCriterion,
  ContextWindowTest, ContextWindowCheckpoint, McpToolTest,
  Seed, RunAnalysis, RunComparison,
} from './types';

// === Model Groups ===
export const useModelGroups = () =>
  useQuery({ queryKey: ['model-groups'], queryFn: () => api.get<ModelGroup[]>('/model-groups') });

export const useModelGroup = (id: number) =>
  useQuery({
    queryKey: ['model-groups', id],
    queryFn: () => api.get<ModelGroupDetail>(`/model-groups/${id}`),
    enabled: id > 0,
  });

export const useCreateModelGroup = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; description?: string; modelIds?: number[] }) =>
      api.post<ModelGroup>('/model-groups', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['model-groups'] }),
  });
};

export const useUpdateModelGroup = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: number; name?: string; description?: string; modelIds?: number[] }) =>
      api.patch<void>(`/model-groups/${id}`, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['model-groups'] }),
  });
};

export const useDeleteModelGroup = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/model-groups/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['model-groups'] }),
  });
};

// === Providers ===
export const useProviders = () =>
  useQuery({ queryKey: ['providers'], queryFn: () => api.get<Provider[]>('/providers') });

export const useProvider = (id: number) =>
  useQuery({ queryKey: ['providers', id], queryFn: () => api.get<Provider>(`/providers/${id}`) });

export const useCreateProvider = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<Provider>) => api.post<Provider>('/providers', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['providers'] }),
  });
};

export const useUpdateProvider = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: Partial<Provider> & { id: number }) =>
      api.patch<void>(`/providers/${id}`, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['providers'] }),
  });
};

export const useDeleteProvider = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/providers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['providers'] }),
  });
};

export const useSyncModels = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (providerId: number) => api.post<{ models: string[]; count: number }>(`/providers/${providerId}/sync-models`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['models'] }),
  });
};

// === Models ===
export const useModels = (providerId?: number) =>
  useQuery({
    queryKey: ['models', providerId],
    queryFn: () => api.get<Model[]>(providerId ? `/models/provider/${providerId}` : '/models'),
  });

export const useToggleModelDisabled = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, disabled }: { id: number; disabled: boolean }) =>
      api.patch<void>(`/models/${id}/disabled`, { value: disabled }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['models'] }),
  });
};

// === Configs ===
export const useConfigs = () =>
  useQuery({ queryKey: ['configs'], queryFn: () => api.get<TestSuiteConfig[]>('/configs') });

export const useConfig = (id: number) =>
  useQuery({ queryKey: ['configs', id], queryFn: () => api.get(`/configs/${id}`), enabled: id > 0 });

export const useCreateConfig = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: unknown) => api.post<TestSuiteConfig>('/configs', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['configs'] }),
  });
};

export const useDeleteConfig = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/configs/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['configs'] }),
  });
};

// === Tests ===
export const useInstructionTests = () =>
  useQuery({ queryKey: ['tests', 'instruction'], queryFn: () => api.get<InstructionTest[]>('/tests/instruction') });

export const useReasoningTests = () =>
  useQuery({ queryKey: ['tests', 'reasoning'], queryFn: () => api.get<ReasoningTest[]>('/tests/reasoning') });

export const useConversationTests = () =>
  useQuery({ queryKey: ['tests', 'conversation'], queryFn: () => api.get<ConversationTest[]>('/tests/conversation') });

export const useContextWindowTests = () =>
  useQuery({ queryKey: ['tests', 'context-window'], queryFn: () => api.get<ContextWindowTest[]>('/tests/context-window') });

export const useMcpToolTests = () =>
  useQuery({ queryKey: ['tests', 'mcp-tool'], queryFn: () => api.get<McpToolTest[]>('/tests/mcp-tool') });

// Detail fetchers for composite tests
export const useConversationTestDetail = (id: number) =>
  useQuery({
    queryKey: ['tests', 'conversation', id],
    queryFn: () => api.get<{ test: ConversationTest; turns: ConversationTurn[]; criteria: ConversationJudgingCriterion[] }>(`/tests/conversation/${id}`),
    enabled: id > 0,
  });

export const useContextWindowTestDetail = (id: number) =>
  useQuery({
    queryKey: ['tests', 'context-window', id],
    queryFn: () => api.get<{ test: ContextWindowTest; checkpoints: ContextWindowCheckpoint[] }>(`/tests/context-window/${id}`),
    enabled: id > 0,
  });

export const useCreateConversationTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: { test: Partial<ConversationTest>; turns: Partial<ConversationTurn>[]; criteria: Partial<ConversationJudgingCriterion>[] }) =>
      api.post<ConversationTest>('/tests/conversation', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'conversation'] }),
  });
};

export const useDeleteConversationTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/tests/conversation/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'conversation'] }),
  });
};

export const useCreateContextWindowTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: { test: Partial<ContextWindowTest>; checkpoints: Partial<ContextWindowCheckpoint>[] }) =>
      api.post<ContextWindowTest>('/tests/context-window', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'context-window'] }),
  });
};

export const useCreateInstructionTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<InstructionTest>) => api.post<InstructionTest>('/tests/instruction', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'instruction'] }),
  });
};

export const useDeleteInstructionTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/tests/instruction/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'instruction'] }),
  });
};

export const useCreateReasoningTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<ReasoningTest>) => api.post<ReasoningTest>('/tests/reasoning', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'reasoning'] }),
  });
};

export const useDeleteReasoningTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/tests/reasoning/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'reasoning'] }),
  });
};

export const useCreateMcpToolTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<McpToolTest>) => api.post<McpToolTest>('/tests/mcp-tool', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'mcp-tool'] }),
  });
};

export const useDeleteMcpToolTest = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<void>(`/tests/mcp-tool/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['tests', 'mcp-tool'] }),
  });
};

// === Seeds ===
export const useSeeds = () =>
  useQuery({ queryKey: ['seeds'], queryFn: () => api.get<Seed[]>('/seeds') });

export const useSeedStats = () =>
  useQuery({ queryKey: ['seeds', 'stats'], queryFn: () => api.get<{ baseCount: number; augmentedCount: number; total: number }>('/seeds/stats') });

// === Runs ===
export const useRuns = (count = 20) =>
  useQuery({ queryKey: ['runs', count], queryFn: () => api.get<BenchmarkRun[]>(`/runs?count=${count}`) });

export const useRun = (id: number) =>
  useQuery({ queryKey: ['runs', id], queryFn: () => api.get(`/runs/${id}`), enabled: id > 0 });

export const useStartRun = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: { configId: number; providerId: number; modelGroupId?: number; judgeModelId?: number; name?: string }) =>
      api.post('/runs', data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  });
};

export const useCancelRun = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.post(`/runs/${id}/cancel`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  });
};

export const useRunResults = (id: number) =>
  useQuery({
    queryKey: ['runs', id, 'results'],
    queryFn: () => api.get<RunAnalysis>(`/runs/${id}/results`),
    enabled: id > 0,
  });

// === Analysis ===
export const useCompareRuns = () =>
  useMutation({
    mutationFn: (runIds: number[]) => api.post<RunComparison>('/analysis/compare', { runIds }),
  });
