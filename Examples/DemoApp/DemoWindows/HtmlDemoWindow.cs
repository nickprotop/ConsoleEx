using DemoApp.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Themes;

namespace DemoApp.DemoWindows;

internal static class HtmlDemoWindow
{
	private const int WindowWidth = 124;
	private const int WindowHeight = 40;
	private const string HomePseudoUrl = "about:home";

	/// <summary>
	/// Theme-derived colours for the browser chrome.
	/// </summary>
	/// <remarks>
	/// Derived rather than hardcoded for the reason <see cref="DemoTheme"/> exists: a
	/// fixed dark chrome is unreadable the moment someone switches to a light theme,
	/// and this window has a theme switcher one keystroke away.
	/// </remarks>
	private readonly record struct Chrome(
		Color Surface, Color Raised, Color Border, Color Accent, Color Text, Color Dim)
	{
		public static Chrome From(ConsoleWindowSystem ws)
		{
			var theme = ws.Theme;
			Color bg = theme.WindowBackgroundColor;
			Color accent = theme.PrimaryColor ?? theme.ButtonFocusedBackgroundColor ?? bg.Tint(0.30);

			// A toolbar reads as chrome by sitting slightly off the page behind it.
			// Which direction "slightly off" is depends on the theme, so ask.
			Color surface = bg.IsDark() ? bg.Tint(0.05) : bg.Shade(0.05);
			Color raised = bg.IsDark() ? bg.Tint(0.12) : bg.Shade(0.10);
			Color text = PaletteColors.ReadableOn(surface);

			return new Chrome(
				Surface: surface,
				Raised: raised,
				Border: bg.Mix(accent, 0.35),
				Accent: PaletteColors.EnsureContrastRatio(accent, surface, PaletteColors.GraphicContrast),
				Text: text,
				Dim: PaletteColors.EnsureContrastRatio(text, surface, PaletteColors.TextContrast).Shade(0.25));
		}
	}

	/// <summary>
	/// The home page: a tour of what the renderer does, and a launch pad for real sites.
	/// </summary>
	private static string HomeHtml(string localPageUrl) => $$"""
        <h1>SharpConsoleUI HTML Control</h1>
        <p>A <b>native</b> HTML rendering widget for a .NET TUI compositor.
        Parses real websites with <i>AngleSharp</i>, lays out block/inline/table/grid
        with a custom engine, and renders inline images as half-block pixels —
        all inside a composited, multi-window terminal UI.</p>

        <h2>Images are sized the way the page means them</h2>
        <p>A width in a stylesheet and a width in an HTML attribute now mean the same
        thing: a fraction of the page. A thumbnail stays a thumbnail beside a hero
        image, and both grow when you widen the window. Open
        <a href="https://en.wikipedia.org/wiki/Cat">Wikipedia</a> and resize to watch it.</p>

        <table>
          <tr><th>Declared</th><th>At 120 columns</th><th>What it is</th></tr>
          <tr><td>32px</td><td>4 columns</td><td>an icon, left alone</td></tr>
          <tr><td>220px</td><td>26 columns</td><td>a thumbnail</td></tr>
          <tr><td>640px</td><td>75 columns</td><td>a content image</td></tr>
          <tr><td>1024px+</td><td>full width</td><td>a hero, clamped to the page</td></tr>
        </table>

        <h2>Page backgrounds</h2>
        <p>A page picks its colours for its own background. The toolbar's
        <b>&#9635;</b> button lets a page paint that background here too, live:
        Hacker News becomes its own cream page, textfiles.com its black one.
        Off by default, because this control sits inside a themed window. Either
        way text is held to a contrast floor against whichever surface it lands
        on, so a page can never render as invisible text.</p>

        <h2>Local files</h2>
        <p>The control reads <code>file://</code> URLs — pages and images both — so a page
        saved to disk opens with its pictures intact, resolved against its own folder.</p>
        <p><a href="{{localPageUrl}}">Open a local page from disk →</a></p>

        <h2>What else this page demonstrates</h2>
        <ul>
          <li><b>Text formatting:</b> <b>bold</b>, <i>italic</i>, <u>underline</u>,
              <del>strikethrough</del>, <code>inline code</code>, and <a href="about:home">internal links</a>.</li>
          <li><b>Lists</b> — ordered, unordered, and nested:
            <ul>
              <li>Compositor with z-order and gradients</li>
              <li>30+ built-in controls
                <ol>
                  <li>Trees, tables, date pickers, charts…</li>
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
        <p>Click any link below, or type an address. The control fetches the page,
        shows a loading banner, then renders it in place. Press
        <code>Alt+Home</code> or the Home button to come back here.</p>

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
            <tr><td><a href="http://textfiles.com">textfiles.com</a></td>
                <td>Old-school internet archive, pure ASCII charm (http only)</td></tr>
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
            <td>PNG/JPG → half-block pixels, progressive loading, file:// and data: URIs</td>
          </tr>
        </table>

        <h2>Horizontal rule</h2>
        <hr>

        <h2>Styled content</h2>
        <p style="color: cyan">Cyan colored text via inline CSS.</p>
        <p style="color: yellow; font-weight: bold">Bold yellow text.</p>
        <p style="color: #ff6b9d">Custom hex color (#ff6b9d).</p>
        """;

