using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace DotNetIDE;

public enum DialogResult { Save, DontSave, Cancel }

public static class ConfirmSaveDialog
{
    public static DialogResult Show(ConsoleWindowSystem ws, string fileName)
    {
        var result = DialogResult.Cancel;
        var closed = new System.Threading.ManualResetEventSlim(false);

        var saveBtn = new ButtonControl { Text = "Save", Width = 10 };
        var dontSaveBtn = new ButtonControl { Text = "Don't Save", Width = 14 };
        var cancelBtn = new ButtonControl { Text = "Cancel", Width = 10 };

        Window? dialog = null;

        saveBtn.Click += (_, _) => { result = DialogResult.Save; dialog?.Close(); closed.Set(); };
        dontSaveBtn.Click += (_, _) => { result = DialogResult.DontSave; dialog?.Close(); closed.Set(); };
        cancelBtn.Click += (_, _) => { result = DialogResult.Cancel; dialog?.Close(); closed.Set(); };

        var buttonRow = new HorizontalGridControl { HorizontalAlignment = HorizontalAlignment.Left };
        var saveCol = new ColumnContainer(buttonRow); saveCol.AddContent(saveBtn); buttonRow.AddColumn(saveCol);
        var dontCol = new ColumnContainer(buttonRow); dontCol.AddContent(dontSaveBtn); buttonRow.AddColumn(dontCol);
        var cancelCol = new ColumnContainer(buttonRow); cancelCol.AddContent(cancelBtn); buttonRow.AddColumn(cancelCol);
        buttonRow.StickyPosition = StickyPosition.Bottom;

        dialog = new WindowBuilder(ws)
            .WithTitle("Unsaved Changes")
            .WithSize(50, 8)
            .Centered()
            .AsModal()
            .Closable(false)
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

        dialog.OnClosed += (_, _) => closed.Set();

        ws.AddWindow(dialog);
        closed.Wait(TimeSpan.FromMinutes(5));
        return result;
    }
}
