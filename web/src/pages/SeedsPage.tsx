import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useSeeds, useSeedStats } from '@/api/queries';

export function SeedsPage() {
  const { data: seeds, isLoading } = useSeeds();
  const { data: stats } = useSeedStats();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Seeds</h1>
        <p className="text-muted-foreground">Prompt seeds for model evaluation</p>
      </div>

      {stats && (
        <div className="grid grid-cols-3 gap-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">Base Seeds</CardTitle>
            </CardHeader>
            <CardContent><div className="text-2xl font-bold">{stats.baseCount}</div></CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">Augmented</CardTitle>
            </CardHeader>
            <CardContent><div className="text-2xl font-bold">{stats.augmentedCount}</div></CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">Total</CardTitle>
            </CardHeader>
            <CardContent><div className="text-2xl font-bold">{stats.total}</div></CardContent>
          </Card>
        </div>
      )}

      {isLoading ? (
        <p className="text-muted-foreground">Loading...</p>
      ) : (
        <Card>
          <CardHeader><CardTitle>All Seeds</CardTitle></CardHeader>
          <CardContent>
            <div className="space-y-2">
              {seeds?.map((seed) => (
                <div key={seed.id} className="rounded-md border p-3">
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{seed.category}</Badge>
                    {seed.isAugmented && <Badge variant="secondary">Augmented</Badge>}
                  </div>
                  <p className="mt-2 text-sm">{seed.instruction}</p>
                </div>
              ))}
              {seeds?.length === 0 && (
                <p className="text-center text-muted-foreground py-4">No seeds yet.</p>
              )}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
