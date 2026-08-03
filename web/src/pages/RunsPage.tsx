import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  useRuns, useProviders, useConfigs, useModelGroups, useModels, useStartRun, useCancelRun,
} from '@/api/queries';
import { useBenchmarkHub } from '@/hooks/useBenchmarkHub';
import { Play, StopCircle, Radio } from 'lucide-react';

const statusColor = (status: string) => {
  switch (status) {
    case 'completed': return 'default' as const;
    case 'running': return 'secondary' as const;
    case 'failed': case 'cancelled': return 'destructive' as const;
    default: return 'outline' as const;
  }
};

export function RunsPage() {
  const { data: runs, isLoading } = useRuns();
  const { data: providers } = useProviders();
  const { data: configs } = useConfigs();
  const { data: modelGroups } = useModelGroups();
  const startRun = useStartRun();
  const cancelRun = useCancelRun();
  const [open, setOpen] = useState(false);
  const [providerId, setProviderId] = useState('');
  const [modelGroupId, setModelGroupId] = useState('');
  const [configId, setConfigId] = useState('');
  const [judgeModelId, setJudgeModelId] = useState('');
  const [watchingRunId, setWatchingRunId] = useState<number | null>(null);

  const { data: providerModels } = useModels(providerId ? Number(providerId) : undefined);

  const hub = useBenchmarkHub(watchingRunId);

  const handleStart = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    startRun.mutate({
      configId: Number(configId),
      providerId: Number(providerId),
      modelGroupId: modelGroupId ? Number(modelGroupId) : undefined,
      judgeModelId: judgeModelId ? Number(judgeModelId) : undefined,
      name: (fd.get('name') as string) || undefined,
    }, { onSuccess: () => setOpen(false) });
  };

  // RECOVERY NOTE (2026-08-04): this was live in the surviving source but
  // unused, and noUnusedLocals rejects it. The hub is already driven by
  // `watchingRunId` above, so this looks like a superseded auto-watch
  // approach. Commented rather than deleted — it may have been mid-change
  // when the files were lost, and the original intent cannot be recovered.
  // const activeRun = runs?.find((r) => r.status === 'running');

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Benchmark Runs</h1>
          <p className="text-muted-foreground">Launch and monitor benchmark evaluations</p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button><Play className="mr-2 h-4 w-4" /> New Run</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Start Benchmark Run</DialogTitle></DialogHeader>
            <form onSubmit={handleStart} className="space-y-4">
              <div className="space-y-2">
                <Label>Name (optional)</Label>
                <Input name="name" placeholder="e.g. Local models Q1 2026" />
              </div>
              <div className="space-y-2">
                <Label>Provider</Label>
                <Select value={providerId} onValueChange={(v) => { setProviderId(v); setModelGroupId(''); }}>
                  <SelectTrigger><SelectValue placeholder="Select provider" /></SelectTrigger>
                  <SelectContent>
                    {providers?.map((p) => (
                      <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {(modelGroups?.length ?? 0) > 0 && (
                <div className="space-y-2">
                  <Label>Model Group (optional, overrides provider model list)</Label>
                  <Select value={modelGroupId} onValueChange={setModelGroupId}>
                    <SelectTrigger><SelectValue placeholder="All models from provider" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">All models from provider</SelectItem>
                      {modelGroups?.map((g) => (
                        <SelectItem key={g.id} value={g.id.toString()}>{g.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="space-y-2">
                <Label>Configuration</Label>
                <Select value={configId} onValueChange={setConfigId}>
                  <SelectTrigger><SelectValue placeholder="Select config" /></SelectTrigger>
                  <SelectContent>
                    {configs?.map((c) => (
                      <SelectItem key={c.id} value={c.id.toString()}>{c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {providerId && (providerModels?.filter((m) => !m.isDeleted && !m.isDisabled)?.length ?? 0) > 0 && (
                <div className="space-y-2">
                  <Label>Judge Model (trusted model for scoring)</Label>
                  <Select value={judgeModelId} onValueChange={setJudgeModelId}>
                    <SelectTrigger><SelectValue placeholder="Auto-select (top performer)" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="">Auto-select</SelectItem>
                      {providerModels?.filter((m) => !m.isDeleted && !m.isDisabled).map((m) => (
                        <SelectItem key={m.id} value={m.id.toString()}>{m.identifier}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <DialogFooter>
                <Button type="submit" disabled={startRun.isPending || !providerId || !configId}>
                  {startRun.isPending ? 'Starting...' : 'Start Run'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      {/* Live progress panel */}
      {hub.isConnected && hub.progress && (
        <Card className="border-squirmify/50">
          <CardContent className="py-4 space-y-3">
            <div className="flex items-center gap-2">
              <Radio className="h-4 w-4 text-squirmify animate-pulse" />
              <span className="font-medium">Live: {hub.progress.stage}</span>
              <Badge variant="secondary">{hub.progress.percentComplete}%</Badge>
            </div>
            {hub.progress.currentModel && (
              <p className="text-sm text-muted-foreground">
                Model {hub.progress.currentModelIndex + 1}/{hub.progress.totalModels}: {hub.progress.currentModel}
                {hub.progress.currentTest && <> — {hub.progress.currentTest}</>}
              </p>
            )}
            <div className="h-2 rounded-full bg-muted overflow-hidden">
              <div
                className="h-full bg-squirmify transition-all duration-500"
                style={{ width: `${hub.progress.percentComplete}%` }}
              />
            </div>
            {hub.logs.length > 0 && (
              <div className="max-h-32 overflow-y-auto rounded border bg-muted/30 p-2 text-xs font-mono space-y-0.5">
                {hub.logs.slice(-10).map((log, i) => (
                  <div key={i} className={log.level === 'error' ? 'text-destructive' : 'text-muted-foreground'}>
                    {log.message}
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <p className="text-muted-foreground">Loading...</p>
      ) : (
        <div className="space-y-3">
          {runs?.map((run) => (
            <Card key={run.id} className={watchingRunId === run.id ? 'border-squirmify/50' : ''}>
              <CardContent className="flex items-center justify-between py-4">
                <div>
                  <div className="flex items-center gap-3">
                    <span className="font-medium">{run.name ?? `Run #${run.id}`}</span>
                    <Badge variant={statusColor(run.status)}>{run.status}</Badge>
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {run.totalModels} models &middot; {run.completedTests}/{run.totalTests} tests
                    {run.errorCount > 0 && <span className="text-destructive"> &middot; {run.errorCount} errors</span>}
                    {run.startedAt && <span> &middot; {new Date(run.startedAt).toLocaleString()}</span>}
                  </div>
                </div>
                <div className="flex gap-2">
                  {run.status === 'running' && (
                    <>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setWatchingRunId(watchingRunId === run.id ? null : run.id)}
                      >
                        <Radio className="mr-1 h-3 w-3" /> {watchingRunId === run.id ? 'Unwatch' : 'Watch'}
                      </Button>
                      <Button variant="destructive" size="sm" onClick={() => cancelRun.mutate(run.id)}>
                        <StopCircle className="mr-1 h-3 w-3" /> Cancel
                      </Button>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
          {runs?.length === 0 && (
            <p className="text-center text-muted-foreground py-8">No runs yet. Start your first benchmark.</p>
          )}
        </div>
      )}
    </div>
  );
}
