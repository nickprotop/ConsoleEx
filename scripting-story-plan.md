# SharpConsoleUI Scripting Story — Plan

**Date:** 2026-04-12
**Status:** Draft plan
**Goal:** Make it trivial to invoke a SharpConsoleUI TUI from any shell or script, using .NET 10 file-based apps (`dotnet run script.cs`). No bindings, no project files, no build steps.

## Why This Instead of Language Bindings

Language bindings (Python, PowerShell, etc.) have high ongoing maintenance cost in languages outside the maintainer's expertise. .NET 10 file-based apps eliminate the need entirely:

- Users write a single `.cs` file with `#:package SharpConsoleUI@X.Y.Z` at the top
- They run it with `dotnet run script.cs` — no project, no build
- Any shell (PowerShell, bash, fish, nushell, zsh) can invoke the script and consume its output
- The maintainer writes only C# — the language they already own

This reaches every scripting environment on every platform simultaneously, with zero per-language maintenance.

## Deliverables

### 1. Documentation: "SharpConsoleUI as a Script"

A new doc page covering:

- **Prerequisites** — .NET 10 SDK installed, platform support notes
- **The `#:package` directive** — how dependency resolution works in file-based apps
- **Shebang setup** — making `.cs` files executable on Unix-like systems
- **First script** — A 20-line picker menu walkthrough
- **stdin/stdout contracts** — how to pipe data in, return data out
- **Exit codes** — conventions for cancel, error, confirm
- **Startup time** — expectations, caching behavior, when to care

**Location:** `docs/scripting/index.md` (served via the existing GitHub Pages site under `nickprotop.github.io/ConsoleEx/scripting/`)

### 2. Script Templates

Ready-to-copy `.cs` files under `scripts/templates/` in the repo. Each is a self-contained working example users copy, tweak, and run.

**Templates to ship in v1:**

| Template | Purpose | Input/Output |
|---|---|---|
| `picker.cs` | Single-select list picker | stdin: lines or JSON array → stdout: selected item, exit 0=selected / 1=cancelled |
| `multi-picker.cs` | Multi-select checklist | stdin: lines → stdout: newline-separated selections |
| `confirm.cs` | Yes/no confirmation with rich description | args: title/message → exit 0=yes / 1=no |
| `prompt.cs` | Text input with validation | args: prompt text → stdout: entered text |
| `wizard.cs` | Multi-step form wizard | stdin: JSON schema → stdout: JSON result |
| `log-viewer.cs` | Scrollable log viewer with search/filter | stdin: log stream → interactive viewer |
| `table-select.cs` | Tabular data picker (columns from object properties) | stdin: JSON array → stdout: selected row |
| `progress.cs` | Long-running operation monitor with cancel | args: command to run → shows progress, returns command's exit code |

Each template:
- Starts with `#!/usr/bin/env dotnet` shebang (Unix)
- Has `#:package SharpConsoleUI@X.Y.Z` dependency directive
- Includes a header comment block explaining usage, inputs, outputs, exit codes
- Fits in under 150 lines where possible
- Has no external dependencies beyond SharpConsoleUI itself

### 3. Shell Integration Recipes

One page per shell showing how to invoke templates from that shell's idioms.

**Shells to cover in v1:**

- **PowerShell** — Piping objects (via `ConvertTo-Json`), consuming results (via `ConvertFrom-Json`), handling cancellation
- **bash / zsh** — Piping lines, command substitution, exit code handling
- **fish** — fish-specific idioms
- **nushell** — Native table piping

**Location:** `docs/scripting/shells/{powershell,bash,fish,nushell}.md`

**Example (PowerShell page excerpt):**

```powershell
# Pick a running service interactively, then restart it
$service = Get-Service |
    ConvertTo-Json |
    dotnet run scripts/templates/table-select.cs |
    ConvertFrom-Json

if ($service) {
    Restart-Service $service.Name
}
```

### 4. Optional Launcher: `scui` (deferred, evaluate after v1)

A thin Bash/PowerShell helper that wraps `dotnet run` with:
- Shorter invocation: `scui picker` instead of `dotnet run ~/.scui/templates/picker.cs`
- Template discovery: `scui list`
- Template initialization: `scui new my-tool` (copies a template to a new file)

