import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter,
} from '@/components/ui/dialog';
import { useProviders, useCreateProvider, useDeleteProvider, useSyncModels } from '@/api/queries';
import { Plus, RefreshCw, Trash2 } from 'lucide-react';

export function ProvidersPage() {
  const { data: providers, isLoading } = useProviders();
  const createProvider = useCreateProvider();
  const deleteProvider = useDeleteProvider();
  const syncModels = useSyncModels();
  const [open, setOpen] = useState(false);

  const handleCreate = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createProvider.mutate({
      name: fd.get('name') as string,
      baseUrl: fd.get('baseUrl') as string,
      authToken: (fd.get('authToken') as string) || undefined,
      useAuth: !!fd.get('authToken'),
      timeoutMinutes: Number(fd.get('timeout')) || 10,
    }, { onSuccess: () => setOpen(false) });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Providers</h1>
          <p className="text-muted-foreground">LLM API endpoints to benchmark against</p>
        </div>
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button><Plus className="mr-2 h-4 w-4" /> Add Provider</Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Add Provider</DialogTitle>
            </DialogHeader>
            <form onSubmit={handleCreate} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="name">Name</Label>
                <Input id="name" name="name" placeholder="Local llama.cpp" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="baseUrl">Base URL</Label>
                <Input id="baseUrl" name="baseUrl" placeholder="http://localhost:8080" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="authToken">Auth Token (optional)</Label>
                <Input id="authToken" name="authToken" type="password" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="timeout">Timeout (minutes)</Label>
                <Input id="timeout" name="timeout" type="number" defaultValue={10} />
              </div>
              <DialogFooter>
                <Button type="submit" disabled={createProvider.isPending}>
                  {createProvider.isPending ? 'Creating...' : 'Create'}
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
          {providers?.map((provider) => (
            <Card key={provider.id}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-lg">{provider.name}</CardTitle>
                <div className="flex items-center gap-2">
                  <Badge variant={provider.isActive ? 'default' : 'secondary'}>
                    {provider.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent>
                <div className="flex items-center justify-between">
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <div>{provider.baseUrl}</div>
                    <div>Timeout: {provider.timeoutMinutes}m &middot; Auth: {provider.useAuth ? 'Yes' : 'No'}</div>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => syncModels.mutate(provider.id)}
                      disabled={syncModels.isPending}
                    >
                      <RefreshCw className="mr-1 h-3 w-3" /> Sync Models
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => deleteProvider.mutate(provider.id)}
                    >
                      <Trash2 className="h-3 w-3 text-destructive" />
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
          {providers?.length === 0 && (
            <p className="text-center text-muted-foreground py-8">No providers configured yet.</p>
          )}
        </div>
      )}
    </div>
  );
}
