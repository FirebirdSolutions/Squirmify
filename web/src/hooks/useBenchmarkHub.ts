import { useEffect, useRef, useState, useCallback } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import type { RunProgress, LogEvent } from '@/api/types';

interface BenchmarkHubState {
  progress: RunProgress | null;
  logs: LogEvent[];
  isConnected: boolean;
  runComplete: boolean;
  completedStatus: string | null;
}

export function useBenchmarkHub(runId: number | null) {
  const [state, setState] = useState<BenchmarkHubState>({
    progress: null,
    logs: [],
    isConnected: false,
    runComplete: false,
    completedStatus: null,
  });

  const connectionRef = useRef<HubConnection | null>(null);

  const clearState = useCallback(() => {
    setState({
      progress: null,
      logs: [],
      isConnected: false,
      runComplete: false,
      completedStatus: null,
    });
  }, []);

  useEffect(() => {
    if (!runId) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/benchmark')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('ProgressUpdate', (progress: RunProgress) => {
      setState((prev) => ({ ...prev, progress }));
    });

    connection.on('LogEvent', (logEvent: LogEvent) => {
      setState((prev) => ({
        ...prev,
        logs: [...prev.logs.slice(-199), logEvent],
      }));
    });

    connection.on('RunComplete', (_runId: number, status: string) => {
      setState((prev) => ({
        ...prev,
        runComplete: true,
        completedStatus: status,
      }));
    });

    connection
      .start()
      .then(() => {
        setState((prev) => ({ ...prev, isConnected: true }));
        return connection.invoke('SubscribeToRun', runId);
      })
      .catch((err) => console.error('[SignalR] Connection failed:', err));

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [runId]);

  return { ...state, clearState };
}
