using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace DotNetIDE;

public static class NuGetDialog
{
    public static void Show(ConsoleWindowSystem ws, Action<(string? PackageName, string? Version)> onResult)
    {
        bool fired = false;
        void FireResult(string? pkg, string? ver) { if (fired) return; fired = true; onResult((pkg, ver)); }

        var namePrompt    = new PromptControl { Prompt = "Package name: ",      InputWidth = 30 };
        var versionPrompt = new PromptControl { Prompt = "Version (optional): ", InputWidth = 20 };
        var addBtn    = new ButtonControl { Text = "Add",    Width = 8 };
        var cancelBtn = new ButtonControl { Text = "Cancel", Width = 8 };

        Window? dialog = null;

        addBtn.Click += (_, _) =>
        {
            var pkg = namePrompt.Input.Trim();
            var ver = versionPrompt.Input.Trim();
            FireResult(string.IsNullOrWhiteSpace(pkg) ? null : pkg,
                       string.IsNullOrWhiteSpace(ver) ? null : ver);
            dialog?.Close();
        };
        cancelBtn.Click += (_, _) => { FireResult(null, null); dialog?.Close(); };

        var buttonRow = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Left };
        var addCol    = new ColumnContainer(buttonRow); addCol.AddContent(addBtn);    buttonRow.AddColumn(addCol);
        var cancelCol = new ColumnContainer(buttonRow); cancelCol.AddContent(cancelBtn); buttonRow.AddColumn(cancelCol);
        buttonRow.StickyPosition = StickyPosition.Bottom;

        dialog = new WindowBuilder(ws)
            .WithTitle("Add NuGet Package")
            .WithSize(52, 10)
            .Centered()
            .AsModal()
            .Resizable(false)
            .Minimizable(false)
            .Maximizable(false)
            .AddControl(new MarkupControl(new List<string> { "" }))
            .AddControl(namePrompt)
            .AddControl(versionPrompt)
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
            .AddControl(buttonRow)
            .Build();

        // X button or Escape = Cancel
        dialog.OnClosed += (_, _) => FireResult(null, null);
        dialog.KeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                dialog.Close();
                e.Handled = true;
            }
        };

        ws.AddWindow(dialog);
    }
}