	/// <summary>
	/// Writes a small page and an image to a temp folder, so the file:// demo has
	/// something real to open.
	/// </summary>
	/// <remarks>
	/// Generated rather than shipped: it proves the interesting half of local
	/// loading, which is that a relative <c>src</c> resolves against the page's own
	/// directory rather than against the last site visited.
	/// </remarks>
	private static string CreateLocalSample()
	{
		var dir = Path.Combine(Path.GetTempPath(), "sharpconsoleui-html-demo");
		Directory.CreateDirectory(dir);

		var imagePath = Path.Combine(dir, "gradient.bmp");
		if (!File.Exists(imagePath))
			File.WriteAllBytes(imagePath, GradientBmp(320, 160));

		var pagePath = Path.Combine(dir, "local.html");
		File.WriteAllText(pagePath, $$"""
            <h1>A page from your disk</h1>
            <p>This page was read over <code>file://</code>. Nothing was downloaded.</p>
            <p>The image below is referenced as <code>&lt;img src="gradient.bmp"&gt;</code> —
            a <i>relative</i> path, resolved against this file's own folder:</p>
            <img src="gradient.bmp" width="320" alt="a gradient that failed to load">
            <h2>Why it matters</h2>
            <p>A page saved from a browser keeps its pictures in a folder beside it.
            Resolving relative sources against the document means such a page opens
            intact, rather than as a wall of alt text.</p>

            <h2>An image carried inside the markup</h2>
            <p>This one has no file at all. Its pixels are base64 in the
            <code>src</code> itself, as a <code>data:</code> URI, decoded during layout
            with nothing fetched: <img src="{{SwatchDataUri(240, 120)}}" width="240"
            alt="a blue-green swatch"> and the sentence continues underneath it.</p>
            <p>It also sits <i>inside</i> this paragraph rather than beside it. A
            terminal cell is twice as tall as it is wide, so a picture cannot share a
            line of text and stay recognisable — it takes its own rows at the point
            where it was written, and the words keep their order around it.</p>
            <ul>
              <li>A picture in a list item: <img src="{{SwatchDataUri(120, 60)}}" width="120" alt="a small swatch"></li>
              <li>The bullet stays on the text, and is not repeated after the rows.</li>
            </ul>
            <p><a href="about:home">← Back to the home page</a></p>
            """);

		return new Uri(pagePath).AbsoluteUri;
	}

	/// <summary>
	/// A colour swatch as a base64 <c>data:</c> URI, for an image carried inside
	/// the markup rather than fetched.
	/// </summary>
	/// <remarks>
	/// BMP keeps this honest about what it is demonstrating: no encoder, no
	/// library call that might itself be the thing under test. The bytes are the
	/// same ones <see cref="GradientBmp"/> builds, base64 of exactly that buffer,
	/// which is what a page means by an embedded image.
	/// </remarks>
	private static string SwatchDataUri(int width, int height) =>
		"data:image/bmp;base64," + Convert.ToBase64String(GradientBmp(width, height));

