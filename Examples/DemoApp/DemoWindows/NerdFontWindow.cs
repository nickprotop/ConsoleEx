using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.NerdFont;

namespace DemoApp.DemoWindows;

internal static class NerdFontWindow
{
    private const int WindowWidth = 70;
    private const int WindowHeight = 30;

    public static Window Create(ConsoleWindowSystem ws)
    {
        string status = NerdFontHelper.IsSupported
            ? "[green]Detected[/]"
            : "[yellow]Not detected (ASCII fallbacks shown)[/]";

        var header = Controls.Markup($"[bold cyan]Nerd Font Icons[/]  -  NerdFont support: {status}")
            .Build();

        var fontAwesome = Controls.Markup("[bold yellow]FontAwesome[/]")
            .AddLine(FormatIconRow(Icons.FontAwesome.Folder, "Folder",
                                   Icons.FontAwesome.FolderOpen, "FolderOpen",
                                   Icons.FontAwesome.File, "File"))
            .AddLine(FormatIconRow(Icons.FontAwesome.FileCode, "FileCode",
                                   Icons.FontAwesome.Check, "Check",
                                   Icons.FontAwesome.Times, "Times"))
            .AddLine(FormatIconRow(Icons.FontAwesome.Star, "Star",
                                   Icons.FontAwesome.Heart, "Heart",
                                   Icons.FontAwesome.Warning, "Warning"))
            .AddLine(FormatIconRow(Icons.FontAwesome.Search, "Search",
                                   Icons.FontAwesome.Home, "Home",
                                   Icons.FontAwesome.Gear, "Gear"))
            .AddLine(FormatIconRow(Icons.FontAwesome.Terminal, "Terminal",
                                   Icons.FontAwesome.Database, "Database",
                                   Icons.FontAwesome.Cloud, "Cloud"))
            .AddLine(FormatIconRow(Icons.FontAwesome.Rocket, "Rocket",
                                   Icons.FontAwesome.Bug, "Bug",
                                   Icons.FontAwesome.Lock, "Lock"))
            .Build();

        var material = Controls.Markup("[bold yellow]Material Design[/]")
            .AddLine(FormatIconRow(Icons.Material.Home, "Home",
                                   Icons.Material.Cog, "Cog",
                                   Icons.Material.Account, "Account"))
            .AddLine(FormatIconRow(Icons.Material.Star, "Star",
                                   Icons.Material.Bell, "Bell",
                                   Icons.Material.Eye, "Eye"))
            .AddLine(FormatIconRow(Icons.Material.Rocket, "Rocket",
                                   Icons.Material.Lightning, "Lightning",
                                   Icons.Material.Fire, "Fire"))
            .Build();

        var devicons = Controls.Markup("[bold yellow]Devicons[/]")
            .AddLine(FormatIconRow(Icons.Devicons.CSharp, "CSharp",
                                   Icons.Devicons.Python, "Python",
                                   Icons.Devicons.JavaScript, "JavaScript"))
            .AddLine(FormatIconRow(Icons.Devicons.Docker, "Docker",
                                   Icons.Devicons.Git, "Git",
                                   Icons.Devicons.React, "React"))
            .AddLine(FormatIconRow(Icons.Devicons.Linux, "Linux",
                                   Icons.Devicons.Windows, "Windows",
                                   Icons.Devicons.Apple, "Apple"))
            .Build();

        var octicons = Controls.Markup("[bold yellow]Octicons[/]")
            .AddLine(FormatIconRow(Icons.Octicons.GitBranch, "GitBranch",
                                   Icons.Octicons.GitCommit, "GitCommit",
                                   Icons.Octicons.GitMerge, "GitMerge"))
            .AddLine(FormatIconRow(Icons.Octicons.GitPullRequest, "PullRequest",
                                   Icons.Octicons.Repo, "Repo",
                                   Icons.Octicons.IssueOpened, "Issue"))
            .Build();

        var powerline = Controls.Markup("[bold yellow]Powerline[/]")
            .AddLine(FormatIconRow(Icons.Powerline.RightArrow, "RightArrow",
                                   Icons.Powerline.LeftArrow, "LeftArrow",
                                   Icons.Powerline.Branch, "Branch"))
            .Build();

        var weather = Controls.Markup("[bold yellow]Weather[/]")
            .AddLine(FormatIconRow(Icons.Weather.Sunny, "Sunny",
                                   Icons.Weather.Rain, "Rain",
                                   Icons.Weather.Snow, "Snow"))
            .AddLine(FormatIconRow(Icons.Weather.Thunderstorm, "Storm",
                                   Icons.Weather.MoonFull, "MoonFull",
                                   Icons.Weather.Thermometer, "Thermo"))
            .Build();

        var fallbacks = Controls.Markup("[bold yellow]Fallbacks (auto-detect)[/]")
            .AddLine($"  {NerdFontHelper.Fallbacks.Folder}  Folder    " +
                     $"{NerdFontHelper.Fallbacks.File}  File    " +
                     $"{NerdFontHelper.Fallbacks.Check}  Check")
            .AddLine($"  {NerdFontHelper.Fallbacks.GitBranch}  Branch    " +
                     $"{NerdFontHelper.Fallbacks.Terminal}  Terminal    " +
                     $"{NerdFontHelper.Fallbacks.Rocket}  Rocket")
            .Build();

        var footer = Controls.Markup("[dim]Press [bold]ESC[/] to close[/]")
            .Centered()
            .Build();

        return new WindowBuilder(ws)
            .WithTitle("Nerd Font Icons")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .AddControls(
                header,
                Controls.Separator(),
                fontAwesome,
                material,
                devicons,
                octicons,
                powerline,
                weather,
                Controls.Separator(),
                fallbacks,
                footer)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }

    private static string FormatIconRow(string icon1, string name1,
                                         string icon2, string name2,
                                         string icon3, string name3)
    {
        const int columnWidth = 20;
        return $"  {icon1}  {name1.PadRight(columnWidth - 4)}" +
               $"{icon2}  {name2.PadRight(columnWidth - 4)}" +
               $"{icon3}  {name3}";
    }
}
