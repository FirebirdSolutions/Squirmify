import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useRuns, useCompareRuns } from '@/api/queries';

export function ComparePage() {
  const { data: runs } = useRuns();
  const completedRuns = runs?.filter((r) => r.status === 'completed') ?? [];
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const compareRuns = useCompareRuns();

  const toggleRun = (id: number) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Compare Runs</h1>
        <p className="text-muted-foreground">Side-by-side comparison of benchmark runs</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Select runs to compare</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2 mb-4">
            {completedRuns.map((run) => (
              <label
                key={run.id}
                className="flex items-center gap-3 rounded-md border p-3 cursor-pointer hover:bg-muted/50"
              >
                <input
                  type="checkbox"
                  checked={selectedIds.includes(run.id)}
                  onChange={() => toggleRun(run.id)}
                  className="rounded"
                />
                <span className="font-medium">{run.name ?? `Run #${run.id}`}</span>
                <span className="text-sm text-muted-foreground">
                  {run.totalModels} models &middot; {new Date(run.startedAt!).toLocaleDateString()}
                </span>
              </label>
            ))}
            {completedRuns.length === 0 && (
              <p className="text-muted-foreground">No completed runs available for comparison.</p>
            )}
          </div>
          <Button
            disabled={selectedIds.length < 2 || compareRuns.isPending}
            onClick={() => compareRuns.mutate(selectedIds)}
          >
            Compare {selectedIds.length} runs
          </Button>
        </CardContent>
      </Card>

      {compareRuns.data && (
        <Card>
          <CardHeader><CardTitle>Comparison</CardTitle></CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b">
                    <th className="text-left p-2">Run</th>
                    <th className="text-right p-2">Models</th>
                    <th className="text-right p-2">Instruction</th>
                    <th className="text-right p-2">Reasoning</th>
                    <th className="text-right p-2">Conversation</th>
                    <th className="text-right p-2">Composite</th>
                    <th className="text-right p-2">Tok/s</th>
                  </tr>
                </thead>
                <tbody>
                  {compareRuns.data.runs.map((run) => (
                    <tr key={run.runId} className="border-b">
                      <td className="p-2 font-medium">{run.runName ?? `Run #${run.runId}`}</td>
                      <td className="p-2 text-right">{run.modelCount}</td>
                      <td className="p-2 text-right">{(run.instructionPassRate * 100).toFixed(1)}%</td>
                      <td className="p-2 text-right">{run.reasoningAvgScore.toFixed(1)}</td>
                      <td className="p-2 text-right">{run.conversationAvgScore.toFixed(1)}</td>
                      <td className="p-2 text-right">
                        <Badge className="bg-squirmify text-squirmify-foreground">
                          {run.compositeScore.toFixed(1)}
                        </Badge>
                      </td>
                      <td className="p-2 text-right">{run.avgTokensPerSec.toFixed(0)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
