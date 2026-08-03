import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { useProviders, useModels, useToggleModelDisabled } from '@/api/queries';

export function ModelsPage() {
  const { data: providers } = useProviders();
  const [selectedProvider, setSelectedProvider] = useState<number | undefined>();
  const { data: models, isLoading } = useModels(selectedProvider);
  const toggleDisabled = useToggleModelDisabled();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Models</h1>
        <p className="text-muted-foreground">Available models from your providers</p>
      </div>

      <div className="flex items-center gap-4">
        <Select
          value={selectedProvider?.toString() ?? 'all'}
          onValueChange={(v) => setSelectedProvider(v === 'all' ? undefined : Number(v))}
        >
          <SelectTrigger className="w-64">
            <SelectValue placeholder="All providers" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All providers</SelectItem>
            {providers?.map((p) => (
              <SelectItem key={p.id} value={p.id.toString()}>{p.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading ? (
        <p className="text-muted-foreground">Loading...</p>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>
              {models?.length ?? 0} models
              {selectedProvider && providers ? ` from ${providers.find(p => p.id === selectedProvider)?.name}` : ''}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {models?.map((model) => (
                <div key={model.id} className="flex items-center justify-between rounded-md border p-3">
                  <div className="flex items-center gap-3">
                    <span className="font-mono text-sm">{model.identifier}</span>
                    {model.displayName && (
                      <span className="text-sm text-muted-foreground">({model.displayName})</span>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    {!model.isAvailable && <Badge variant="outline">Unavailable</Badge>}
                    <Button
                      variant={model.isDisabled ? 'destructive' : 'outline'}
                      size="sm"
                      onClick={() => toggleDisabled.mutate({ id: model.id, disabled: !model.isDisabled })}
                    >
                      {model.isDisabled ? 'Disabled' : 'Enabled'}
                    </Button>
                  </div>
                </div>
              ))}
              {models?.length === 0 && (
                <p className="text-center text-muted-foreground py-4">
                  No models found. Sync models from a provider first.
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
