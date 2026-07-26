# BlazorTerm — Feature Implementation Plan

**Site:** https://terminal.tommyb.dev
**Date:** July 2026
**Companion to:** `blazorterm-improvement-plan.md` (content / infra / a11y),
`blazorterm-design-plan.md` (visual)

---

## Summary

Features ordered by **what they signal**, not by effort. The through-line: this
site should demonstrate parsing, composition, and observability — the things you
actually do — rather than decoration.

If only one thing gets built: **pipes**. It's the feature where a senior
engineer looks at it and immediately understands you didn't take a shortcut.

---

## Prerequisite: structured command output

Nothing in Tier 1 works until commands stop returning pre-rendered markup.

This is the same lesson as Phase 1 of the content plan — the renderer must not
own the data.

```csharp
abstract record OutputLine
{
    public abstract string ToPlainText();   // what grep/wc see
}

record TextLine(string Text, LineStyle Style = LineStyle.Body) : OutputLine;
record KeyValueLine(string Key, string Value) : OutputLine;
record LinkLine(string Label, string Href) : OutputLine;
record TableLine(string[] Cells) : OutputLine;
record RawLine(string Text) : OutputLine;   // ASCII art, preformatted

record CommandResult(IReadOnlyList<OutputLine> Lines, int ExitCode = 0);
```

Every command returns `CommandResult`. The terminal renderer turns
`OutputLine` into styled spans. Filters operate on `ToPlainText()`.

- [x] Define `OutputLine` hierarchy
- [x] Migrate every existing command to return `CommandResult`
- [x] Move all styling into the renderer
- [x] Snapshot-test a few commands' plain-text projections

**Effort:** 1 session. **Blocks:** pipes, filesystem, trace rendering.

---

# Tier 1 — Build these

## 1.1 Pipes ⭐ highest signal

`resume | grep azure` · `projects | wc -l` · `cat contact.txt | grep github`

**Why:** moves the site from "menu of canned outputs" to "someone implemented a
shell." On-brand for a source-generator/Roslyn specialist — it demonstrates
tokenising and composition rather than decoration.

### Implementation

```
input → Tokenizer → CommandSegment[] → Pipeline.Execute() → CommandResult
```

- [x] **Tokeniser**: split on `|` respecting quoted strings; parse each segment
      into `(name, args[], flags[])`
- [x] **`ICommand`** — produces output from no input
- [x] **`IFilter : ICommand`** — takes `CommandResult` in, returns one out
- [x] **Pipeline executor** — thread output through filters left to right; abort
      the chain on non-zero exit code
- [x] Filters to ship: `grep [-i] [-v] <pattern>`, `head [-n]`, `tail [-n]`,
      `wc [-l]`, `sort [-r]`, `uniq`
- [x] Error handling in shell idiom: `grep: missing pattern`, and
      `resume | resume` → `resume: not a filter`
- [x] `grep` should highlight matches in the accent colour, not just filter
- [x] Tab-completion must complete filter names after a `|`

### Tests

- [x] Tokeniser: quoting, escaped pipes, empty segments, trailing `|`
- [x] Each filter in isolation
- [x] Three-stage chain: `resume | grep azure | wc -l`

**Effort:** 1 weekend.

---

## 1.2 `trace <command>` ⭐ most differentiating

Run any command, then render its real OpenTelemetry span tree as an ASCII
waterfall.

**Why:** you already have the instrumentation and the OTLP pipeline. No other
portfolio can show a live distributed trace *of itself*, and distributed tracing
is your day job. Strongest possible show-don't-tell.

### Implementation

- [x] Register an in-process `ActivityListener` scoped to the command execution
- [x] Start a root `Activity` for the command; collect all descendants by
      `TraceId`
- [x] Buffer completed activities in a per-circuit collector (bounded — cap at
      ~200 spans, discard overflow with a note)
- [x] Build a tree from `ParentSpanId`, sort children by start time
- [x] Render proportional bars against total root duration

```
❯ trace resume

TRACE  4f2a9c1e8b3d7a56          total 12.4ms

resume                     ├████████████████████┤  12.4ms
  ├─ content.load          ├███████┤               4.1ms
  │   └─ cache.hit         ├█┤                     0.3ms
  ├─ markdown.render       ├─────────█████┤        5.2ms
  └─ output.format         ├──────────────────██┤  1.8ms

3 spans · 0 errors · exporter: OTLP → collector
```

- [x] Show span attributes on a `-v` flag
- [x] Colour error spans in red; show status codes
- [x] Ensure the listener is disposed per command — no leaks across the circuit
- [x] Guard: `trace trace` should refuse rather than recurse

**Effort:** 1 weekend. **Depends on:** structured output for the renderer.

---

## 1.3 Virtual filesystem

`cd projects/` · `ls` · `cat service-bus-explorer/README.md` · `cd ..`

**Why:** converts a flat command list into an environment worth exploring. Also
attacks the "help is doing navigation's job" problem from the other side.

### Implementation