	/// <summary>
	/// A 24-bit BMP of a colour gradient, built by hand.
	/// </summary>
	/// <remarks>
	/// BMP because it needs no encoder: the header is a dozen fields and the
	/// pixels are raw. The library decodes PNG, JPEG, BMP, GIF and TGA, so this
	/// exercises the same path a downloaded image takes.
	/// </remarks>
	private static byte[] GradientBmp(int width, int height)
	{
		// Rows are padded to a multiple of four bytes, and stored bottom-up.
		int rowStride = (width * 3 + 3) & ~3;
		int pixelBytes = rowStride * height;
		const int headerSize = 54;

		var bmp = new byte[headerSize + pixelBytes];

		void WriteInt32(int offset, int value)
		{
			bmp[offset] = (byte)value;
			bmp[offset + 1] = (byte)(value >> 8);
			bmp[offset + 2] = (byte)(value >> 16);
			bmp[offset + 3] = (byte)(value >> 24);
		}

		bmp[0] = (byte)'B';
		bmp[1] = (byte)'M';
		WriteInt32(2, bmp.Length);
		WriteInt32(10, headerSize);
		WriteInt32(14, 40);          // DIB header size
		WriteInt32(18, width);
		WriteInt32(22, height);
		bmp[26] = 1;                 // colour planes
		bmp[28] = 24;                // bits per pixel
		WriteInt32(34, pixelBytes);

		for (int y = 0; y < height; y++)
		{
			int row = headerSize + (height - 1 - y) * rowStride;
			for (int x = 0; x < width; x++)
			{
				int i = row + x * 3;
				bmp[i] = (byte)(200 - 120 * x / width);   // blue
				bmp[i + 1] = (byte)(60 + 120 * y / height); // green
				bmp[i + 2] = (byte)(40 + 200 * x / width);  // red
			}
		}

		return bmp;
	}

	public static Window Create(ConsoleWindowSystem ws)
	{
		// Plain BMP Unicode glyphs — no fonts required, supported everywhere
		// SharpConsoleUI renders (Wcwidth handles width classification).
		const string iconHome = "\u2302";     // house
		const string iconBack = "\u2190";     // leftwards arrow
		const string iconForward = "\u2192";  // rightwards arrow
		const string iconReload = "\u21BB";   // clockwise open circle arrow
		const string iconGlobe = "\u25C9";    // fisheye, as an address-bar marker
		const string iconSearch = "\u2315";   // telephone recorder
		const string iconClose = "\u2715";    // multiplication x
		const string iconBookmark = "\u2605"; // black star
		const string iconBrowser = "\u25C9";  // fisheye, as a window-title marker

		var chrome = Chrome.From(ws);
		string localPageUrl = CreateLocalSample();
		string homeHtml = HomeHtml(localPageUrl);

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
			.WithContent(homeHtml)
			.WithShowImages(true)
			.WithHorizontalAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// Assigned once the window is built; the navigation closures above are
		// defined before it exists and capture it.
		Window? browserWindow = null;

		ButtonControl? backButton = null;
		ButtonControl? forwardButton = null;
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
			var display = url == HomePseudoUrl ? "[grey]about:home[/]" : $"[bold]{url}[/]";
			urlItem.Label = prefix + display;
		}

