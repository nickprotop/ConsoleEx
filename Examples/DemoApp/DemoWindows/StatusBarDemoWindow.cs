using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class StatusBarDemoWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var statusBar = Controls.StatusBar()
            .AddLeft("\u2191\u2193", "Navigate")
            .AddLeft("Enter", "View")
            .AddLeftSeparator()
            .AddLeft("Esc", "Exit")
            .AddCenterText("[dim]StatusBarControl Demo[/]")
            .AddRightText("[yellow]3 outdated[/]")
            .AddRight("Ctrl+S", "Search")
            .WithAboveLine()
            .WithBackgroundColor(Color.Grey15)
            .WithShortcutForegroundColor(Color.Cyan1)
            .StickyBottom()
            .Build();

        var counterItem = statusBar.RightItems[0]; // "3 outdated"
        int counter = 3;

        var markup = Controls.Markup()
            .AddLine("[bold cyan]StatusBarControl Demo[/]")
            .AddLine("")
            .AddLine("This window shows a dedicated StatusBarControl at the bottom.")
            .AddLine("It supports three alignment zones (left, center, right),")
            .AddLine("clickable items with shortcut+label, and dynamic updates.")
            .AddLine("")
            .AddLine("[dim]Use the toolbar buttons below to interact:[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        var addItemBtn = Controls.Button()
            .WithText("  Add Left Item  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .Build();

        var removeItemBtn = Controls.Button()
            .WithText("  Remove Last Left  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .Build();

        var updateCounterBtn = Controls.Button()
            .WithText("  Increment Counter  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .Build();

        var toggleCenterBtn = Controls.Button()
            .WithText("  Toggle Center  ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .Build();

        // Place buttons in a toolbar (auto-height handles bordered buttons)
        var buttonToolbar = Controls.Toolbar()
            .WithSpacing(1)
            .WithWrap()
            .WithBackgroundColor(Color.Grey11)
            .WithMargin(1, 1, 1, 0)
            .Build();

        buttonToolbar.AddItem(addItemBtn);
        buttonToolbar.AddItem(removeItemBtn);
        buttonToolbar.AddItem(updateCounterBtn);
        buttonToolbar.AddItem(toggleCenterBtn);

        var clickLog = Controls.Markup()
            .AddLine("[dim]Click a status bar item to see it here...[/]")
            .WithMargin(1, 1, 1, 0)
            .Build();

        int addCount = 0;
        addItemBtn.Click += (_, _) =>
        {
            addCount++;
            statusBar.AddLeft($"F{addCount}", $"Action{addCount}");
        };

        removeItemBtn.Click += (_, _) =>
        {
            var items = statusBar.LeftItems;
            if (items.Count > 0)
                statusBar.RemoveLeft(items[items.Count - 1]);
        };

        updateCounterBtn.Click += (_, _) =>
        {
            counter++;
            counterItem.Label = $"[yellow]{counter} outdated[/]";
        };

        bool centerVisible = true;
        toggleCenterBtn.Click += (_, _) =>
        {
            centerVisible = !centerVisible;
            var centerItems = statusBar.CenterItems;
            if (centerItems.Count > 0)
                centerItems[0].IsVisible = centerVisible;
        };

        statusBar.ItemClicked += (sender, args) =>
        {
            if (!args.Item.IsSeparator)
            {
                var shortcut = args.Item.Shortcut;
                var label = args.Item.Label;
                var display = shortcut != null ? $"{shortcut}:{label}" : label;
                clickLog.SetContent(new List<string> { $"[green]Clicked:[/] {display}" });
            }
        };

        return new WindowBuilder(ws)
            .WithTitle("Status Bar Demo")
            .WithSize(90, 26)
            .Centered()
            .AddControl(markup)
            .AddControl(buttonToolbar)
            .AddControl(clickLog)
            .AddControl(statusBar)
            .BuildAndShow();
    }
}
