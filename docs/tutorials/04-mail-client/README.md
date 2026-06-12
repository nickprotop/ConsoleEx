# TerminalMail — source for Tutorial 4

The complete, runnable source for [Tutorial 4: Terminal Mail Client](../04-mail-client.md).
Every file here is reproduced verbatim in that tutorial.

## Run

```bash
cd TerminalMail
dotnet run
```

Keys: `↑↓` navigate · `c` compose · `s` settings · `q` quit.

## Test

```bash
dotnet test TerminalMail.Tests/TerminalMail.Tests.csproj
```

## Layout

| Path | Responsibility |
|---|---|
| `TerminalMail/Program.cs` | Bootstrap: `ConsoleWindowSystem` + the mail window |
| `TerminalMail/Models/` | Plain data: `Folder`, `Message` |
| `TerminalMail/ViewModels/` | MVVM: `ViewModelBase`, `RelayCommand`, and the mailbox/message/compose view models |
| `TerminalMail/UI/` | `MailWindow` (three-pane layout), `MessageTableDataSource`, `ColorScheme`, `Dialogs` (compose), `SettingsView` (NavigationView) |
| `TerminalMail/Data/SampleInbox.cs` | In-memory seed data (no real IMAP/SMTP) |
| `TerminalMail.Tests/` | xUnit tests for the view models and data source |

The project targets `net10.0` and references the in-repo `SharpConsoleUI` library
(with a `SharpConsoleUI` NuGet fallback, so it still builds if you copy this folder
out of the repository).
