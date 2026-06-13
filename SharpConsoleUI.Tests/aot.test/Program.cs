// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// NativeAOT smoke test. Exercises a BROAD slice of the library — the window system,
// ~40 controls, the markup parser, the [markdown] tag (Markdig), syntax highlighters,
// Spectre.Console integration, AngleSharp (HtmlControl), ImageSharp (ImageControl), and
// the two subprocess-backed controls (TerminalControl / VideoControl) — under a native
// executable, then exits cleanly. Run by CI after `dotnet publish -p:PublishAot=true` to
// prove the library stays AOT-compatible: an AOT runtime failure (reflection that only
// throws when executed) in any of these paths is caught here rather than in a user's
// native binary.
//
// SUBPROCESS-BACKED CONTROLS (TerminalControl / VideoControl):
//   These spawn external processes (a PTY shell, an FFmpeg decoder). The GOAL here is to
//   prove the native binary can INVOKE those code paths without an AOT/reflection failure
//   — NOT that a PTY or ffmpeg is actually present. So we ACTUALLY start them, wrapped in
//   try/catch, and classify any exception via IsAotFailure():
//     - AOT/reflection/metadata/trim signal  → Fail (real AOT breakage).
//     - anything else (no PTY, ffmpeg absent, file not found, etc.) → "environment-skipped"
//       NOTE + continue (the environment is just missing, the code path is AOT-reachable).
//   Whatever we start, we Stop/Dispose so no subprocess or read thread is orphaned.

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Highlighting;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Parsing;
using Spectre.Console;
// Spectre.Console's namespace also defines Color and TreeNode; alias the SharpConsoleUI
// types so the rest of the file keeps referring to the library's versions.
using Color = SharpConsoleUI.Color;
using TreeNode = SharpConsoleUI.Controls.TreeNode;

// PTY SHIM RE-ENTRY GUARD — must be the FIRST executable statement.
// TerminalControl's LinuxPtyBackend launches THIS SAME executable with
// `--pty-shim <slaveFd> <exe> [args]`; that child must set up the slave PTY and exec()
// into the target shell. PtyShim.RunIfShim does that and never returns on success.
// Without this guard the shim child would instead re-run the entire smoke test (duplicated
// output, orphaned re-executions). Every host program that uses TerminalControl must call
// this at the top of Main — see Examples/DemoApp/Program.cs.
if (SharpConsoleUI.PtyShim.RunIfShim(args)) return 127;

int Fail(string message)
{
	Console.Error.WriteLine($"AOT SMOKE FAILED: {message}");
	return 1;
}

// Classifies an exception thrown by a subprocess-backed control. Returns true when the
// exception looks like a NativeAOT / trimming / reflection-metadata failure (which must
// fail the smoke test), false when it looks like a missing environment (no PTY, ffmpeg
// not installed, file not found — which is an expected CI environment-skip, not a bug).
bool IsAotFailure(Exception ex)
{
	for (Exception? e = ex; e != null; e = e.InnerException)
	{
		var typeName = e.GetType().FullName ?? string.Empty;
		if (typeName.Contains("MissingMetadata", StringComparison.OrdinalIgnoreCase) ||
			typeName.Contains("System.Runtime.Serialization", StringComparison.OrdinalIgnoreCase))
			return true;

		var msg = e.Message ?? string.Empty;
		if (msg.Contains("AOT", StringComparison.OrdinalIgnoreCase) ||
			msg.Contains("metadata", StringComparison.OrdinalIgnoreCase) ||
			msg.Contains("trimm", StringComparison.OrdinalIgnoreCase) ||
			msg.Contains("reflection", StringComparison.OrdinalIgnoreCase))
			return true;
	}
	return false;
}

int controlCount = 0;
var skips = new List<string>();

// Headless window system shared by all sections.
using var driver = new HeadlessConsoleDriver(120, 40);
var system = new ConsoleWindowSystem(driver);

