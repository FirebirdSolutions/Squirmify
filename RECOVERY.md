# Squirmify recovery, 2026-08-04

Reconstruction of the March 2026 standalone rebuild after the WantToCry
ransomware. Written so the next person (or the next me) does not have to
re-derive any of it.

## What the ransomware actually did

Not a blanket encrypt-everything. It **deleted files but left directory trees
standing**, which made the damage look total on a casual `ls`:

- every `.cs` file in `src/` — gone, directories intact
- every config file at `web/` root — `package.json`, `vite.config.ts`,
  `tsconfig*.json`, `index.html`, `components.json`, `src/vite-env.d.ts`
- the repo's own `.gitignore`
- `node_modules/` hollowed out: 394 package directories with their contents
  and top-level manifests removed. `react/cjs/` was an empty directory.
  341 manifests deeper in the tree survived, which is how versions were
  partly recovered.

It did **not** touch `bin/`, `obj/`, `web/src/`, `web/dist/`, or the JSON test
definitions. That asymmetry is the whole reason recovery was possible.

## What survived, and what it bought us

| Asset | State | What it gave us |
|---|---|---|
| `web/src` | intact, never committed | 34 files / 4,039 lines — the whole SPA |
| `Squirmify.*.dll` + `.pdb` | intact | the entire backend, decompilable |
| `Squirmify.Console` (native) | intact, runnable | proof of correct behaviour to diff against |
| `web/dist` | intact | the original `index.html` structure |
| test JSON + `base_seeds.jsonl` | intact | 882 seeds, 188 tests |
| GitHub `ChoonForge/Squirmify` | intact | the *pre*-rebuild code only |

## Backend: decompiled, 0 errors

`ilspycmd` 10.1.0 over the five assemblies, using their `.pdb` symbols.
**148 files / ~10,700 lines.** All five projects build clean, and the CLI
rebuilt from recovered source produces identical help output to the March
binary.

Symbol files mattered enormously: primary constructors, original parameter
and local names, and nullable annotations all survived. All 202 embedded
Dapper SQL strings came back verbatim, so the schema is recoverable from the
`CREATE`/`INSERT` statements.

Roughly 900 of the initial 920 errors were plumbing — ilspycmd emits an
invalid `<LangVersion>15.0</LangVersion>` and relative `HintPath`s that do not
resolve. Real package versions came from the committed `deps.json`.

Three genuine reconstructions, each marked with a `RECOVERY NOTE` comment
in-file:

1. `BenchmarkOrchestrator` — a stopword array the compiler had cached on
   `<PrivateImplementationDetails>`, inexpressible in C#. Values verbatim from
   the IL; only the storage location is reconstructed.
2. `Squirmify.Api/Program.cs` — missing entirely, because top-level statements
   lower to a synthetic `Program.<Main>$` that ilspycmd skips. Extracted from
   the assembly. This recovered the full bootstrap: DI, SignalR hub at
   `/hubs/benchmark`, camelCase JSON with enum converter, the `DevCors` policy,
   and the orchestrator→hub event bridge.
3. `Squirmify.Console/Program.cs` — same lowering, emitted as the invalid
   identifier `_003CMain_003E_0024`. Renamed to a conventional `Main`.

## Frontend: rebuilt config, builds and runs

`web/src` never needed recovering — only the config around it. Every value was
derived from evidence rather than guessed:

- **`@/` alias** — 81 imports across `src/` use it
- **port 5173** — the recovered API pins `DevCors` to `localhost:5173/5174`
- **`/api` proxy** — `src/api/client.ts` hardcodes `const API_BASE = '/api'`
- **`/hubs` proxy with `ws: true`** — `useBenchmarkHub.ts` calls
  `.withUrl('/hubs/benchmark')`
- **Tailwind v4** — `src/index.css` uses `@import "tailwindcss"` and
  `@theme inline`; 4.2.1 confirmed from a surviving nested manifest
- **React 19** — `main.tsx` uses `createRoot`
- **radix-ui 1.4.3** — the unified package, not `@radix-ui/react-*`
- **shadcn 3.8.5** — pinned exactly. `src/index.css` imports
  `shadcn/tailwind.css`, and 3.8.5 is the version that shipped
  `dist/tailwind.css`. This one is a real dependency, not just the CLI.
- **`index.html`** — structure taken from the surviving built `dist/index.html`
  (`<div id="root">`, `vite.svg` favicon). Title changed from the Vite scaffold
  default `web` to `Squirmify`.

Result: `tsc -b` clean, `vite build` transforms 2,036 modules, dev server
serves and compiles. Output sizes track the March original (55.65 kB CSS vs
41 kB, 577 kB JS vs 471 kB — the delta is sourcemaps plus newer minors).

## What is NOT recovered

- **Comments.** Gone permanently; decompilation cannot recover them.
- **Exact dependency versions** for packages whose manifests were wiped at
  every level. Pinned by API usage instead, so minor versions may differ from
  March. `shadcn` is the one exact pin, because its behaviour depends on it.
- **The design rationale.** The original rebuild session lives only in the
  Claude web UI, with no built-in export. It holds the codebase review and the
  *why* behind decisions. Now a nice-to-have: the code itself is back.
- **End-to-end verification.** Nothing beyond CLI startup and frontend
  compilation has been exercised. No benchmark has been run against a live
  provider.

## Open decisions

- `recovered/` has deliberately **not** been merged into `src/`. Kept separate
  so raw decompiler output stays diffable against the fixed-up version.
- `.gitignore` currently force-tracks `bin/**/Squirmify.*.dll` and `.pdb`,
  against normal practice, because those were the only copy of the backend.
  Once `recovered/` is reconciled into `src/` and verified, restore the normal
  rule.
- Treat recovered binaries as untrusted until reviewed. They date to 5–11
  March, predating the 2026-05-22 intrusion, so they are very likely clean —
  but "very likely" is not "verified".

## One process lesson

`web/src` — 4,039 lines, the single most valuable surviving artifact — was
**never committed to git**. It survived by luck, not design. The first action
of this recovery was committing it, before any analysis.
