using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class HtmlDemoWindow
{
    private const int WindowWidth = 120;
    private const int WindowHeight = 38;
    private const string HomePseudoUrl = "about:home";

    /// <summary>
    /// A beefy showcase page that exercises every rendering feature we've wired up:
    /// headings, inline markup, nested tables (the recent bugfix), blockquotes,
    /// ordered/unordered lists with nesting, horizontal rules, preformatted code,
    /// styled text, and a quick-launch grid of live test sites.
    /// </summary>
    private const string HomeHtml = """
        <h1>SharpConsoleUI <span style="color: cyan">HTML Control</span></h1>
        <p>A <b>native</b> HTML rendering widget for a .NET TUI compositor.
        Parses real websites with <i>AngleSharp</i>, lays out block/inline/table/grid
        with a custom engine, and renders inline images as half-block pixels —
        all inside a composited, multi-window terminal UI.</p>

        <h2>What this page demonstrates</h2>
        <ul>
          <li><b>Text formatting:</b> <b>bold</b>, <i>italic</i>, <u>underline</u>,
              <del>strikethrough</del>, <code>inline code</code>, and <a href="about:home">internal links</a>.</li>
          <li><b>Lists</b> — ordered, unordered, and nested:
            <ul>
              <li>Compositor with z-order and gradients</li>
              <li>30+ built-in controls
                <ol>
                  <li>Trees, tables, date pickers, line graphs…</li>
                  <li>…and now an HTML browser</li>
                </ol>
              </li>
              <li>Embedded terminal emulator</li>
            </ul>
          </li>
          <li><b>Blockquotes</b>, <b>horizontal rules</b>, and <b>preformatted code blocks</b></li>
          <li><b>Tables</b> — including nested infobox-style layouts (see below)</li>
        </ul>

        <h2>Try browsing real websites</h2>
        <p>Click any of the links below. The HtmlControl will fetch the page,
        show a dim overlay + loading banner, then render it in place. Hit
        <code>Home</code> or the 🏠 button to come back here.</p>

        <table>
          <thead>
            <tr><th>Site</th><th>Why it's interesting</th></tr>
          </thead>
          <tbody>
            <tr><td><a href="https://en.wikipedia.org/wiki/Cat">Wikipedia: Cat</a></td>
                <td>Heavy article with infobox, nested tables, and ~20 images</td></tr>
            <tr><td><a href="https://lite.cnn.com">CNN Lite</a></td>
                <td>Text-only news front page — great link density</td></tr>
            <tr><td><a href="https://news.ycombinator.com">Hacker News</a></td>
                <td>Minimal HTML, lean layout, real-world anchors</td></tr>
            <tr><td><a href="https://info.cern.ch">info.cern.ch</a></td>
                <td>The first website ever published (1991)</td></tr>
            <tr><td><a href="https://textfiles.com">textfiles.com</a></td>
                <td>Old-school internet archive, pure ASCII charm</td></tr>
          </tbody>
        </table>

        <h2>Blockquote</h2>
        <blockquote>The terminal is not dead. It's just getting started.</blockquote>

        <h2>Code block</h2>
        <pre>var html = HtmlBuilder.Create()
            .WithContent("&lt;h1&gt;Hello, world&lt;/h1&gt;")
            .WithShowImages(true)
            .OnLinkClicked((s, e) =&gt; ((HtmlControl)s!).LoadUrlAsync(e.Url))
            .Build();</pre>

        <h2>Nested tables — the regression-tested case</h2>
        <table>
          <tr>
            <th>Feature</th><th>Details</th>
          </tr>
          <tr>
            <td><b>Layout engine</b></td>
            <td>
              <table>
                <tr><td>Block</td><td>paragraphs, headings, lists, blockquotes</td></tr>
                <tr><td>Inline</td><td>word-wrap, decorations, links</td></tr>
                <tr><td>Table</td><td>box-drawing borders, proportional columns</td></tr>
                <tr><td>Grid</td><td>display:grid + grid-template-columns</td></tr>
              </table>
            </td>
          </tr>
          <tr>
            <td><b>Images</b></td>
            <td>PNG/JPG → half-block pixels, progressive loading</td>
          </tr>
        </table>

        <h2>Horizontal rule</h2>
        <hr>

        <h2>Styled content</h2>
        <p style="color: cyan">Cyan colored text via inline CSS.</p>
        <p style="color: yellow; font-weight: bold">Bold yellow text.</p>
        <p style="color: #ff6b9d">Custom hex color (#ff6b9d).</p>
        """;

    public static Window Create(ConsoleWindowSystem ws)
    {
        // Plain BMP Unicode glyphs — no fonts required, supported everywhere SharpConsoleUI
        // renders (Wcwidth handles width classification).
        const string iconHome     = "\u2302"; // ⌂ HOUSE
        const string iconBack     = "\u2190"; // ← LEFTWARDS ARROW
        const string iconForward  = "\u2192"; // → RIGHTWARDS ARROW
        const string iconReload   = "\u21BB"; // ↻ CLOCKWISE OPEN CIRCLE ARROW
        const string iconGlobe    = "\u25C9"; // ◉ FISHEYE (used as address-bar prefix)
        const string iconSearch   = "\u2315"; // ⌕ TELEPHONE RECORDER
        const string iconClose    = "\u2715"; // ✕ MULTIPLICATION X
        const string iconBookmark = "\u2605"; // ★ BLACK STAR
        const string iconBrowser  = "\u25C9"; // ◉ window-title marker

        // -------------------------------------------------------------------
        // Navigation history (local to this window)
        // -------------------------------------------------------------------
        var backStack = new Stack<string>();
        var forwardStack = new Stack<string>();
        string currentUrl = HomePseudoUrl;

        // -------------------------------------------------------------------
        // HTML view
        // -------------------------------------------------------------------
        var htmlView = HtmlBuilder.Create()
            .WithContent(HomeHtml)
            .WithShowImages(true)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Build();

        // -------------------------------------------------------------------
        // Navigation controls
        // -------------------------------------------------------------------
        ButtonControl? backButton = null;
        ButtonControl? forwardButton = null;
        ButtonControl? homeButton = null;
        ButtonControl? reloadButton = null;
        PromptControl? addressBar = null;
        StatusBarControl? statusBar = null;
        StatusBarItem? urlItem = null;
        StatusBarItem? hoverItem = null;
        StatusBarItem? scrollItem = null;

        void UpdateNavButtonStates()
        {
            if (backButton != null) backButton.IsEnabled = backStack.Count > 0;
            if (forwardButton != null) forwardButton.IsEnabled = forwardStack.Count > 0;
        }

        void UpdateAddressBar(string url)
        {
            if (addressBar != null)
                addressBar.Input = url == HomePseudoUrl ? "" : url;
        }

        void UpdateStatusUrl(string? state, string url)
        {
            if (urlItem == null) return;
            var prefix = state != null ? $"[yellow]{state}[/] " : "";
            var display = url == HomePseudoUrl ? "[grey]about:home[/]" : $"[cyan]{url}[/]";
            urlItem.Label = prefix + display;
        }

        void NavigateTo(string url, bool pushHistory)
        {
            if (pushHistory && currentUrl != url)
            {
                backStack.Push(currentUrl);
                forwardStack.Clear();
            }
            currentUrl = url;
            UpdateAddressBar(url);
            UpdateNavButtonStates();

            if (url == HomePseudoUrl)
            {
                htmlView.SetContent(HomeHtml);
                UpdateStatusUrl(null, HomePseudoUrl);
            }
            else
            {
                UpdateStatusUrl("loading", url);
                _ = htmlView.LoadUrlAsync(url);
            }
        }

        // -------------------------------------------------------------------
        // HtmlControl event wiring
        // -------------------------------------------------------------------
        htmlView.LinkClicked += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.Url) || args.Url.StartsWith("#"))
                return;

            if (args.Url == HomePseudoUrl || args.Url.StartsWith("about:"))
            {
                NavigateTo(HomePseudoUrl, pushHistory: true);
                return;
            }

            NavigateTo(args.Url, pushHistory: true);
        };

        htmlView.LinkHover += (_, args) =>
        {
            if (hoverItem == null) return;
            hoverItem.Label = args.Url != null
                ? $"[underline cyan]{args.Url}[/]"
                : "";
        };

        htmlView.ContentLoaded += (_, _) =>
        {
            // Text is laid out — the page is readable. Clear the "loading" tag immediately.
            // Images may still be streaming in the background; the banner at the top of
            // the HtmlControl covers that, so the status bar stays clean.
            UpdateStatusUrl(null, currentUrl);
        };

        htmlView.LoadError += (_, args) =>
        {
            UpdateStatusUrl("error", currentUrl);
        };

        htmlView.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HtmlControl.ScrollOffset)
                || args.PropertyName == nameof(HtmlControl.ContentHeight))
            {
                UpdateScrollItem();
            }
        };

        void UpdateScrollItem()
        {
            if (scrollItem == null) return;
            int h = htmlView.ContentHeight;
            int o = htmlView.ScrollOffset;
            if (h == 0)
            {
                scrollItem.Label = "[grey]—[/]";
                return;
            }
            int pct = h > 0 ? Math.Clamp(100 * o / Math.Max(1, h), 0, 100) : 0;
            scrollItem.Label = $"[grey]{o}/{h}  ({pct}%)[/]";
        }

        // -------------------------------------------------------------------
        // Toolbar buttons
        // -------------------------------------------------------------------
        homeButton = Controls.Button()
            .WithText($" {iconHome} Home ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .OnClick((_, _) => NavigateTo(HomePseudoUrl, pushHistory: true))
            .Build();

        backButton = Controls.Button()
            .WithText($" {iconBack} Back ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .OnClick((_, _) =>
            {
                if (backStack.Count == 0) return;
                forwardStack.Push(currentUrl);
                var target = backStack.Pop();
                NavigateTo(target, pushHistory: false);
            })
            .Build();
        backButton.IsEnabled = false;

        forwardButton = Controls.Button()
            .WithText($" {iconForward} Forward ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .OnClick((_, _) =>
            {
                if (forwardStack.Count == 0) return;
                backStack.Push(currentUrl);
                var target = forwardStack.Pop();
                NavigateTo(target, pushHistory: false);
            })
            .Build();
        forwardButton.IsEnabled = false;

        reloadButton = Controls.Button()
            .WithText($" {iconReload} Reload ")
            .WithBorder(ButtonBorderStyle.Rounded)
            .OnClick((_, _) =>
            {
                if (currentUrl == HomePseudoUrl)
                    htmlView.SetContent(HomeHtml);
                else
                    _ = htmlView.LoadUrlAsync(currentUrl);
            })
            .Build();

        // Address bar — full URL input with Enter to navigate. Lives on its own row below
        // the button toolbar so it can use HorizontalAlignment.Stretch to fill the width
        // (ToolbarControl lays children out at their measured width and has no
        // "fill remaining space" child slot, so putting it inside the toolbar would
        // collapse it to its minimum ~10-column input field).
        addressBar = Controls.Prompt($"{iconGlobe}  ")
            .UnfocusOnEnter(false)
            .WithMargin(1, 0, 1, 0)
            .WithAlignment(HorizontalAlignment.Stretch)
            .OnEntered((_, url) =>
            {
                var address = url.Trim();
                if (string.IsNullOrEmpty(address))
                {
                    NavigateTo(HomePseudoUrl, pushHistory: true);
                    return;
                }
                if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    && !address.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    address = "https://" + address;
                }
                NavigateTo(address, pushHistory: true);
            })
            .Build();

        var navToolbar = Controls.Toolbar()
            .WithSpacing(1)
            .WithContentPadding(1, 0, 1, 0)
            .WithBackgroundColor(Color.Grey11)
            .Build();
        navToolbar.AddItem(homeButton);
        navToolbar.AddItem(backButton);
        navToolbar.AddItem(forwardButton);
        navToolbar.AddItem(reloadButton);

        // -------------------------------------------------------------------
        // Bookmarks bar — quick-launch buttons for stress-testing real sites
        // -------------------------------------------------------------------
        ButtonControl BookmarkButton(string label, string url) =>
            Controls.Button()
                .WithText($" {label} ")
                .WithBorder(ButtonBorderStyle.None)
                .WithColors(Color.Grey70, Color.Grey11)
                .WithFocusedColors(Color.White, Color.Blue)
                .OnClick((_, _) => NavigateTo(url, pushHistory: true))
                .Build();

        var bookmarksToolbar = Controls.Toolbar()
            .WithSpacing(2)
            .WithContentPadding(1, 0, 1, 0)
            .WithBelowLine()
            .WithBackgroundColor(Color.Grey11)
            .Build();
        bookmarksToolbar.AddItem(Controls.Markup().AddLine($"[dim]{iconBookmark}  Bookmarks:[/]").Build());
        bookmarksToolbar.AddItem(BookmarkButton("Wikipedia Cat", "https://en.wikipedia.org/wiki/Cat"));
        bookmarksToolbar.AddItem(BookmarkButton("CNN Lite", "https://lite.cnn.com"));
        bookmarksToolbar.AddItem(BookmarkButton("Hacker News", "https://news.ycombinator.com"));
        bookmarksToolbar.AddItem(BookmarkButton("info.cern.ch", "https://info.cern.ch"));
        bookmarksToolbar.AddItem(BookmarkButton("textfiles", "https://textfiles.com"));

        // -------------------------------------------------------------------
        // Sticky bottom status bar
        // -------------------------------------------------------------------
        statusBar = Controls.StatusBar()
            .AddLeft(iconSearch, "Ctrl+L")
            .AddLeftSeparator()
            .AddLeftText("[grey]about:home[/]")
            .AddCenterText("")
            .AddRight(iconClose, "ESC to close")
            .AddRightSeparator()
            .AddRightText("[grey]—[/]")
            .WithAboveLine()
            .WithBackgroundColor(Color.Grey15)
            .WithShortcutForegroundColor(Color.Cyan1)
            .StickyBottom()
            .Build();

        urlItem = statusBar.LeftItems[2];   // the address text
        hoverItem = statusBar.CenterItems[0];
        scrollItem = statusBar.RightItems[2];
        UpdateScrollItem();

        // -------------------------------------------------------------------
        // Window
        // -------------------------------------------------------------------
        var window = new WindowBuilder(ws)
            .WithTitle($"{iconBrowser}  HTML Browser")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithBackgroundGradient(
                ColorGradient.FromColors(new Color(10, 20, 45), new Color(5, 5, 15)),
                GradientDirection.Vertical)
            .OnKeyPressed((s, e) =>
            {
                switch (e.KeyInfo.Key)
                {
                    case ConsoleKey.Escape:
                        ws.CloseWindow((Window)s!);
                        e.Handled = true;
                        break;

                    case ConsoleKey.L when e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control):
                        // Ctrl+L focuses the address bar (classic browser shortcut)
                        ((Window)s!).FocusManager.SetFocus(addressBar, FocusReason.Keyboard);
                        e.Handled = true;
                        break;

                    case ConsoleKey.Backspace when e.KeyInfo.Modifiers == 0
                        && !(((Window)s!).FocusManager.FocusedControl is PromptControl):
                        // Backspace (outside the address bar) navigates back
                        if (backStack.Count > 0)
                        {
                            forwardStack.Push(currentUrl);
                            NavigateTo(backStack.Pop(), pushHistory: false);
                            e.Handled = true;
                        }
                        break;
                }
            })
            .AddControl(navToolbar)
            .AddControl(addressBar)
            .AddControl(bookmarksToolbar)
            .AddControl(htmlView)
            .AddControl(statusBar)
            .BuildAndShow();

        return window;
    }
}
