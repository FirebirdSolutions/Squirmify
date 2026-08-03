import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  useProviders, useModels, useModelGroups, useConfigs, useRuns,
  useInstructionTests, useReasoningTests, useConversationTests,
  useContextWindowTests, useMcpToolTests, useSeedStats,
} from '@/api/queries';
import { Link } from '@tanstack/react-router';

function StatCard({ label, value, href }: { label: string; value: string | number; href?: string }) {
  const content = (
    <Card className={href ? 'hover:border-squirmify/50 transition-colors' : ''}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="text-3xl font-bold">{value}</div>
      </CardContent>
    </Card>
  );
  return href ? <Link to={href}>{content}</Link> : content;
}

export function DashboardPage() {
  const { data: providers } = useProviders();
  const { data: models } = useModels();
  const { data: modelGroups } = useModelGroups();
  const { data: configs } = useConfigs();
  const { data: runs } = useRuns(5);
  const { data: instructionTests } = useInstructionTests();
  const { data: reasoningTests } = useReasoningTests();
  const { data: conversationTests } = useConversationTests();
  const { data: contextWindowTests } = useContextWindowTests();
  const { data: mcpToolTests } = useMcpToolTests();
  const { data: seedStats } = useSeedStats();

  const totalTests = (instructionTests?.length ?? 0) + (reasoningTests?.length ?? 0)
    + (conversationTests?.length ?? 0) + (contextWindowTests?.length ?? 0) + (mcpToolTests?.length ?? 0);
  const activeModels = models?.filter((m) => !m.isDeleted && !m.isDisabled).length ?? 0;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <p className="text-muted-foreground">LLM evaluation at a glance</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
        <StatCard label="Providers" value={providers?.length ?? '—'} href="/providers" />
        <StatCard label="Models" value={activeModels} href="/models" />
        <StatCard label="Model Groups" value={modelGroups?.length ?? '—'} href="/model-groups" />
        <StatCard label="Tests" value={totalTests} href="/tests" />
        <StatCard label="Seeds" value={seedStats?.total ?? '—'} href="/seeds" />
        <StatCard label="Configs" value={configs?.length ?? '—'} href="/configs" />
      </div>

      {/* Test breakdown */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Tests by Type</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-2">
              {[
                { label: 'Instruction', count: instructionTests?.length ?? 0 },
                { label: 'Reasoning', count: reasoningTests?.length ?? 0 },
                { label: 'Conversation', count: conversationTests?.length ?? 0 },
                { label: 'Context Window', count: contextWindowTests?.length ?? 0 },
                { label: 'MCP Tool', count: mcpToolTests?.length ?? 0 },
              ].map(({ label, count }) => (
                <div key={label} className="flex items-center justify-between">
                  <span className="text-sm">{label}</span>
                  <div className="flex items-center gap-2">
                    <div className="h-2 rounded-full bg-squirmify" style={{ width: `${Math.max(4, (count / Math.max(totalTests, 1)) * 200)}px` }} />
                    <span className="text-sm font-medium w-8 text-right">{count}</span>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Recent Runs</CardTitle></CardHeader>
          <CardContent>
            {runs && runs.length > 0 ? (
              <div className="space-y-2">
                {runs.map((run) => (
                  <div key={run.id} className="flex items-center justify-between rounded-md border p-3">
                    <div>
                      <span className="font-medium">{run.name ?? `Run #${run.id}`}</span>
                      <span className="ml-3 text-sm text-muted-foreground">
                        {run.totalModels} models &middot; {run.completedTests}/{run.totalTests} tests
                      </span>
                    </div>
                    <Badge variant={run.status === 'completed' ? 'default' : run.status === 'running' ? 'secondary' : 'destructive'}>
                      {run.status}
                    </Badge>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-muted-foreground">
                No runs yet. <Link to="/runs" className="text-squirmify underline">Start one</Link>
              </p>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