```csharp
abstract record FsNode(string Name);
record DirectoryNode(string Name, IReadOnlyList<FsNode> Children) : FsNode(Name);
record FileNode(string Name, Func<CommandResult> Read) : FsNode(Name);
```

- [x] Build the tree from the same content model the resume pages use — one
      source of truth, three surfaces (terminal, static pages, filesystem)
- [x] Path resolution: absolute, relative, `.`, `..`, `~`
- [x] `cd`, `ls [-l]`, `cat`, `pwd`, `tree`
- [x] Track cwd in circuit state; include it in `[PersistentState]`
- [x] Prompt segment reflects cwd (`~/projects` not always `~/`)
- [x] Tab-completion across path segments, not just command names
- [x] `ls` output should pipe cleanly into `grep`

Suggested layout:

```
~/
├── about.txt
├── resume.md
├── contact.txt
├── stack/
│   ├── languages.txt
│   ├── platform.txt
│   └── cloud.txt
└── projects/
    ├── blazorterm/
    ├── service-bus-explorer/
    ├── property-resolvers/
    └── otel-tracing-demo/
```

**Effort:** 1 weekend.

---

# Tier 2 — Real data, live

## 2.1 `version`

- [x] Git SHA the container was built from (inject via
      `--build-arg` → `AssemblyInformationalVersion` or GitVersion)
- [x] Build timestamp, .NET version, uptime
- [x] Link the SHA to the GitHub commit

Quietly signals that you think about provenance. **Effort:** 2 hours.

## 2.2 `who`

- [x] Concurrent circuits over SignalR, anonymised (count + rough geo at most)
- [x] Guard the sad case: don't render "1 user connected" as the common state —
      phrase it as session info instead
- [x] Uses the circuit you're already paying for

**Effort:** 3 hours.

## 2.3 `git log`

Career as commit history. `git blame stack.txt` shows when each technology was
picked up.

- [x] Render job changes as commits with plausible SHAs and dates
- [x] `git log --oneline`, `git show <sha>` for role detail
- [x] `git blame` on stack files

Clever conceit, near-zero cost, reuses the timeline data. **Effort:** 4 hours.

## 2.4 `kubectl get pods`

Read-only, sanitised view of the Talos cluster.

- [ ] Allowlist resources: pods, nodes, namespaces only
- [ ] Strip anything identifying — no IPs, no internal hostnames, no secrets
- [ ] Cache aggressively (30–60s); never hit the API per keystroke
- [ ] Read-only service account, separate from anything with write scope
- [ ] Fail closed: if the cluster is unreachable, print a friendly stub

⚠️ Only build this if you're confident in the sanitisation. The blast radius of
getting it wrong is real.

**Effort:** 1 day including hardening.

## 2.5 `ssh guest@tommyb.dev` — the showpiece

The genre's ultimate flex. Harder in .NET than Go — you'd wire up a server-side
SSH implementation plus a PTY-ish renderer over the same content model.

- [ ] Evaluate SSH server libraries for .NET
- [ ] Anonymous auth only, no shell escape, hard session timeout
- [ ] Rate limit and run in an isolated container with no cluster access
- [ ] Reuse `CommandResult` → ANSI renderer (shares work with the `curl` resume)

`telnet` is the cheap version if SSH proves too much.

**Effort:** multi-weekend. Do last, or not at all.

---

# Tier 3 — Cheap delight

- [ ] `sudo` → `tom is not in the sudoers file. This incident will be reported.`
- [ ] `vim` → traps until `:q!` (with a visible hint after ~10s so it isn't cruel)
- [ ] More themes — nord, solarized, dracula — persisted to `localStorage`
- [ ] `rides` — recent Strava activity as an ASCII bar chart
- [ ] `uptime`, `fortune`, `cowsay` if the mood takes you

**Effort:** an hour each, mostly.

---

# Explicitly not doing

| Feature | Why not |
|---|---|
| `ask` / LLM over the resume | Now the most common portfolio bolt-on. Costs money per visitor, can confidently invent things about your employment history, and signals "called an API" rather than "built something." `trace` is more impressive and more *yours*. |
| Matrix rain screensaver | Every terminal portfolio has one. Visual equivalent of a stock photo. |

---

## Sequencing

| Session | Work |
|---|---|
| 1 | Structured `CommandResult` refactor (prerequisite) |
| 2–3 | Pipes + filters + tests |
| 4–5 | `trace` with ASCII waterfall |
| 6–7 | Virtual filesystem + path tab-completion |
| 8 | `version`, `who`, `git log` |
| 9 | Tier 3 delights |
| 10+ | `kubectl` (if comfortable), then `ssh` (if ambitious) |

---

## Cross-cutting checks

- [ ] Every new command returns `CommandResult` — no exceptions
- [ ] Every new command is tab-completable and appears in the grouped `help`
- [ ] Nothing new bloats the initial HTML or blocks first paint
- [ ] Anything hitting live infrastructure is cached, read-only, and fails closed
- [ ] Output regions stay `aria-live` friendly — ASCII art needs
      `aria-hidden` plus a text alternative
- [ ] ASCII waterfalls and charts need a sensible narrow-viewport fallback
