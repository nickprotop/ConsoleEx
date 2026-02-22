using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace DotNetIDE;

public enum DialogResult { Save, DontSave, Cancel }

public static class ConfirmSaveDialog
{
    public static void Show(ConsoleWindowSystem ws, string fileName, Action<DialogResult> onResult)
    {
        // Guard: onResult fires exactly once regardless of close path
        bool fired = false;
        void FireResult(DialogResult r) { if (fired) return; fired = true; onResult(r); }

        var saveBtn    = new ButtonControl { Text = "Save",       Width = 10 };
        var dontSaveBtn = new ButtonControl { Text = "Don't Save", Width = 14 };
        var cancelBtn  = new ButtonControl { Text = "Cancel",     Width = 10 };

        Window? dialog = null;

        saveBtn.Click    += (_, _) => { FireResult(DialogResult.Save);     dialog?.Close(); };
        dontSaveBtn.Click += (_, _) => { FireResult(DialogResult.DontSave); dialog?.Close(); };
        cancelBtn.Click  += (_, _) => { FireResult(DialogResult.Cancel);   dialog?.Close(); };

        var buttonRow = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Left };
        var saveCol   = new ColumnContainer(buttonRow); saveCol.AddContent(saveBtn);     buttonRow.AddColumn(saveCol);
        var dontCol   = new ColumnContainer(buttonRow); dontCol.AddContent(dontSaveBtn); buttonRow.AddColumn(dontCol);
        var cancelCol = new ColumnContainer(buttonRow); cancelCol.AddContent(cancelBtn); buttonRow.AddColumn(cancelCol);
        buttonRow.StickyPosition = StickyPosition.Bottom;

        dialog = new WindowBuilder(ws)
            .WithTitle("Unsaved Changes")
            .WithSize(50, 8)
            .Centered()
            .AsModal()
            .Resizable(false)
            .Minimizable(false)
            .Maximizable(false)
            .AddControl(new MarkupControl(new List<string>
            {
                "",
                $"  [yellow]{Spectre.Console.Markup.Escape(fileName)}[/] has unsaved changes.",
                "",
                "  Do you want to save before closing?"
            }))
            .AddControl(new RuleControl { StickyPosition = StickyPosition.Bottom })
            .AddControl(buttonRow)
            .Build();

        // X button or Escape = Cancel
        dialog.OnClosed += (_, _) => FireResult(DialogResult.Cancel);
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
