# bash / zsh recipes

All examples assume the templates are at `docs/scripting/templates/` in the SharpConsoleUI repo, or copied to a convenient path (e.g., `~/bin/`). Replace `$TMPL` with your path.

```bash
TMPL=./docs/scripting/templates
```

## Pick a git branch and check it out

```bash
branch=$(git branch --list | sed 's/^..//' | dotnet run $TMPL/picker.cs) \
    && git checkout "$branch"
```

## Pick multiple files for deletion

```bash
dotnet run $TMPL/multi-picker.cs < <(find /tmp -maxdepth 1 -type f) |
    while IFS= read -r f; do rm -i "$f"; done
```

## Confirm before running a destructive command

```bash
dotnet run $TMPL/confirm.cs --title "Danger" --message "Delete /tmp/cache?" \
    && rm -rf /tmp/cache
```

## Prompt for an API token and export it

```bash
export GH_TOKEN=$(dotnet run $TMPL/prompt.cs --prompt "GitHub token: " --mask)
```

## Pick a process to kill

```bash
ps -eo pid,comm,pcpu,pmem --no-headers |
    awk 'BEGIN{print "["} NR>1{print ","} {printf "{\"pid\":\"%s\",\"comm\":\"%s\",\"cpu\":\"%s\",\"mem\":\"%s\"}", $1, $2, $3, $4} END{print "]"}' |
    dotnet run $TMPL/table-select.cs |
    jq -r '.pid' |
    xargs -r kill
```

## Monitor a long-running build

```bash
dotnet run $TMPL/progress.cs -- npm run build
```

## Exit code handling

Every template exits 0 on success, 1 on cancel, 2 on invalid input. Use this to short-circuit:

```bash
if ! choice=$(ls | dotnet run $TMPL/picker.cs); then
    echo "User cancelled." >&2
    exit 1
fi
echo "Picked: $choice"
```
