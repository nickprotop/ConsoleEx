# nushell recipes

nushell's native table handling composes especially well with `table-select.cs` via JSON.

```nu
let tmpl = "./docs/scripting/templates"
```

## Pick a running process

```nu
ps |
    select pid name cpu mem |
    to json |
    dotnet run $"($tmpl)/table-select.cs" |
    from json |
    get pid
```

## Pick a git branch

```nu
git branch --list |
    lines |
    each { $in | str trim | str replace -r '^\*\s*' '' } |
    str join "\n" |
    dotnet run $"($tmpl)/picker.cs" |
    str trim
```

## Confirm

```nu
dotnet run $"($tmpl)/confirm.cs" --title "Deploy" --message "Push to prod?"
if $env.LAST_EXIT_CODE == 0 {
    ./deploy.sh
}
```

## Prompt for input

```nu
let name = (dotnet run $"($tmpl)/prompt.cs" --prompt "Name: ")
print $"Hello, ($name)"
```

## Multi-select files

```nu
ls *.log |
    get name |
    str join "\n" |
    dotnet run $"($tmpl)/multi-picker.cs" |
    lines |
    each { |f| mv $f $"($f).archived" }
```

## Progress wrapper

```nu
dotnet run $"($tmpl)/progress.cs" -- npm run build
```