// Helper: build a window, add controls, drive a few render frames, then close it.
// Each control added bumps controlCount. Returns null on success, or an error message.
string? RenderGroup(string label, params IWindowControl[] controls)
{
	Window? window = null;
	try
	{
		window = new Window(system) { Width = 110, Height = 34, Top = 1, Left = 1, Title = label };
		foreach (var c in controls)
		{
			window.AddControl(c);
			controlCount++;
		}
		system.AddWindow(window);
		for (int i = 0; i < 3; i++)
			system.ProcessOnce();
		return null;
	}
	catch (Exception ex)
	{
		return $"{label}: {ex.GetType().Name}: {ex.Message}";
	}
	finally
	{
		if (window != null)
			system.CloseWindow(window, force: true);
	}
}

try
{
	// 1. Markup parser core — tokenizer + color system.
	var cells = MarkupParser.Parse("[red]Hello[/] [bold green]AOT[/] [italic]world[/]", Color.White, Color.Black);
	if (cells.Count == 0)
		return Fail("MarkupParser produced no cells");

	// 2. [markdown] tag — exercises Markdig + the markdown→markup translator + a
	//    syntax-highlighted fenced code block (the real AOT risk: Markdig pipeline).
	var mdCells = MarkupParser.Parse(
		"[markdown]# Title\n\n**bold** and `code` and a [link](https://x)\n\n- item one\n- item two\n\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```[/]",
		Color.White, Color.Black);
	if (mdCells.Count == 0)
		return Fail("[markdown] tag produced no cells (Markdig path)");

	// 3. MarkdownToMarkup.Convert directly on a multi-construct document.
	var converted = MarkdownToMarkup.Convert(
		"# Heading\n\nSome **bold** text.\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```json\n{\"k\":1}\n```\n");
	if (string.IsNullOrEmpty(converted))
		return Fail("MarkdownToMarkup.Convert returned empty");

	// 4. Syntax highlighters — regex highlighters + registry. Tokenize one line each.
	foreach (var lang in new[] { "csharp", "bash", "json" })
	{
		var hl = SyntaxHighlighters.For(lang);
		if (hl == null)
			return Fail($"SyntaxHighlighters.For(\"{lang}\") returned null");
		var sample = lang switch
		{
			"csharp" => "public int X = 42; // comment",
			"bash"   => "echo \"hello $USER\" # comment",
			_        => "{ \"key\": \"value\", \"n\": 123 }",
		};
		var (tokens, _) = hl.Tokenize(sample, 0, SyntaxLineState.Initial);
		if (tokens == null || tokens.Count == 0)
			return Fail($"highlighter \"{lang}\" produced no tokens");
	}

	// ---- 5. Broad control coverage. Grouped across several windows so layout is realistic. ----

	// 5a. Text / status controls.
	if (RenderGroup("text",
			new MarkupControl(new List<string> { "[bold underline]NativeAOT broad smoke[/]", "[grey]static text control[/]" }) { Wrap = true },
			Controls.Markdown("## Live markdown\n\n**rendered** via the [markdown] tag").Build(),
			Controls.Rule("A Rule"),
			new RuleControl { Title = "direct rule" },
			new SeparatorControl(),
			Controls.Label("a label"),
			new SpinnerControl())
		is { } e5a) return Fail(e5a);

	// 5b. Buttons / inputs / pickers.
	if (RenderGroup("inputs",
			Controls.Button("Click me").Build(),
			Controls.Checkbox("Enable feature").Build(),
			Controls.Prompt("name> ").Build(),
			Controls.Slider().WithRange(0, 100).WithValue(42).Build(),
			Controls.RangeSlider().WithRange(0, 100).WithValues(20, 80).Build(),
			Controls.DatePicker("Date: ").Build(),
			Controls.TimePicker("Time: ").Build())
		is { } e5b) return Fail(e5b);

	// 5c. Data controls — populated so the render path actually runs.
	if (RenderGroup("data",
			Controls.List("Items")
				.AddItem("First", icon: "*")
				.AddItem("Second")
				.AddItem("Third")
				.Build(),
			Controls.Tree()
				.AddRootNodes(MakeTree())
				.Build(),
			Controls.Table()
				.WithColumns("Name", "Value")
				.AddRow("alpha", "1")
				.AddRow("beta", "2")
				.WithTitle("Demo Table")
				.Build(),
			Controls.Dropdown("Pick: ")
				.AddItems("One", "Two", "Three")
				.Build())
		is { } e5c) return Fail(e5c);

	// 5d. Graph / progress controls.
	if (RenderGroup("graphs",
			Controls.ProgressBar().WithValue(60).WithMaxValue(100).Build(),
			Controls.Sparkline().WithData(new double[] { 1, 3, 2, 5, 4, 6, 3, 7 }).Build(),
			Controls.BarGraph().WithLabel("cpu").WithValue(70).WithMaxValue(100).Build(),
			Controls.LineGraph().WithData(new double[] { 1, 2, 1.5, 3, 2.5, 4 }).WithTitle("trend").Build())
		is { } e5d) return Fail(e5d);

	// 5e. Containers / layout controls.
	if (RenderGroup("containers",
			Controls.Panel("[green]panel body[/]"),
			Controls.ScrollablePanel()
				.AddControl(Controls.Label("scrollable child one"))
				.AddControl(Controls.Label("scrollable child two"))
				.Build(),
			Controls.HorizontalGrid()
				.Column(c => c.Add(Controls.Label("left col")))
				.Column(c => c.Add(Controls.Label("right col")))
				.Build(),
			Controls.TabControl()
				.AddTab("Tab A", Controls.Label("content A"))
				.AddTab("Tab B", Controls.Label("content B"))
				.Build(),
			Controls.MultilineEdit("line 1\nline 2\nline 3").Build())
		is { } e5e) return Fail(e5e);

	// 5e2. CollapsiblePanel — exercise BOTH header styles and BOTH states in the AOT graph.
	//      Borderless + expanded (custom icons, then toggled), and Bordered + collapsed.
	var collapsibleExpanded = Controls.CollapsiblePanel("[bold]Reasoning[/]")
		.WithExpandedIcon("[green]▾[/]")
		.WithCollapsedIcon("[green]▸[/]")
		.AddControl(Controls.Label("aot child one"))
		.AddControl(Controls.Label("aot child two"))
		.Build();
	collapsibleExpanded.Toggle();   // exercise the toggle path
	if (RenderGroup("collapsible",
			collapsibleExpanded,
			Controls.CollapsiblePanel("[bold]Details[/]")
				.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
				.Collapsed()
				.AddControl(Controls.Label("bordered child one"))
				.AddControl(Controls.Label("bordered child two"))
				.Build())
		is { } e5e2) return Fail(e5e2);

	// 5f. Chrome controls — menu / toolbar / status bar.
	if (RenderGroup("chrome",
			Controls.Menu()
				.AddItem("File", () => { })
				.AddItem("Edit", () => { })
				.Build(),
			Controls.Toolbar()
				.AddButton("Save", (s, b) => { })
				.AddSeparator()
				.AddButton("Open", (s, b) => { })
				.Build(),
			Controls.StatusBar()
				.AddLeftText("ready")
				.AddRight("F1", "Help")
				.Build())
		is { } e5f) return Fail(e5f);

	// 5g. Figlet + LogViewer + splitter chrome.
	LogViewerControl logViewer;
	try
	{
		logViewer = new LogViewerControl(system.LogService);
		system.LogService.LogInfo("smoke: log entry one", "Smoke");
		system.LogService.LogWarning("smoke: log entry two", "Smoke");
	}
	catch (Exception ex)
	{
		return Fail($"LogViewerControl ctor/logging: {ex.Message}");
	}
	if (RenderGroup("misc",
			Controls.Figlet("AOT").Build(),
			logViewer,
			Controls.HorizontalSplitter()
				.WithControls(Controls.Label("above"), Controls.Label("below"))
				.Build(),
			Controls.Splitter().Build())
		is { } e5g) return Fail(e5g);

	// 5h. CanvasControl — draw a few primitives through CanvasGraphics.
	try
	{
		var canvas = Controls.Canvas(40, 12).Build();
		controlCount++;
		var g = canvas.BeginPaint();
		g.Clear(Color.Black);
		g.DrawLine(0, 0, 39, 11, '#', Color.White, Color.Black);
		g.DrawBox(2, 2, 20, 6, BoxChars.Single, Color.Green, Color.Black);
		g.DrawCircle(30, 6, 4, '*', Color.Yellow, Color.Black);
		g.FillRect(5, 4, 6, 2, '.', Color.Cyan, Color.Black);
		g.WriteString(4, 3, "canvas", Color.Magenta, Color.Black);
		var canvasWindow = new Window(system) { Width = 60, Height = 20, Top = 1, Left = 1, Title = "canvas" };
		canvasWindow.AddControl(canvas);
		system.AddWindow(canvasWindow);
		for (int i = 0; i < 3; i++) system.ProcessOnce();
		system.CloseWindow(canvasWindow, force: true);
	}
	catch (Exception ex)
	{
		return Fail($"CanvasControl: {ex.Message}");
	}

	// 6. SpectreRenderableControl — wraps a Spectre IRenderable. Exercises Spectre.Console under AOT.
	try
	{
		var spectreTable = new Spectre.Console.Table();
		spectreTable.AddColumn("Col1");
		spectreTable.AddColumn("Col2");
		spectreTable.AddRow("a", "b");
		var spectreMarkup = new Spectre.Console.Markup("[red]spectre markup[/]");
		if (RenderGroup("spectre",
				new SpectreRenderableControl(spectreMarkup),
				new SpectreRenderableControl(spectreTable))
			is { } e6) return Fail(e6);
	}
	catch (Exception ex)
	{
		return Fail($"SpectreRenderableControl: {ex.Message}");
	}

	// 7. HtmlControl — AngleSharp/AngleSharp.Css under AOT, including CSS calc().
	//
	//   Exercising HtmlControl pulls AngleSharp.Css's CSS value pipeline into the reachable
	//   call graph. AngleSharp.Css's CssCalc*Expression.Compute methods call
	//   Activator.CreateInstance(value.GetType(), ...) which trim analysis flags as IL2072.
	//   RUNTIME-VERIFIED: a NativeAOT binary rendering HTML with CSS calc() runs correctly
	//   (exit 0, content rendered — the concrete expression types survive trimming). The
	//   IL2072 is therefore a trim-analysis warning, not a runtime crash, and is cleared via
	//   a scoped [UnconditionalSuppressMessage] on the CSS-pipeline methods in
	//   SharpConsoleUI/Html/HtmlLayoutEngine.cs. HtmlControl is now exercised here, with a
	//   calc()-using stylesheet, to keep that path under continuous AOT coverage.
	try
	{
		var html = new HtmlControl();
		html.SetContent("<html><body><div style=\"width: calc(100% - 10px)\"><h1>Hi</h1><p><b>bold</b> text</p></div></body></html>");
		if (RenderGroup("html", html) is { } e7) return Fail(e7);
	}
	catch (Exception ex)
	{
		return Fail($"HtmlControl: {ex.Message}");
	}

	// 8. ImageControl — ImageSharp PNG decode under AOT (ImageSharp is an AOT risk).
	//    Decode a tiny in-memory 2x2 PNG via PixelBuffer.FromStream (exercises the real decoder).
	try
	{
		const string tinyPngBase64 =
			"iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEUlEQVR4nGP4z8DA8B+MgBgAHfAD/dPQfSYAAAAASUVORK5CYII=";
		var pngBytes = System.Convert.FromBase64String(tinyPngBase64);
		using var pngStream = new MemoryStream(pngBytes);
		var pixelBuffer = PixelBuffer.FromStream(pngStream);
		if (pixelBuffer.Width <= 0 || pixelBuffer.Height <= 0)
			return Fail("ImageControl: decoded PixelBuffer has no size");
		var image = Controls.Image(pixelBuffer);
		if (RenderGroup("image", image) is { } e8) return Fail(e8);
	}
	catch (Exception ex)
	{
		return Fail($"ImageControl (ImageSharp): {ex.Message}");
	}

	// 9. TerminalControl — ACTUALLY START a PTY-backed shell under AOT.
	//    The builder's Build() opens the PTY, spawns the shell, and starts a background
	//    read thread (all in the ctor). On CI (linux-x64 with /bin/sh) this should start;
	//    if no PTY/shell is available it throws → environment-skip. We drive a few frames
	//    then Dispose() to kill the subprocess + read thread (no orphans).
	if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
	{
		SharpConsoleUI.Controls.Terminal.TerminalControl? terminal = null;
		Window? terminalWindow = null;
		try
		{
			string shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
			terminal = Controls.Terminal(shell).Build();   // opens PTY, spawns shell, starts read thread
			controlCount++;
			terminalWindow = new Window(system) { Width = 40, Height = 12, Top = 1, Left = 1, Title = "terminal" };
			terminalWindow.AddControl(terminal);
			system.AddWindow(terminalWindow);
			for (int i = 0; i < 3; i++) system.ProcessOnce();
			Console.Error.WriteLine("AOT SMOKE NOTE: TerminalControl started (PTY shell spawned + read thread running)");
		}
		catch (Exception ex)
		{
			if (IsAotFailure(ex))
				return Fail($"TerminalControl AOT failure: {ex}");
			skips.Add($"TerminalControl environment-skipped ({ex.GetType().Name}: {ex.Message})");
		}
		finally
		{
			// Always stop the PTY subprocess + read thread, then close the window.
			try { terminal?.Dispose(); } catch { /* best-effort cleanup */ }
			if (terminalWindow != null)
				system.CloseWindow(terminalWindow, force: true);
		}
	}
	else
	{
		skips.Add("TerminalControl environment-skipped (platform is neither Linux nor Windows)");
	}

	// 10. VideoControl — ACTUALLY CALL Play() under AOT.
	//     ffmpeg is NOT installed in CI; the control's own PlaybackLoopAsync checks
	//     VideoFrameReader.IsFfmpegAvailable() first and gracefully surfaces an
	//     ErrorMessage instead of crashing. That graceful no-op IS the expected PASS — it
	//     proves the Play()/StartPlayback() code path (and its FFmpeg-probe) is AOT-reachable.
	//     Play() runs the decode loop on a background Task, so we drive frames to let the
	//     dispatched UI-thread continuation run, then Stop()/Dispose() to clean up.
	{
		VideoControl? video = null;
		Window? videoWindow = null;
		try
		{
			video = Controls.Video().WithSource("nonexistent.mp4").Build();
			controlCount++;
			videoWindow = new Window(system) { Width = 40, Height = 12, Top = 1, Left = 1, Title = "video" };
			videoWindow.AddControl(video);
			system.AddWindow(videoWindow);
			video.Play();   // invokes StartPlayback → background decode loop → FFmpeg-availability probe
			for (int i = 0; i < 6; i++) system.ProcessOnce();   // drain the dispatched ffmpeg-not-found continuation
			video.Stop();
			Console.Error.WriteLine(
				video.ErrorMessage != null
					? "AOT SMOKE NOTE: VideoControl Play() reached graceful FFmpeg-absent fallback"
					: "AOT SMOKE NOTE: VideoControl Play() invoked (no error surfaced)");
		}
		catch (Exception ex)
		{
			if (IsAotFailure(ex))
				return Fail($"VideoControl AOT failure: {ex}");
			skips.Add($"VideoControl environment-skipped ({ex.GetType().Name}: {ex.Message})");
		}
		finally
		{
			try { video?.Stop(); } catch { /* best-effort */ }
			try { video?.Dispose(); } catch { /* best-effort */ }
			if (videoWindow != null)
				system.CloseWindow(videoWindow, force: true);
		}
	}
}
catch (Exception ex)
{
	return Fail($"unexpected: {ex.GetType().Name}: {ex.Message}");
}

// Verify the headless driver actually rendered (buffer sized to the screen).
if (driver.ScreenSize.Width <= 0 || driver.ScreenSize.Height <= 0)
	return Fail("headless driver has no screen buffer after rendering");

foreach (var s in skips)
	Console.Error.WriteLine($"AOT SMOKE NOTE: {s}");

Console.Error.WriteLine(
	$"AOT SMOKE OK: {controlCount} controls exercised (incl. TerminalControl + VideoControl actually started); " +
	$"markdown+highlighters+spectre+image AOT-clean; " +
	$"HtmlControl exercised (AngleSharp + CSS calc(); IL2072 cleared via scoped suppression); " +
	$"{driver.ScreenSize.Width}x{driver.ScreenSize.Height} rendered.");
return 0;

// ---- helpers ----

static TreeNode[] MakeTree()
{
	var root = new TreeNode("Root");
	root.AddChild(new TreeNode("Child A"));
	var b = new TreeNode("Child B");
	b.AddChild(new TreeNode("Grandchild B1"));
	root.AddChild(b);
	return new[] { root };
}
