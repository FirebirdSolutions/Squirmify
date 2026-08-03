import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'node:path';

// RECONSTRUCTED 2026-08-04 after WantToCry deleted web/'s config files.
// Derived from the surviving source, not guessed:
//   - `@/` alias      : 81 imports across src/ use it
//   - port 5173       : the recovered API pins DevCors to localhost:5173/5174
//   - API port 5105   : the surviving March Dockerfile sets
//                       ASPNETCORE_URLS=http://+:5105 and EXPOSEs it
//   - /api proxy      : src/api/client.ts hardcodes `const API_BASE = '/api'`
//   - /hubs proxy     : useBenchmarkHub.ts calls .withUrl('/hubs/benchmark'),
//                       and SignalR needs ws: true to negotiate a socket
//   - tailwind plugin : src/index.css uses `@import "tailwindcss"` + `@theme
//                       inline`, which is the v4 Vite-plugin setup
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5105',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5105',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