**Decision:** Not in v1. File-based apps already cache compilation, and the value of a wrapper is small compared to the maintenance cost of yet another deliverable. Revisit if user feedback asks for it.

## Repository Structure

New top-level directory:

```
ConsoleEx/
  SharpConsoleUI/              # Existing
  scripts/
    templates/
      picker.cs
      multi-picker.cs
      confirm.cs
      prompt.cs
      wizard.cs
      log-viewer.cs
      table-select.cs
      progress.cs
    recipes/                   # Working examples per shell
      powershell/
        restart-service.ps1
        pick-git-branch.ps1
      bash/
        pick-git-branch.sh
        confirm-deploy.sh
      fish/
        pick-git-branch.fish
      nushell/
        pick-process.nu
    README.md                  # Index, quick start
  docs/
    scripting/
      index.md                 # Main guide
      shells/
        powershell.md
        bash.md
        fish.md
        nushell.md
      templates.md             # Reference for each template's I/O contract
```

## Implementation Steps

### Phase 1: Prove the pattern (half day)

1. Write `scripts/templates/picker.cs` — single-select list picker
2. Verify `dotnet run picker.cs` works end-to-end with `#:package SharpConsoleUI@<current>`
3. Verify shebang invocation works on Linux + macOS
4. Verify cold-start and warm-start times are acceptable (target: <1s warm)
5. If startup time is a problem, investigate `dotnet run --configfile` caching or AOT precompilation

**Exit criteria:** A single working `picker.cs` invokable as `./picker.cs` on Unix, `dotnet run picker.cs` on Windows, with a PowerShell one-liner that pipes data in and consumes the result.

### Phase 2: Template set (1–2 days)

1. Implement the 8 v1 templates listed above
2. Standardize the I/O contract across templates (JSON schemas, exit codes)
3. Write the header comment block convention (usage, inputs, outputs, examples)
4. Manual test each template on Linux, macOS, Windows

**Exit criteria:** All 8 templates work on all 3 platforms, follow the shared I/O contract, have consistent docstrings.

### Phase 3: Documentation (1 day)

1. Write `docs/scripting/index.md` — main guide
2. Write `docs/scripting/templates.md` — per-template reference
3. Write the 4 shell recipe pages
4. Write 2–3 real-world recipes per shell under `scripts/recipes/`
5. Update main README with a "Scripting" section linking to the guide

**Exit criteria:** A new user can read the guide, copy a template, modify it for their use case, and integrate it into their shell workflow in under 15 minutes.

### Phase 4: CI & Release Integration (half day)

1. Add a CI step that runs each template with sample input on all 3 platforms to catch breakage
2. Add the `#:package` version to the publish flow — when a new SharpConsoleUI version ships, update all templates' `#:package` directives to match
3. Update `docs.yml` workflow to include the new `docs/scripting/` pages

**Exit criteria:** Tag pushes automatically update template package references and rebuild the docs site.

## Technical Details

### `#:package` Directive

File-based apps use a magic comment at the top of the file to declare NuGet dependencies:

```csharp
#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.54

using SharpConsoleUI;
using SharpConsoleUI.Builders;
// ... rest of script
```

The `dotnet run` command resolves the package, caches it, and executes the file. Subsequent runs with the same dependency hash skip resolution.

**Version management:** Every template ships with a pinned version of SharpConsoleUI. A release-time script updates all `#:package` directives in `scripts/templates/*.cs` to the new version before the repository tag is created.

### Shebang on Unix

Unix-like systems can execute `.cs` files directly if:
1. The file has `#!/usr/bin/env dotnet` as its first line
2. The file is marked executable (`chmod +x`)
3. `dotnet` is on the `PATH`

The shebang line is a valid C# comment when followed by the `#:package` directive on line 2, so it doesn't break compilation.

**Windows note:** Windows ignores the shebang. Users invoke templates as `dotnet run picker.cs` explicitly. Document this clearly.

### I/O Contract

All templates follow a consistent contract so they compose in shell pipelines:

**Input:**
- Simple templates (picker, confirm, prompt): plain lines on stdin OR args
- Structured templates (table-select, wizard): JSON on stdin
- Templates detect input mode by checking `Console.IsInputRedirected`

**Output:**
- Simple templates: plain text on stdout
- Structured templates: JSON on stdout
- Errors: plain text on stderr, never mixed with stdout
- Progress/UI rendering: bypasses stdout/stderr via SharpConsoleUI's driver (doesn't pollute pipes)

