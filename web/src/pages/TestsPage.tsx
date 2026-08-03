import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  useInstructionTests, useReasoningTests, useConversationTests,
  useContextWindowTests, useMcpToolTests,
  useCreateInstructionTest, useDeleteInstructionTest,
  useCreateReasoningTest, useDeleteReasoningTest,
  useCreateConversationTest, useDeleteConversationTest,
  useCreateContextWindowTest,
  useCreateMcpToolTest, useDeleteMcpToolTest,
} from '@/api/queries';
import { Plus, Trash2, X } from 'lucide-react';

export function TestsPage() {
  const { data: instructionTests } = useInstructionTests();
  const { data: reasoningTests } = useReasoningTests();
  const { data: conversationTests } = useConversationTests();
  const { data: contextWindowTests } = useContextWindowTests();
  const { data: mcpToolTests } = useMcpToolTests();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Test Definitions</h1>
        <p className="text-muted-foreground">Tests that models are evaluated against</p>
      </div>

      <Tabs defaultValue="instruction">
        <TabsList>
          <TabsTrigger value="instruction">
            Instruction <Badge variant="secondary" className="ml-1">{instructionTests?.length ?? 0}</Badge>
          </TabsTrigger>
          <TabsTrigger value="reasoning">
            Reasoning <Badge variant="secondary" className="ml-1">{reasoningTests?.length ?? 0}</Badge>
          </TabsTrigger>
          <TabsTrigger value="conversation">
            Conversation <Badge variant="secondary" className="ml-1">{conversationTests?.length ?? 0}</Badge>
          </TabsTrigger>
          <TabsTrigger value="context-window">
            Context Window <Badge variant="secondary" className="ml-1">{contextWindowTests?.length ?? 0}</Badge>
          </TabsTrigger>
          <TabsTrigger value="mcp-tool">
            MCP Tool <Badge variant="secondary" className="ml-1">{mcpToolTests?.length ?? 0}</Badge>
          </TabsTrigger>
        </TabsList>

        <TabsContent value="instruction">
          <InstructionTab tests={instructionTests ?? []} />
        </TabsContent>

        <TabsContent value="reasoning">
          <ReasoningTab tests={reasoningTests ?? []} />
        </TabsContent>

        <TabsContent value="conversation">
          <ConversationTab tests={conversationTests ?? []} />
        </TabsContent>

        <TabsContent value="context-window">
          <ContextWindowTab tests={contextWindowTests ?? []} />
        </TabsContent>

        <TabsContent value="mcp-tool">
          <McpToolTab tests={mcpToolTests ?? []} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// --- Instruction Tab ---

import type { InstructionTest, ReasoningTest, ConversationTest, ContextWindowTest, McpToolTest } from '@/api/types';

function InstructionTab({ tests }: { tests: InstructionTest[] }) {
  const createTest = useCreateInstructionTest();
  const deleteTest = useDeleteInstructionTest();
  const [open, setOpen] = useState(false);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createTest.mutate({
      category: fd.get('category') as string,
      prompt: fd.get('prompt') as string,
      expectedResult: fd.get('expectedResult') as string,
      validationType: fd.get('validationType') as string,
      strictOrder: fd.get('strictOrder') === 'on',
      isActive: true,
    }, { onSuccess: () => setOpen(false) });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Instruction Following Tests</CardTitle>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button size="sm"><Plus className="mr-1 h-3 w-3" /> Add</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Add Instruction Test</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="space-y-2">
                <Label>Category</Label>
                <Input name="category" placeholder="e.g. formatting, constraints" required />
              </div>
              <div className="space-y-2">
                <Label>Prompt</Label>
                <Textarea name="prompt" placeholder="The instruction to test..." rows={3} required />
              </div>
              <div className="space-y-2">
                <Label>Expected Result</Label>
                <Textarea name="expectedResult" placeholder="What to look for in the response..." rows={2} required />
              </div>
              <div className="space-y-2">
                <Label>Validation Type</Label>
                <Select name="validationType" defaultValue="contains">
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="contains">Contains</SelectItem>
                    <SelectItem value="exact_match">Exact Match</SelectItem>
                    <SelectItem value="regex">Regex</SelectItem>
                    <SelectItem value="word_count">Word Count</SelectItem>
                    <SelectItem value="list_count">List Count</SelectItem>
                    <SelectItem value="format_check">Format Check</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex items-center gap-2">
                <input type="checkbox" id="strictOrder" name="strictOrder" className="h-4 w-4" />
                <Label htmlFor="strictOrder" className="font-normal">Strict order</Label>
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createTest.isPending}>
                  {createTest.isPending ? 'Adding...' : 'Add Test'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {tests.map((test) => (
            <div key={test.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{test.category}</Badge>
                  <Badge variant="secondary">{test.validationType}</Badge>
                  {!test.isActive && <Badge variant="destructive">Inactive</Badge>}
                </div>
                <Button variant="ghost" size="sm" onClick={() => deleteTest.mutate(test.id)}>
                  <Trash2 className="h-3 w-3 text-destructive" />
                </Button>
              </div>
              <p className="mt-2 text-sm">{test.prompt}</p>
              <p className="mt-1 text-xs text-muted-foreground">Expected: {test.expectedResult}</p>
            </div>
          ))}
          {tests.length === 0 && (
            <p className="text-center text-muted-foreground py-4">No instruction tests defined.</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// --- Reasoning Tab ---

function ReasoningTab({ tests }: { tests: ReasoningTest[] }) {
  const createTest = useCreateReasoningTest();
  const deleteTest = useDeleteReasoningTest();
  const [open, setOpen] = useState(false);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createTest.mutate({
      category: fd.get('category') as string,
      description: (fd.get('description') as string) || undefined,
      prompt: fd.get('prompt') as string,
      correctAnswer: fd.get('correctAnswer') as string,
      isActive: true,
    }, { onSuccess: () => setOpen(false) });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Reasoning Tests</CardTitle>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button size="sm"><Plus className="mr-1 h-3 w-3" /> Add</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Add Reasoning Test</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="space-y-2">
                <Label>Category</Label>
                <Input name="category" placeholder="e.g. logic, math, spatial" required />
              </div>
              <div className="space-y-2">
                <Label>Description (optional)</Label>
                <Input name="description" placeholder="Brief description of the test" />
              </div>
              <div className="space-y-2">
                <Label>Prompt</Label>
                <Textarea name="prompt" placeholder="The reasoning problem..." rows={4} required />
              </div>
              <div className="space-y-2">
                <Label>Correct Answer</Label>
                <Textarea name="correctAnswer" placeholder="The expected answer..." rows={2} required />
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createTest.isPending}>
                  {createTest.isPending ? 'Adding...' : 'Add Test'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {tests.map((test) => (
            <div key={test.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{test.category}</Badge>
                  {test.description && <span className="text-xs text-muted-foreground">{test.description}</span>}
                </div>
                <Button variant="ghost" size="sm" onClick={() => deleteTest.mutate(test.id)}>
                  <Trash2 className="h-3 w-3 text-destructive" />
                </Button>
              </div>
              <p className="mt-2 text-sm">{test.prompt}</p>
              <p className="mt-1 text-xs text-muted-foreground">Answer: {test.correctAnswer}</p>
            </div>
          ))}
          {tests.length === 0 && (
            <p className="text-center text-muted-foreground py-4">No reasoning tests defined.</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// --- Conversation Tab ---

interface TurnDraft { userMessage: string; expectedTheme: string }
interface CriterionDraft { criterion: string }

function ConversationTab({ tests }: { tests: ConversationTest[] }) {
  const createTest = useCreateConversationTest();
  const deleteTest = useDeleteConversationTest();
  const [open, setOpen] = useState(false);
  const [turns, setTurns] = useState<TurnDraft[]>([{ userMessage: '', expectedTheme: '' }]);
  const [criteria, setCriteria] = useState<CriterionDraft[]>([{ criterion: '' }]);

  const resetForm = () => {
    setTurns([{ userMessage: '', expectedTheme: '' }]);
    setCriteria([{ criterion: '' }]);
  };

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createTest.mutate({
      test: {
        category: fd.get('category') as string,
        description: (fd.get('description') as string) || undefined,
        systemPrompt: (fd.get('systemPrompt') as string) || undefined,
        isActive: true,
      },
      turns: turns.filter(t => t.userMessage.trim()).map((t, i) => ({
        turnNumber: i + 1,
        userMessage: t.userMessage,
        expectedTheme: t.expectedTheme || undefined,
      })),
      criteria: criteria.filter(c => c.criterion.trim()).map((c, i) => ({
        criterion: c.criterion,
        sortOrder: i + 1,
      })),
    }, {
      onSuccess: () => { setOpen(false); resetForm(); },
    });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Conversation Tests</CardTitle>
        <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) resetForm(); }}>
          <DialogTrigger asChild>
            <Button size="sm"><Plus className="mr-1 h-3 w-3" /> Add</Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
            <DialogHeader><DialogTitle>Add Conversation Test</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>Category</Label>
                  <Input name="category" placeholder="e.g. multi_turn, persona" required />
                </div>
                <div className="space-y-2">
                  <Label>Description</Label>
                  <Input name="description" placeholder="What this test evaluates" />
                </div>
              </div>
              <div className="space-y-2">
                <Label>System Prompt (optional)</Label>
                <Textarea name="systemPrompt" placeholder="System prompt for the conversation..." rows={2} />
              </div>

              {/* Turns */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label className="text-sm font-semibold">Conversation Turns</Label>
                  <Button type="button" variant="outline" size="sm" onClick={() => setTurns([...turns, { userMessage: '', expectedTheme: '' }])}>
                    <Plus className="mr-1 h-3 w-3" /> Turn
                  </Button>
                </div>
                {turns.map((turn, i) => (
                  <div key={i} className="flex gap-2 items-start">
                    <span className="mt-2 text-xs text-muted-foreground w-6 shrink-0">#{i + 1}</span>
                    <div className="flex-1 space-y-1">
                      <Input
                        placeholder="User message"
                        value={turn.userMessage}
                        onChange={(e) => { const t = [...turns]; t[i].userMessage = e.target.value; setTurns(t); }}
                        required
                      />
                      <Input
                        placeholder="Expected theme (optional)"
                        value={turn.expectedTheme}
                        onChange={(e) => { const t = [...turns]; t[i].expectedTheme = e.target.value; setTurns(t); }}
                        className="h-7 text-xs"
                      />
                    </div>
                    {turns.length > 1 && (
                      <Button type="button" variant="ghost" size="sm" className="mt-1" onClick={() => setTurns(turns.filter((_, j) => j !== i))}>
                        <X className="h-3 w-3" />
                      </Button>
                    )}
                  </div>
                ))}
              </div>

              {/* Judging Criteria */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label className="text-sm font-semibold">Judging Criteria</Label>
                  <Button type="button" variant="outline" size="sm" onClick={() => setCriteria([...criteria, { criterion: '' }])}>
                    <Plus className="mr-1 h-3 w-3" /> Criterion
                  </Button>
                </div>
                {criteria.map((c, i) => (
                  <div key={i} className="flex gap-2">
                    <Input
                      placeholder="e.g. Maintains consistent persona throughout"
                      value={c.criterion}
                      onChange={(e) => { const cr = [...criteria]; cr[i].criterion = e.target.value; setCriteria(cr); }}
                      required
                    />
                    {criteria.length > 1 && (
                      <Button type="button" variant="ghost" size="sm" onClick={() => setCriteria(criteria.filter((_, j) => j !== i))}>
                        <X className="h-3 w-3" />
                      </Button>
                    )}
                  </div>
                ))}
              </div>

              <DialogFooter>
                <Button type="submit" disabled={createTest.isPending}>
                  {createTest.isPending ? 'Adding...' : 'Add Test'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {tests.map((test) => (
            <div key={test.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Badge variant="outline">{test.category}</Badge>
                  {!test.isActive && <Badge variant="destructive">Inactive</Badge>}
                </div>
                <Button variant="ghost" size="sm" onClick={() => deleteTest.mutate(test.id)}>
                  <Trash2 className="h-3 w-3 text-destructive" />
                </Button>
              </div>
              {test.description && <p className="mt-2 text-sm">{test.description}</p>}
            </div>
          ))}
          {tests.length === 0 && (
            <p className="text-center text-muted-foreground py-4">No conversation tests defined.</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// --- Context Window Tab ---

interface CheckpointDraft { secretWord: string; carrierSentence: string }

function ContextWindowTab({ tests }: { tests: ContextWindowTest[] }) {
  const createTest = useCreateContextWindowTest();
  const [open, setOpen] = useState(false);
  const [checkpoints, setCheckpoints] = useState<CheckpointDraft[]>([
    { secretWord: '', carrierSentence: '' },
    { secretWord: '', carrierSentence: '' },
  ]);

  const resetForm = () => {
    setCheckpoints([{ secretWord: '', carrierSentence: '' }, { secretWord: '', carrierSentence: '' }]);
  };

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    const baseTokens = Number(fd.get('baseTargetTokens') as string) || 32000;
    const validCheckpoints = checkpoints.filter(c => c.secretWord.trim());

    createTest.mutate({
      test: {
        name: fd.get('name') as string,
        description: (fd.get('description') as string) || undefined,
        fillerType: fd.get('fillerType') as string,
        baseTargetTokens: baseTokens,
        baseCheckpointCount: validCheckpoints.length,
        buriedInstruction: (fd.get('buriedInstruction') as string) || undefined,
        needleComplexity: fd.get('needleComplexity') as string,
        isActive: true,
      },
      checkpoints: validCheckpoints.map((c, i) => ({
        targetTokenPosition: Math.round((baseTokens / (validCheckpoints.length + 1)) * (i + 1)),
        secretWord: c.secretWord,
        carrierSentence: c.carrierSentence || undefined,
        sortOrder: i + 1,
      })),
    }, {
      onSuccess: () => { setOpen(false); resetForm(); },
    });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Context Window Tests</CardTitle>
        <Dialog open={open} onOpenChange={(v) => { setOpen(v); if (!v) resetForm(); }}>
          <DialogTrigger asChild>
            <Button size="sm"><Plus className="mr-1 h-3 w-3" /> Add</Button>
          </DialogTrigger>
          <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
            <DialogHeader><DialogTitle>Add Context Window Test</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>Name</Label>
                  <Input name="name" placeholder="e.g. Long prose recall" required />
                </div>
                <div className="space-y-2">
                  <Label>Description</Label>
                  <Input name="description" placeholder="What this test evaluates" />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-2">
                  <Label>Filler Type</Label>
                  <Select name="fillerType" defaultValue="prose">
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="prose">Prose</SelectItem>
                      <SelectItem value="code">Code</SelectItem>
                      <SelectItem value="mixed">Mixed</SelectItem>
                      <SelectItem value="technical">Technical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Needle Complexity</Label>
                  <Select name="needleComplexity" defaultValue="simple">
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="simple">Simple</SelectItem>
                      <SelectItem value="moderate">Moderate</SelectItem>
                      <SelectItem value="complex">Complex</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Base Target Tokens</Label>
                  <Input name="baseTargetTokens" type="number" defaultValue={32000} min={1000} step={1000} />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Buried Instruction (optional)</Label>
                <Textarea name="buriedInstruction" placeholder="Instruction to bury in the context..." rows={2} />
              </div>

              {/* Checkpoints */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <Label className="text-sm font-semibold">Checkpoints (needles to find)</Label>
                  <Button type="button" variant="outline" size="sm" onClick={() => setCheckpoints([...checkpoints, { secretWord: '', carrierSentence: '' }])}>
                    <Plus className="mr-1 h-3 w-3" /> Checkpoint
                  </Button>
                </div>
                {checkpoints.map((cp, i) => (
                  <div key={i} className="flex gap-2 items-start">
                    <span className="mt-2 text-xs text-muted-foreground w-6 shrink-0">#{i + 1}</span>
                    <div className="flex-1 grid grid-cols-2 gap-2">
                      <Input
                        placeholder="Secret word"
                        value={cp.secretWord}
                        onChange={(e) => { const c = [...checkpoints]; c[i].secretWord = e.target.value; setCheckpoints(c); }}
                        required
                      />
                      <Input
                        placeholder="Carrier sentence (optional)"
                        value={cp.carrierSentence}
                        onChange={(e) => { const c = [...checkpoints]; c[i].carrierSentence = e.target.value; setCheckpoints(c); }}
                      />
                    </div>
                    {checkpoints.length > 1 && (
                      <Button type="button" variant="ghost" size="sm" className="mt-1" onClick={() => setCheckpoints(checkpoints.filter((_, j) => j !== i))}>
                        <X className="h-3 w-3" />
                      </Button>
                    )}
                  </div>
                ))}
              </div>

              <DialogFooter>
                <Button type="submit" disabled={createTest.isPending}>
                  {createTest.isPending ? 'Adding...' : 'Add Test'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {tests.map((test) => (
            <div key={test.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <span className="font-medium">{test.name}</span>
                <div className="flex gap-2">
                  <Badge variant="outline">{test.fillerType}</Badge>
                  <Badge variant="secondary">{test.needleComplexity}</Badge>
                  {!test.isActive && <Badge variant="destructive">Inactive</Badge>}
                </div>
              </div>
              {test.description && <p className="mt-1 text-sm text-muted-foreground">{test.description}</p>}
              <p className="mt-1 text-xs text-muted-foreground">
                Base tokens: {test.baseTargetTokens.toLocaleString()} &middot; Checkpoints: {test.baseCheckpointCount}
              </p>
            </div>
          ))}
          {tests.length === 0 && (
            <p className="text-center text-muted-foreground py-4">No context window tests defined.</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// --- MCP Tool Tab ---

function McpToolTab({ tests }: { tests: McpToolTest[] }) {
  const createTest = useCreateMcpToolTest();
  const deleteTest = useDeleteMcpToolTest();
  const [open, setOpen] = useState(false);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createTest.mutate({
      category: fd.get('category') as string,
      description: fd.get('description') as string,
      toolName: fd.get('toolName') as string,
      command: fd.get('command') as string,
      scenarioPrompt: fd.get('scenarioPrompt') as string,
      responseValidationType: fd.get('responseValidationType') as string,
      executeTool: fd.get('executeTool') === 'on',
      isActive: true,
    }, { onSuccess: () => setOpen(false) });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>MCP Tool Tests</CardTitle>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button size="sm"><Plus className="mr-1 h-3 w-3" /> Add</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Add MCP Tool Test</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-2">
                  <Label>Category</Label>
                  <Input name="category" placeholder="e.g. crud, search" required />
                </div>
                <div className="space-y-2">
                  <Label>Tool Name</Label>
                  <Input name="toolName" placeholder="e.g. echo_create_note" required />
                </div>
              </div>
              <div className="space-y-2">
                <Label>Command</Label>
                <Input name="command" placeholder="e.g. create_note" required />
              </div>
              <div className="space-y-2">
                <Label>Description</Label>
                <Input name="description" placeholder="What this test verifies" required />
              </div>
              <div className="space-y-2">
                <Label>Scenario Prompt</Label>
                <Textarea name="scenarioPrompt" placeholder="The scenario for the model..." rows={3} required />
              </div>
              <div className="space-y-2">
                <Label>Response Validation</Label>
                <Select name="responseValidationType" defaultValue="contains">
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="contains">Contains</SelectItem>
                    <SelectItem value="json_schema">JSON Schema</SelectItem>
                    <SelectItem value="tool_call">Tool Call</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex items-center gap-2">
                <input type="checkbox" id="executeTool" name="executeTool" className="h-4 w-4" />
                <Label htmlFor="executeTool" className="font-normal">Execute tool (requires MCP server)</Label>
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createTest.isPending}>
                  {createTest.isPending ? 'Adding...' : 'Add Test'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </CardHeader>
      <CardContent>
        <div className="space-y-2">
          {tests.map((test) => (
            <div key={test.id} className="rounded-md border p-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="font-mono text-sm">{test.toolName}</span>
                  <Badge variant="outline">{test.category}</Badge>
                  {!test.isActive && <Badge variant="destructive">Inactive</Badge>}
                </div>
                <Button variant="ghost" size="sm" onClick={() => deleteTest.mutate(test.id)}>
                  <Trash2 className="h-3 w-3 text-destructive" />
                </Button>
              </div>
              <p className="mt-2 text-sm">{test.description}</p>
            </div>
          ))}
          {tests.length === 0 && (
            <p className="text-center text-muted-foreground py-4">No MCP tool tests defined.</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
