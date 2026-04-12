# PowerShell recipes

All examples assume the templates are reachable via `$Tmpl`. Replace with your path.

```powershell
$Tmpl = './docs/scripting/templates'
```

## Pick a running service and restart it

```powershell
$svc = Get-Service |
    Select-Object Name, Status, DisplayName |
    ConvertTo-Json |
    dotnet run "$Tmpl/table-select.cs" |
    ConvertFrom-Json

if ($svc) {
    Restart-Service -Name $svc.Name
}
```

## Pick a branch to check out

```powershell
$branch = git branch --list |
    ForEach-Object { $_.Trim('* ').Trim() } |
    dotnet run "$Tmpl/picker.cs"

if ($LASTEXITCODE -eq 0) {
    git checkout $branch
}
```

## Confirm before deploying

```powershell
dotnet run "$Tmpl/confirm.cs" --title "Deploy" --message "Push to prod?"
if ($LASTEXITCODE -eq 0) {
    ./deploy.ps1
}
```

## Prompt for credentials

```powershell
$user = dotnet run "$Tmpl/prompt.cs" --prompt "Username: "
$pass = dotnet run "$Tmpl/prompt.cs" --prompt "Password: " --mask
$cred = [PSCredential]::new($user, (ConvertTo-SecureString $pass -AsPlainText -Force))
```

## Multi-select files to process

```powershell
Get-ChildItem *.log |
    Select-Object -ExpandProperty Name |
    dotnet run "$Tmpl/multi-picker.cs" |
    ForEach-Object { Compress-Archive -Path $_ -DestinationPath "$_.zip" }
```

## Monitor a long-running operation

```powershell
dotnet run "$Tmpl/progress.cs" -- npm install
```

## Exit code handling

```powershell
$choice = Get-ChildItem | Select-Object -ExpandProperty Name | dotnet run "$Tmpl/picker.cs"
if ($LASTEXITCODE -ne 0) {
    Write-Error "User cancelled."
    exit 1
}
Write-Output "Picked: $choice"
```

## Windows note

Windows ignores the `#!/usr/bin/env dotnet` shebang on line 1 of the templates. Always invoke via `dotnet run` explicitly. File-based app caching means the second run of a given template is near-instant.