**Exit codes:**
- `0` — User confirmed / selected / completed
- `1` — User cancelled (Esc, Ctrl+C in-app, empty selection)
- `2` — Invalid input / validation error
- `>2` — Unexpected error

This matches conventions used by `fzf`, `gum`, and other well-known interactive CLI tools, so shell scripts can handle them uniformly.

### Terminal vs. Pipe Detection

Templates must handle two modes:
- **Interactive:** stdin is a TTY, stdout may be a TTY or pipe — run the TUI normally
- **Piped:** stdin is a pipe — read piped data before taking over the terminal

The trick: SharpConsoleUI needs a TTY to render. If stdout is also a pipe, the TUI can't render at all. Solution: templates open `/dev/tty` (Unix) or `CONIN$`/`CONOUT$` (Windows) directly for UI I/O when stdin/stdout are redirected. This is a known pattern (fzf does it) and SharpConsoleUI's driver should support it — verify in Phase 1.

**If the driver doesn't support alternate I/O channels:** This is a hard blocker and needs to be fixed in SharpConsoleUI itself before the scripting story ships. Add this to Phase 1 as a discovery item.

### Startup Time

File-based apps have a one-time compile cost per unique source hash. Cached runs are fast (~200-400ms on warm .NET runtime). Cold-start (first run, cold filesystem cache) can hit 2-3 seconds.

**Mitigation strategies** (in order of preference):
1. Accept warm-start latency — acceptable for interactive tools
2. If cold-start is painful, investigate `dotnet-script` style warmup or precompilation
3. Document cold-start expectations clearly so users aren't surprised

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| SharpConsoleUI driver can't use alternate I/O when stdin/stdout are redirected | Scripts can't compose in pipelines — kills the entire value proposition | Phase 1 discovery item; fix in driver before shipping templates if needed |
| Cold-start time is too slow for CLI use | Users abandon the pattern | Measure in Phase 1; if bad, investigate caching/precompilation or document as an expected cost |
| `#:package` feature changes in .NET 10 release candidates | Templates break | Pin to stable .NET 10 once released; test against RCs |
| Windows users don't know about file-based apps | Adoption is Unix-only | Documentation explicitly shows `dotnet run` invocation for Windows; PowerShell recipes demonstrate it |
| Users expect `scui` launcher that doesn't exist in v1 | Friction at first use | Document the raw `dotnet run` invocation clearly; revisit launcher in v2 based on feedback |
| Template version drift — users copy an old template after upgrading SharpConsoleUI | Broken scripts in user environments | `dotnet run` reports the version mismatch clearly; templates include comment showing how to bump the package version |

## Success Metrics

- A new user reads the guide and runs their first template in under 5 minutes
- At least one template runs successfully on all 3 platforms from a single shell invocation
- PowerShell / bash / fish / nushell recipes all work without modification
- Zero per-language maintenance burden introduced

## Scope Boundaries

**In scope:**
- `.cs` file templates using `dotnet run`
- Shell integration recipes for 4 major shells
- Documentation on the SharpConsoleUI docs site
- CI tests for templates on 3 platforms

**Out of scope:**
- `scui` launcher (deferred to post-v1)
- Language-specific bindings (Python, PowerShell modules, etc. — explicitly not this plan)
- NuGet package for templates (they're source files, distributed via the repo)
- Pre-compiled native executables of templates (goes against the "scripting" positioning)

## Open Questions

1. **Does SharpConsoleUI's `NetConsoleDriver` support opening `/dev/tty` directly when stdin/stdout are redirected?** This determines whether the scripting story is viable at all. Investigate in Phase 1.
2. **What's the actual cold-start time of a `dotnet run script.cs` invocation with SharpConsoleUI as a dependency?** Measure on Linux/macOS/Windows before committing to templates.
3. **Should templates be versioned independently of SharpConsoleUI?** Probably not — keeping them in lockstep is simpler. But if a template has bugs the library doesn't, a `.postN`-style patch could be useful.
4. **Is there a standard JSON schema format for the wizard template?** If the wizard is driven by a JSON schema, we need to pick a format (JSON Schema spec, custom, etc.). Lean toward a minimal custom format for v1.
