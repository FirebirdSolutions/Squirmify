import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { useRuns, useRunResults } from '@/api/queries';

export function ResultsPage() {
  const { data: runs } = useRuns();
  const completedRuns = runs?.filter((r) => r.status === 'completed') ?? [];
  const [selectedRun, setSelectedRun] = useState<number>(0);
  const { data: results, isLoading } = useRunResults(selectedRun);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Results</h1>
        <p className="text-muted-foreground">Detailed analysis of benchmark runs</p>
      </div>

      <Select
        value={selectedRun ? selectedRun.toString() : ''}
        onValueChange={(v) => setSelectedRun(Number(v))}
      >
        <SelectTrigger className="w-80">
          <SelectValue placeholder="Select a completed run" />
        </SelectTrigger>
        <SelectContent>
          {completedRuns.map((run) => (
            <SelectItem key={run.id} value={run.id.toString()}>
              {run.name ?? `Run #${run.id}`} — {new Date(run.startedAt!).toLocaleDateString()}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      {isLoading && selectedRun > 0 && <p className="text-muted-foreground">Loading analysis...</p>}

      {results && (
        <div className="space-y-6">
          {/* Overall Summary */}
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-muted-foreground">Instruction Pass Rate</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{(results.overall.instructionPassRate * 100).toFixed(1)}%</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-muted-foreground">Reasoning Avg</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{results.overall.reasoningAvgScore.toFixed(1)}</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-muted-foreground">Conversation Avg</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{results.overall.conversationAvgScore.toFixed(1)}</div>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm text-muted-foreground">Composite</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{results.overall.compositeScore.toFixed(1)}</div>
              </CardContent>
            </Card>
          </div>

          {/* Model Leaderboard */}
          <Card>
            <CardHeader><CardTitle>Model Leaderboard</CardTitle></CardHeader>
            <CardContent>
              <div className="space-y-2">
                {results.modelSummaries
                  .sort((a, b) => b.compositeScore - a.compositeScore)
                  .map((model, i) => (
                    <div key={model.modelId} className="flex items-center justify-between rounded-md border p-3">
                      <div className="flex items-center gap-3">
                        <span className="text-lg font-bold text-muted-foreground w-6">#{i + 1}</span>
                        <div>
                          <span className="font-mono text-sm">{model.modelName}</span>
                          <div className="text-xs text-muted-foreground">
                            {model.avgTokensPerSec.toFixed(0)} tok/s &middot; {model.avgLatencyMs.toFixed(0)}ms avg
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center gap-3">
                        <Badge variant="outline">Inst: {(model.instructionPassRate * 100).toFixed(0)}%</Badge>
                        <Badge variant="outline">Reason: {model.reasoningAvgScore.toFixed(1)}</Badge>
                        <Badge variant="outline">Conv: {model.conversationAvgScore.toFixed(1)}</Badge>
                        <Badge className="bg-squirmify text-squirmify-foreground">
                          {model.compositeScore.toFixed(1)}
                        </Badge>
                      </div>
                    </div>
                  ))}
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {!selectedRun && completedRuns.length === 0 && (
        <p className="text-center text-muted-foreground py-8">No completed runs to analyze yet.</p>
      )}
    </div>
  );
}
