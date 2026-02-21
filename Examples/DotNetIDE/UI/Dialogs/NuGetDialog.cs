using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace DotNetIDE;

public static class NuGetDialog
{
    public static (string? PackageName, string? Version) Show(ConsoleWindowSystem ws)
    {
        string? packageName = null;
        string? version = null;
        var closed = new System.Threading.ManualResetEventSlim(false);

        var namePrompt = new PromptControl { Prompt = "Package name: ", InputWidth = 30 };
        var versionPrompt = new PromptControl { Prompt = "Version (optional): ", InputWidth = 20 };
        var addBtn = new ButtonControl { Text = "Add", Width = 8 };
        var cancelBtn = new ButtonControl { Text = "Cancel", Width = 8 };

        Window? dialog = null;

        addBtn.Click += (_, _) =>
        {
            packageName = namePrompt.Input.Trim();
            version = versionPrompt.Input.Trim();
            if (version.Length == 0) version = null;
            dialog?.Close();
            closed.Set();
        };
        cancelBtn.Click += (_, _) => { dialog?.Close(); closed.Set(); };

        var buttonRow = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Left };
        var addCol = new ColumnContainer(buttonRow); addCol.AddContent(addBtn); buttonRow.AddColumn(addCol);
        var cancelCol = new ColumnContainer(buttonRow); cancelCol.AddContent(cancelBtn); buttonRow.AddColumn(cancelCol);
        buttonRow.StickyPosition = StickyPosition.Bottom;

        dialog = new WindowBuilder(ws)
            .WithTitle("Add NuGet Package")
            .WithSize(52, 10)
            .Centered()
            .AsModal()
            .Closable(true)
            .AddControl(new MarkupControl(new List<string> { "" }))
            .AddControl(namePrompt)
            .AddControl(versionPrompt)
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
            .AddControl(buttonRow)
            .Build();

        dialog.OnClosed += (_, _) => closed.Set();

        ws.AddWindow(dialog);
        closed.Wait(TimeSpan.FromMinutes(5));
        return (string.IsNullOrWhiteSpace(packageName) ? null : packageName, version);
    }
}
