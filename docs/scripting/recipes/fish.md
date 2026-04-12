# fish recipes

All examples assume the templates are at `$tmpl`. Replace with your path.

```fish
set tmpl ./docs/scripting/templates
```

## Pick a git branch

```fish
set branch (git branch --list | string trim | string replace -r '^\*\s*' '' | dotnet run $tmpl/picker.cs)
test $status -eq 0; and git checkout $branch
```

## Confirm before deleting

```fish
dotnet run $tmpl/confirm.cs --title "Danger" --message "Wipe /tmp/cache?"
test $status -eq 0; and rm -rf /tmp/cache
```

## Prompt for a value

```fish
set name (dotnet run $tmpl/prompt.cs --prompt "Your name: ")
test $status -eq 0; and echo "Hello, $name"
```

## Multi-select environment variables to unset

```fish
env | cut -d= -f1 | dotnet run $tmpl/multi-picker.cs | while read -l var
    set -e $var
end
```

## Monitor a build

```fish
dotnet run $tmpl/progress.cs -- cargo build --release
```

## Exit code handling

```fish
set choice (ls | dotnet run $tmpl/picker.cs)
if test $status -ne 0
    echo "Cancelled" >&2
    exit 1
end
echo "Picked: $choice"
```
