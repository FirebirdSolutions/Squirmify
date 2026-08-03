import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { useConfigs, useCreateConfig, useDeleteConfig } from '@/api/queries';
import { Plus, Trash2 } from 'lucide-react';

function Checkbox({ id, label, defaultChecked }: { id: string; label: string; defaultChecked?: boolean }) {
  return (
    <div className="flex items-center gap-2">
      <input type="checkbox" id={id} name={id} defaultChecked={defaultChecked} className="h-4 w-4 rounded border-border" />
      <Label htmlFor={id} className="font-normal">{label}</Label>
    </div>
  );
}

function NumberField({ id, label, defaultValue, min, max, step }: {
  id: string; label: string; defaultValue?: number; min?: number; max?: number; step?: number;
}) {
  return (
    <div className="space-y-1">
      <Label htmlFor={id} className="text-xs">{label}</Label>
      <Input id={id} name={id} type="number" defaultValue={defaultValue} min={min} max={max} step={step} className="h-8" />
    </div>
  );
}

export function ConfigsPage() {
  const { data: configs, isLoading } = useConfigs();
  const createConfig = useCreateConfig();
  const deleteConfig = useDeleteConfig();
  const [open, setOpen] = useState(false);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const str = (key: string) => (fd.get(key) as string) || undefined;
    const num = (key: string, fallback: number) => {
      const v = fd.get(key) as string;
      return v ? Number(v) : fallback;
    };
    const bool = (key: string) => fd.get(key) === 'on';

    createConfig.mutate({
      name: str('name'),
      description: str('description'),
      runPromptTests: bool('runPromptTests'),
      runConversationTests: bool('runConversationTests'),
      runContextWindowTests: bool('runContextWindowTests'),
      runQualificationTests: bool('runQualificationTests'),
      runMcpToolTests: bool('runMcpToolTests'),
      maxInstructionTests: num('maxInstructionTests', 50),
      maxReasoningTests: num('maxReasoningTests', 20),
      maxConversationTests: num('maxConversationTests', 10),
      maxMcpToolTests: num('maxMcpToolTests', 10),
      instructionPassThreshold: num('instructionPassThreshold', 0.7),
      highQualityThreshold: num('highQualityThreshold', 7),
      topJudgeCount: num('topJudgeCount', 3),
      globalTemperature: num('globalTemperature', 0.7),
      globalTopP: num('globalTopP', 0.9),
      globalMaxTokens: num('globalMaxTokens', 2048),
      maxParallelRequests: num('maxParallelRequests', 3),
      targetSeedCount: num('targetSeedCount', 100),
      overwriteSeeds: bool('overwriteSeeds'),
      contextWindowLevel: str('contextWindowLevel') || 'medium',
      contextWindowTestType: str('contextWindowTestType') || 'buried_instruction',
      contextWindowTargetTokens: num('contextWindowTargetTokens', 32000),
      contextWindowProbeCount: num('contextWindowProbeCount', 5),
      contextWindowCheckpoints: num('contextWindowCheckpoints', 4),
      contextWindowMaxTests: num('contextWindowMaxTests', 3),
    }, { onSuccess: () => setOpen(false) });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Configurations</h1>
          <p className="text-muted-foreground">Test suite configurations for benchmark runs</p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" /> New Config</Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
            <DialogHeader><DialogTitle>Create Configuration</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-6">
              {/* Basic info */}
              <div className="space-y-3">
                <div className="space-y-2">
                  <Label htmlFor="name">Name</Label>
                  <Input id="name" name="name" placeholder="e.g. Full Suite" required />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="description">Description</Label>
                  <Textarea id="description" name="description" placeholder="What this config tests..." rows={2} />
                </div>
              </div>

              <Separator />

              {/* Test types */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Test Types</h3>
                <div className="grid grid-cols-2 gap-3">
                  <Checkbox id="runPromptTests" label="Instruction & Reasoning" defaultChecked />
                  <Checkbox id="runConversationTests" label="Conversation" defaultChecked />
                  <Checkbox id="runContextWindowTests" label="Context Window" />
                  <Checkbox id="runQualificationTests" label="Qualification" defaultChecked />
                  <Checkbox id="runMcpToolTests" label="MCP Tools" />
                </div>
              </div>

              <Separator />

              {/* Limits */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Test Limits</h3>
                <div className="grid grid-cols-3 gap-3">
                  <NumberField id="maxInstructionTests" label="Max instruction" defaultValue={50} min={1} />
                  <NumberField id="maxReasoningTests" label="Max reasoning" defaultValue={20} min={1} />
                  <NumberField id="maxConversationTests" label="Max conversation" defaultValue={10} min={1} />
                  <NumberField id="maxMcpToolTests" label="Max MCP tool" defaultValue={10} min={1} />
                  <NumberField id="topJudgeCount" label="Top judge count" defaultValue={3} min={1} max={10} />
                </div>
              </div>

              <Separator />

              {/* Thresholds */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Thresholds</h3>
                <div className="grid grid-cols-3 gap-3">
                  <NumberField id="instructionPassThreshold" label="Instruction pass %" defaultValue={0.7} min={0} max={1} step={0.05} />
                  <NumberField id="highQualityThreshold" label="HQ threshold (1-10)" defaultValue={7} min={1} max={10} />
                </div>
              </div>

              <Separator />

              {/* Generation params */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Generation Parameters</h3>
                <div className="grid grid-cols-3 gap-3">
                  <NumberField id="globalTemperature" label="Temperature" defaultValue={0.7} min={0} max={2} step={0.1} />
                  <NumberField id="globalTopP" label="Top P" defaultValue={0.9} min={0} max={1} step={0.05} />
                  <NumberField id="globalMaxTokens" label="Max tokens" defaultValue={2048} min={64} />
                  <NumberField id="maxParallelRequests" label="Parallel requests" defaultValue={3} min={1} max={20} />
                </div>
              </div>

              <Separator />

              {/* Seeds */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Seeds</h3>
                <div className="grid grid-cols-3 gap-3">
                  <NumberField id="targetSeedCount" label="Target seed count" defaultValue={100} min={10} />
                  <Checkbox id="overwriteSeeds" label="Overwrite existing seeds" />
                </div>
              </div>

              <Separator />

              {/* Context window */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold">Context Window</h3>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="contextWindowLevel" className="text-xs">Level</Label>
                    <Select name="contextWindowLevel" defaultValue="medium">
                      <SelectTrigger className="h-8"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="light">Light</SelectItem>
                        <SelectItem value="medium">Medium</SelectItem>
                        <SelectItem value="heavy">Heavy</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="contextWindowTestType" className="text-xs">Test type</Label>
                    <Select name="contextWindowTestType" defaultValue="buried_instruction">
                      <SelectTrigger className="h-8"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="buried_instruction">Buried Instruction</SelectItem>
                        <SelectItem value="needle_in_haystack">Needle in Haystack</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <NumberField id="contextWindowTargetTokens" label="Target tokens" defaultValue={32000} min={1000} step={1000} />
                  <NumberField id="contextWindowProbeCount" label="Probe count" defaultValue={5} min={1} />
                  <NumberField id="contextWindowCheckpoints" label="Checkpoints" defaultValue={4} min={1} />
                  <NumberField id="contextWindowMaxTests" label="Max tests" defaultValue={3} min={1} />
                </div>
              </div>

              <DialogFooter>
                <Button type="submit" disabled={createConfig.isPending}>
                  {createConfig.isPending ? 'Creating...' : 'Create'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      {isLoading ? (
        <p className="text-muted-foreground">Loading...</p>
      ) : (
        <div className="grid gap-4">
          {configs?.map((config) => (
            <Card key={config.id}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle>{config.name}</CardTitle>
                <Button variant="ghost" size="sm" onClick={() => deleteConfig.mutate(config.id)}>
                  <Trash2 className="h-3 w-3 text-destructive" />
                </Button>
              </CardHeader>
              <CardContent>
                {config.description && <p className="text-sm text-muted-foreground mb-3">{config.description}</p>}
                <div className="flex flex-wrap gap-2">
                  {config.runQualificationTests && <Badge>Qualification</Badge>}
                  {config.runPromptTests && <Badge>Prompts</Badge>}
                  {config.runConversationTests && <Badge>Conversation</Badge>}
                  {config.runContextWindowTests && <Badge>Context Window</Badge>}
                  {config.runMcpToolTests && <Badge>MCP Tools</Badge>}
                </div>
                <div className="mt-2 text-xs text-muted-foreground">
                  Pass threshold: {(config.instructionPassThreshold * 100)}% &middot;
                  HQ threshold: {config.highQualityThreshold} &middot;
                  Temp: {config.globalTemperature} &middot;
                  Parallel: {config.maxParallelRequests}
                </div>
              </CardContent>
            </Card>
          ))}
          {configs?.length === 0 && (
            <p className="text-center text-muted-foreground py-8">No configurations yet. Create one to run benchmarks.</p>
          )}
        </div>
      )}
    </div>
  );
}