		void NavigateTo(string url, bool pushHistory, bool focusPage = false)
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
				htmlView.SetContent(homeHtml);
				UpdateStatusUrl(null, HomePseudoUrl);
			}
			else
			{
				UpdateStatusUrl("loading", url);
				_ = htmlView.LoadUrlAsync(url);
			}

			// Only the address bar hands the page focus on navigating. Typing an
			// address is a one-shot errand: you are done with the field and want
			// to be driving the document. Clicking a button is not - the toolbar
			// and the bookmarks bar are places you click again, and pulling focus
			// away on every press means the next click has to find its target
			// anew and the highlight jumps off the bar you are working in.
			if (focusPage)
				FocusPage();
		}

		void FocusPage() =>
			browserWindow?.FocusManager.SetFocus(htmlView, FocusReason.Programmatic);

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
			hoverItem.Label = args.Url != null ? $"[underline]{args.Url}[/]" : "";
		};

		htmlView.ContentLoaded += (_, _) => UpdateStatusUrl(null, currentUrl);
		htmlView.LoadError += (_, _) => UpdateStatusUrl("error", currentUrl);

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
		// Toolbar buttons — compact, so chrome costs one row instead of three
		// -------------------------------------------------------------------
		ButtonControl NavButton(string text, Action onClick) =>
			Controls.Button()
				.WithText(text)
				.WithBorder(ButtonBorderStyle.None)
				.WithBackgroundColor(chrome.Raised)
				.WithForegroundColor(chrome.Text)
				.WithFocusedColors(PaletteColors.ReadableOn(chrome.Accent), chrome.Accent)
				.OnClick((_, _) => onClick())
				.Build();

		var homeButton = NavButton($" {iconHome} ", () => NavigateTo(HomePseudoUrl, pushHistory: true));

		backButton = NavButton($" {iconBack} ", () =>
		{
			if (backStack.Count == 0) return;
			forwardStack.Push(currentUrl);
			NavigateTo(backStack.Pop(), pushHistory: false);
		});
		backButton.IsEnabled = false;

		forwardButton = NavButton($" {iconForward} ", () =>
		{
			if (forwardStack.Count == 0) return;
			backStack.Push(currentUrl);
			NavigateTo(forwardStack.Pop(), pushHistory: false);
		});
		forwardButton.IsEnabled = false;

		var reloadButton = NavButton($" {iconReload} ", () =>
		{
			if (currentUrl == HomePseudoUrl)
				htmlView.SetContent(homeHtml);
			else
				_ = htmlView.LoadUrlAsync(currentUrl);
		});

		var navToolbar = Controls.Toolbar()
			.WithSpacing(1)
			.Build();
		navToolbar.AddItem(backButton);
		navToolbar.AddItem(forwardButton);
		navToolbar.AddItem(reloadButton);
		navToolbar.AddItem(homeButton);

		// Page backgrounds, toggled live. The property re-runs layout, so the
		// page on screen changes under you - which is the quickest way to see
		// what the option actually does: Hacker News turns into its own cream
		// page, textfiles.com into its black one.
		ButtonControl? pageBgButton = null;
		pageBgButton = NavButton(" \u25A3 ", () =>
		{
			htmlView.UsePageBackground = !htmlView.UsePageBackground;
			pageBgButton!.Text = htmlView.UsePageBackground ? " \u25A3 " : " \u25A2 ";
			UpdateStatusUrl(null, currentUrl);
		});
		navToolbar.AddItem(pageBgButton);

		// The address bar carries its own surface so it reads as a field rather
		// than as a gap in the toolbar. It had none before, and against a slate
		// theme a borderless input on a slate toolbar is invisible until you
		// type into it.
		addressBar = Controls.Prompt($"{iconGlobe} ")
			.UnfocusOnEnter(false)
			.WithPlaceholder("Search or enter address")
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithInputBackgroundColor(chrome.Raised)
			.WithInputForegroundColor(chrome.Text)
			.WithInputFocusedBackgroundColor(chrome.Surface)
			.WithInputFocusedForegroundColor(chrome.Text)
			.OnEntered((_, url) =>
			{
				var address = url.Trim();
				if (string.IsNullOrEmpty(address))
				{
					NavigateTo(HomePseudoUrl, pushHistory: true, focusPage: true);
					return;
				}
				if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
					&& !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
					&& !address.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
					&& !address.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
				{
					address = "https://" + address;
				}
				NavigateTo(address, pushHistory: true, focusPage: true);
			})
			.Build();

		// Nav buttons and address bar share ONE row, the way a browser does.
		// Three stacked bars cost five of the window's rows before any page
		// showed; this costs one.
		var chromeRow = Controls.Grid()
			.Columns(GridLength.Auto(), GridLength.Star(1))
			.Rows(GridLength.Auto())
			.ColumnGap(1)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		chromeRow.Place(navToolbar, 0, 0);
		chromeRow.Place(addressBar, 0, 1);
		var navCell = chromeRow[0, 0];
		navCell.Background = chrome.Surface;
		var addressCell = chromeRow[0, 1];
		addressCell.Background = chrome.Surface;

		// -------------------------------------------------------------------
		// Bookmarks — quick launch, including the local page
		// -------------------------------------------------------------------
		ButtonControl BookmarkButton(string label, string url) =>
			Controls.Button()
				.WithText($" {label} ")
				.WithBorder(ButtonBorderStyle.None)
				.WithBackgroundColor(chrome.Surface)
				.WithForegroundColor(chrome.Text)
				.WithFocusedColors(PaletteColors.ReadableOn(chrome.Accent), chrome.Accent)
				.OnClick((_, _) => NavigateTo(url, pushHistory: true))
				.Build();

		// Ruled on both sides, so the bookmarks read as their own band rather than
		// as a second row of the toolbar above them. Both rules take the chrome
		// border colour instead of the default, which is a plain grey that reads
		// as a seam on a themed surface.
		var bookmarksToolbar = Controls.Toolbar()
			.WithSpacing(1)
			.WithContentPadding(1, 0, 1, 0)
			.WithAboveLine()
			.WithAboveLineColor(chrome.Border)
			.WithBelowLine()
			.WithBelowLineColor(chrome.Border)
			.Build();
		bookmarksToolbar.AddItem(Controls.Markup()
			.AddLine($"[{chrome.Dim.ToMarkup()}]{iconBookmark}[/]").Build());
		bookmarksToolbar.AddItem(BookmarkButton("Local page", localPageUrl));
		bookmarksToolbar.AddItem(BookmarkButton("Wikipedia Cat", "https://en.wikipedia.org/wiki/Cat"));
		bookmarksToolbar.AddItem(BookmarkButton("CNN Lite", "https://lite.cnn.com"));
		bookmarksToolbar.AddItem(BookmarkButton("Hacker News", "https://news.ycombinator.com"));
		bookmarksToolbar.AddItem(BookmarkButton("info.cern.ch", "https://info.cern.ch"));
		// http, not https: textfiles.com serves no TLS at all, and an https
		// bookmark simply hung until the fetch timed out.
		bookmarksToolbar.AddItem(BookmarkButton("textfiles", "http://textfiles.com"));

		// -------------------------------------------------------------------
		// Sticky bottom status bar
		// -------------------------------------------------------------------
		statusBar = Controls.StatusBar()
			.AddLeft(iconSearch, "Alt+D")
			.AddLeftSeparator()
			.AddLeftText("[grey]about:home[/]")
			.AddCenterText("")
			.AddRight(iconClose, "ESC to close")
			.AddRightSeparator()
			.AddRightText("[grey]—[/]")
			.WithAboveLine()
			.StickyBottom()
			.Build();

		urlItem = statusBar.LeftItems[2];
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
			.OnKeyPressed((s, e) =>
			{
				var win = (Window)s!;
				switch (e.KeyInfo.Key)
				{
					case ConsoleKey.Escape:
						ws.CloseWindow(win);
						e.Handled = true;
						break;

					// Alt+D, not the Ctrl+L a browser also uses: this demo registers
					// Ctrl+L globally to reopen the launcher, and a global shortcut
					// wins, so the handler here could never run and the status bar
					// advertised a key that did the opposite of what it said.
					case ConsoleKey.D when e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt):
						// Selected, not just focused, the way a browser does: the
						// address is offered ready to be replaced by the next
						// keystroke, but is still there to edit or leave alone.
						win.FocusManager.SetFocus(addressBar, FocusReason.Keyboard);
						addressBar!.SelectAll();
						e.Handled = true;
						break;

					// Alt+Home for the home page, as a browser does. Plain Home
					// belongs to whatever has focus: in the address bar it moves
					// the caret, and taking it here made that impossible.
					case ConsoleKey.Home when e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt):
						NavigateTo(HomePseudoUrl, pushHistory: true);
						e.Handled = true;
						break;

					case ConsoleKey.Backspace when e.KeyInfo.Modifiers == 0
						&& !(win.FocusManager.FocusedControl is PromptControl):
						if (backStack.Count > 0)
						{
							forwardStack.Push(currentUrl);
							NavigateTo(backStack.Pop(), pushHistory: false);
							e.Handled = true;
						}
						break;
				}
			})
			.AddControl(chromeRow)
			.AddControl(bookmarksToolbar)
			.AddControl(htmlView)
			.AddControl(statusBar)
			.BuildAndShow();

		browserWindow = window;

		// The page starts focused, so the scroll keys work before anything is
		// clicked.
		window.FocusManager.SetFocus(htmlView, FocusReason.Programmatic);

		DemoTheme.ApplyThemeGradient(window, ws);
		return window;
	}
}
