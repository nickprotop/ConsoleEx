using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class DialogsWindow
{
    #region Constants

    private const int WindowWidth = 65;
    private const int WindowHeight = 24;
    private const int ButtonWidth = 25;
    private const int ButtonLeftMargin = 2;
    private const int SectionTopMargin = 1;
    private const int SectionLabelLeftMargin = 1;

    #endregion

    public static Window Create(ConsoleWindowSystem ws)
    {
        var resultDisplay = Controls.Markup("[dim]Dialog results will appear here[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        #region File Dialog Buttons

        var fileSectionLabel = Controls.Label("[bold cyan]File Dialogs[/]");
        fileSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var openFileBtn = Controls.Button("Open File...")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                _ = Task.Run(async () =>
                {
                    var result = await FileDialogs.ShowFilePickerAsync(ws);
                    resultDisplay.SetContent(new List<string>
                    {
                        result != null
                            ? $"[green]Selected file:[/] {result}"
                            : "[dim]Cancelled[/]"
                    });
                });
            })
            .Build();
        openFileBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var openFolderBtn = Controls.Button("Open Folder...")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                _ = Task.Run(async () =>
                {
                    var result = await FileDialogs.ShowFolderPickerAsync(ws);
                    resultDisplay.SetContent(new List<string>
                    {
                        result != null
                            ? $"[green]Selected folder:[/] {result}"
                            : "[dim]Cancelled[/]"
                    });
                });
            })
            .Build();
        openFolderBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var saveFileBtn = Controls.Button("Save File...")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) =>
            {
                _ = Task.Run(async () =>
                {
                    var result = await FileDialogs.ShowSaveFileAsync(ws);
                    resultDisplay.SetContent(new List<string>
                    {
                        result != null
                            ? $"[green]Save path:[/] {result}"
                            : "[dim]Cancelled[/]"
                    });
                });
            })
            .Build();
        saveFileBtn.Margin = new Margin { Left = ButtonLeftMargin };

        #endregion

        #region System Dialog Buttons

        var systemSectionLabel = Controls.Label("[bold cyan]System Dialogs[/]");
        systemSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

        var aboutBtn = Controls.Button("About")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => AboutDialog.Show(ws))
            .Build();
        aboutBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var settingsBtn = Controls.Button("Settings")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => SettingsDialog.Show(ws))
            .Build();
        settingsBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var performanceBtn = Controls.Button("Performance")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => PerformanceDialog.Show(ws))
            .Build();
        performanceBtn.Margin = new Margin { Left = ButtonLeftMargin };

        var themeBtn = Controls.Button("Theme Selector")
            .WithWidth(ButtonWidth)
            .OnClick((_, _) => ThemeSelectorDialog.Show(ws))
            .Build();
        themeBtn.Margin = new Margin { Left = ButtonLeftMargin };

        #endregion

        #region Window Assembly

        Window? window = null;
        window = new WindowBuilder(ws)
            .WithTitle("Built-in Dialogs")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .OnKeyPressed((sender, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow(window!);
                    e.Handled = true;
                }
            })
            .AddControl(Controls.Markup("[bold underline]Built-in Dialog Showcase[/]")
                .Centered()
                .Build())
            .AddControl(fileSectionLabel)
            .AddControl(openFileBtn)
            .AddControl(openFolderBtn)
            .AddControl(saveFileBtn)
            .AddControl(systemSectionLabel)
            .AddControl(aboutBtn)
            .AddControl(settingsBtn)
            .AddControl(performanceBtn)
            .AddControl(themeBtn)
            .AddControl(resultDisplay)
            .BuildAndShow();

        return window;

        #endregion
    }
}
