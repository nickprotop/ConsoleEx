# SharpConsoleUI Scripting

Ready-to-use SharpConsoleUI templates and shell recipes for building interactive TUI steps into shell pipelines.

**Prerequisites:** .NET 10 SDK installed, `dotnet` on your PATH. See [SHELL_SCRIPTING.md](../SHELL_SCRIPTING.md) for the I/O contract and runtime details.

## Templates

Each template is a single `.cs` file runnable with `dotnet run <file>.cs`. On Unix they are also executable directly (shebang + `chmod +x`).

| Template | Purpose | Input | Output |
|---|---|---|---|
| [picker.cs](templates/picker.cs) | Single-select list | stdin: lines | stdout: selected line |
| [multi-picker.cs](templates/multi-picker.cs) | Multi-select checklist | stdin: lines | stdout: selected lines, newline-separated |
| [confirm.cs](templates/confirm.cs) | Yes/no dialog | args: `--title TITLE --message MSG` | exit 0=yes, 1=no |
| [prompt.cs](templates/prompt.cs) | Text input | args: `--prompt "Label"` (optional `--mask` for passwords) | stdout: entered text |
| [table-select.cs](templates/table-select.cs) | JSON array row picker | stdin: JSON array of objects | stdout: selected row as JSON object |
| [progress.cs](templates/progress.cs) | Run a command with progress monitoring | args: `-- <command> [args...]` | stdout: command's stdout, exit = command's exit |

All templates follow the shared exit-code contract documented in [SHELL_SCRIPTING.md](../SHELL_SCRIPTING.md):
`0` = success/confirmed, `1` = user cancelled, `2` = invalid input, `>2` = unexpected error.

## Shell Recipes

Real-world examples of using the templates from each shell:

- [bash / zsh](recipes/bash.md)
- [PowerShell](recipes/powershell.md)
- [fish](recipes/fish.md)
- [nushell](recipes/nushell.md)

## Copying and Modifying a Template

1. Copy the `.cs` file into your project or scripts directory.
2. Update the `#:package SharpConsoleUI@X.Y.Z` line on line 2 if you want a different SharpConsoleUI version.
3. Edit the template's logic as needed.
4. Run with `dotnet run <your-file>.cs` (Windows) or `./<your-file>.cs` after `chmod +x` (Unix).
