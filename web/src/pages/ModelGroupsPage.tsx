import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog';
import {
  useModelGroups, useModelGroup, useCreateModelGroup, useDeleteModelGroup,
  useProviders, useModels, useUpdateModelGroup,
} from '@/api/queries';
import { Plus, Trash2, Users, ChevronRight } from 'lucide-react';

export function ModelGroupsPage() {
  const { data: groups, isLoading } = useModelGroups();
  const createGroup = useCreateModelGroup();
  const deleteGroup = useDeleteModelGroup();
  const [createOpen, setCreateOpen] = useState(false);
  const [editingGroupId, setEditingGroupId] = useState<number | null>(null);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createGroup.mutate({
      name: fd.get('name') as string,
      description: (fd.get('description') as string) || undefined,
    }, { onSuccess: () => setCreateOpen(false) });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Model Groups</h1>
          <p className="text-muted-foreground">Named sets of models for benchmark runs</p>
        </div>
        <Dialog open={createOpen} onOpenChange={setCreateOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" /> New Group</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader><DialogTitle>Create Model Group</DialogTitle></DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" name="name" placeholder="e.g. Local Models Q1 2026" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="description">Description (optional)</Label>
                <Textarea id="description" name="description" placeholder="What this group is for..." />
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createGroup.isPending}>
                  {createGroup.isPending ? 'Creating...' : 'Create'}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
      </div>

      {isLoading ? (
        <p className="text-muted-foreground">Loading...</p>
      ) : editingGroupId ? (
        <ModelGroupEditor groupId={editingGroupId} onBack={() => setEditingGroupId(null)} />
      ) : (
        <div className="grid gap-4">
          {groups?.map((group) => (
            <Card key={group.id} className="cursor-pointer hover:border-squirmify/50 transition-colors"
              onClick={() => setEditingGroupId(group.id)}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="flex items-center gap-2">
                  <Users className="h-4 w-4" />
                  {group.name}
                </CardTitle>
                <div className="flex items-center gap-2">
                  <Button variant="ghost" size="sm" onClick={(e) => {
                    e.stopPropagation();
                    deleteGroup.mutate(group.id);
                  }}>
                    <Trash2 className="h-3 w-3 text-destructive" />
                  </Button>
                  <ChevronRight className="h-4 w-4 text-muted-foreground" />
                </div>
              </CardHeader>
              {group.description && (
                <CardContent>
                  <p className="text-sm text-muted-foreground">{group.description}</p>
                </CardContent>
              )}
            </Card>
          ))}
          {groups?.length === 0 && (
            <p className="text-center text-muted-foreground py-8">
              No model groups yet. Create one to group models for benchmarking.
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function ModelGroupEditor({ groupId, onBack }: { groupId: number; onBack: () => void }) {
  const { data: detail, isLoading } = useModelGroup(groupId);
  const { data: providers } = useProviders();
  const [selectedProvider, setSelectedProvider] = useState<number | undefined>();
  const { data: allModels } = useModels(selectedProvider);
  const updateGroup = useUpdateModelGroup();

  if (isLoading || !detail) return <p className="text-muted-foreground">Loading...</p>;

  const groupModelIds = new Set(detail.models.map((m) => m.id));

  const toggleModel = (modelId: number) => {
    const newIds = groupModelIds.has(modelId)
      ? detail.models.filter((m) => m.id !== modelId).map((m) => m.id)
      : [...detail.models.map((m) => m.id), modelId];

    updateGroup.mutate({ id: groupId, modelIds: newIds });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={onBack}>&larr; Back</Button>
        <h2 className="text-xl font-bold">{detail.group.name}</h2>
        <Badge variant="secondary">{detail.models.length} models</Badge>
      </div>

      {detail.group.description && (
        <p className="text-muted-foreground">{detail.group.description}</p>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Current models in group */}
        <Card>
          <CardHeader><CardTitle>Models in Group</CardTitle></CardHeader>
          <CardContent>
            {detail.models.length > 0 ? (
              <div className="space-y-2">
                {detail.models.map((model) => (
                  <div key={model.id} className="flex items-center justify-between rounded-md border p-2">
                    <span className="font-mono text-sm">{model.identifier}</span>
                    <Button variant="ghost" size="sm" onClick={() => toggleModel(model.id)}>
                      <Trash2 className="h-3 w-3 text-destructive" />
                    </Button>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-muted-foreground text-sm">No models added yet. Select from available models.</p>
            )}
          </CardContent>
        </Card>

        {/* Available models to add */}
        <Card>
          <CardHeader>
            <CardTitle>Available Models</CardTitle>
            <div className="flex gap-2 mt-2">
              {providers?.map((p) => (
                <Button
                  key={p.id}
                  variant={selectedProvider === p.id ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setSelectedProvider(selectedProvider === p.id ? undefined : p.id)}
                >
                  {p.name}
                </Button>
              ))}
            </div>
          </CardHeader>
          <CardContent>
            <div className="space-y-2 max-h-96 overflow-y-auto">
              {allModels?.filter((m) => !m.isDeleted).map((model) => (
                <div
                  key={model.id}
                  className="flex items-center justify-between rounded-md border p-2 cursor-pointer hover:bg-muted/50"
                  onClick={() => toggleModel(model.id)}
                >
                  <span className="font-mono text-sm">{model.identifier}</span>
                  {groupModelIds.has(model.id) ? (
                    <Badge className="bg-squirmify text-squirmify-foreground">Added</Badge>
                  ) : (
                    <Badge variant="outline">Add</Badge>
                  )}
                </div>
              ))}
              {!selectedProvider && (
                <p className="text-muted-foreground text-sm">Select a provider to see available models.</p>
              )}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
