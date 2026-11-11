using Microsoft.Extensions.Logging;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using MelLevel = Microsoft.Extensions.Logging.LogLevel;
using OurLevel = SharpConsoleUI.Logging.LogLevel;

namespace DemoApp.DemoWindows;

/// <summary>
/// The logging showcase: <see cref="LogViewerControl"/> with every feature it has, fed from both
/// the library's own log service AND an application <c>ILogger</c> pipeline.
/// </summary>
/// <remarks>
/// <para>
/// There are three separate filters in play here and confusing them is the usual source of "my
/// logs are missing". From the outside in:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>ILogService.MinimumLevel</c> decides what is CAPTURED. Below it, an entry is never
/// recorded and no filter can bring it back. It defaults to Warning, which is why an app that
/// wires a bridge and logs Information sees nothing until it lowers this. The toolbar's Level
/// dropdown writes here.
/// </description></item>
/// <item><description>
/// <c>FilterLevel</c> and <c>FilterCategory</c> narrow the VIEW's projection. Entries stay in
/// the buffer and come back when the filter is relaxed.
/// </description></item>
/// <item><description>
/// The table's filter grammar, reached with '/', matches over the rendered rows:
/// <c>category:Order</c>, <c>level:ERROR</c>, plain text, AND/OR terms, fuzzy fallback.
/// </description></item>
/// </list>
/// <para>
/// The two bridges are the other half of the page. <c>SharpConsoleUILoggerProvider</c> carries
/// an application's <c>ILogger</c> entries INTO the viewer, so a TUI can show the logs of the
/// app it is embedded in. <c>LogServiceForwarder</c> carries the library's own entries OUT to
/// the application's pipeline, so window, focus and render diagnostics reach a real sink. Both
/// are wired here at once, which is the configuration that would loop without the re-entry
/// guard; the "Forwarded out" counter proves each entry crosses exactly once.
/// </para>
/// </remarks>
internal static class LogViewerWindow
{
	private const int WindowWidth = 118;
	private const int WindowHeight = 34;

	private const int BusyBurstCount = 40;
	private const int CategoryColumnHint = 14;
	private const int ActionPanelWidth = 22;

	// Enough to separate the panel from the window, not enough to read as a different control.
	private const double SidePanelContrast = 0.18;

	public static Window Create(ConsoleWindowSystem ws)
	{
		// Information, not Trace. At Trace the renderer logs a layout line per frame and the
		// pane becomes a wall of "Performing layout for window", which buries the entries this
		// page is about. The "Capture level" button cycles up to Trace for anyone who wants to
		// see that, and seeing it is itself the lesson about what the capture level costs.
		ws.LogService.MinimumLevel = OurLevel.Information;

		// OUTBOUND: the library's own entries go to a pipeline the demo owns. The sink counts
		// what it receives rather than writing anywhere, since a console sink would corrupt
		// the UI it is reporting on.
		var forwardedOut = new Counter();
		var pipeline = LoggerFactory.Create(b =>
		{
			b.SetMinimumLevel(MelLevel.Trace);
			b.AddProvider(new CountingLoggerProvider(forwardedOut));

			// INBOUND, in the same pipeline: anything logged through this factory also lands in
			// the viewer. Registering both directions over one log service is the cyclic case.
			b.AddProvider(new SharpConsoleUILoggerProvider(ws.LogService));
		});

		var forwarder = new LogServiceForwarder(ws.LogService, pipeline);

		// An application logger, exactly as an adopter's code would have it. The category is a
		// full type name, which is what makes the category column interesting.
		ILogger appLogger = pipeline.CreateLogger("DemoApp.Services.OrderProcessor");
		ILogger stockLogger = pipeline.CreateLogger("DemoApp.Services.StockChecker");

		var viewer = Controls.LogViewer(ws.LogService)
			.WithTitle("Library log + application ILogger")
			.WithGlobalFilterHotkey()     // '/' opens the filter from anywhere on this page
			.Build();

		var status = new MarkupControl(new List<string> { string.Empty })
		{
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		void RefreshStatus() => status.SetContent(new List<string>
		{
			$"[grey]capture[/] [yellow]{ws.LogService.MinimumLevel}[/]   " +
			$"[grey]buffered[/] [cyan]{ws.LogService.Count}[/]   " +
			$"[grey]view filter[/] [cyan]{viewer.FilterLevel}[/]   " +
			$"[grey]grammar[/] " + (viewer.IsFiltering
				? $"[green]{viewer.ActiveFilterText ?? "typing"}[/]"
				: "[grey]none[/]") + "   " +
			$"[grey]forwarded out[/] [magenta]{forwardedOut.Value}[/]"
		});

		// Seed a spread of levels and categories, from both sources, so the page is never empty.
		ws.LogService.LogInfo("Log showcase opened", "DemoApp");
		appLogger.LogInformation("order 10021 accepted");
		appLogger.LogWarning("order 10022 held for review");
		appLogger.LogError(new InvalidOperationException("card declined"), "order 10023 payment failed");
		stockLogger.LogInformation("stock check passed for 4 lines");
		stockLogger.LogDebug("cache hit ratio 0.92");
		ws.LogService.LogWarning("A library-side warning, forwarded to the pipeline too", "Window");
		RefreshStatus();

		// The actions live in two bordered panels down the right. A panel per GROUP rather
		// than one long column of buttons: the border and header do the work the dim text
		// labels were doing badly, and the two groups answer different questions ("put
		// something in the log" against "change what I am looking at").
		//
		// Each panel holds a GridControl with one Auto row per button, so the buttons stack and
		// stretch to the panel's inner width instead of each taking its own natural size.
		var feedButtons = Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(
				GridLength.Auto(),   // MEL: info
				GridLength.Auto(),   // MEL: error
				GridLength.Auto(),   // MEL: scoped
				GridLength.Auto(),   // ours: debug
				GridLength.Auto())   // burst
			.RowGap(1)
			.WithAlignment(HorizontalAlignment.Stretch);

		feedButtons.Place(Action("ILogger info", () =>
		{
			appLogger.LogInformation($"order {Random.Shared.Next(10000, 99999)} accepted");
			RefreshStatus();
		}), 0, 0);

		feedButtons.Place(Action("ILogger error", () =>
		{
			// An exception crosses the bridge intact: LogEntry has a field for it.
			appLogger.LogError(new TimeoutException("gateway timed out"), "order submission failed");
			RefreshStatus();
		}), 1, 0);

		feedButtons.Place(Action("ILogger scope", () =>
		{
			// Scopes and event ids have no field in LogEntry, so the bridge folds them into the
			// message rather than dropping them. The row shows them as a prefix.
			using (appLogger.BeginScope("Order=10042"))
			using (appLogger.BeginScope("Attempt=2"))
			{
				appLogger.Log(MelLevel.Information, new EventId(7, "Submitted"), "retrying submission");
			}
			RefreshStatus();
		}), 2, 0);

		feedButtons.Place(Action("Our LogService", () =>
		{
			// The library's own API. This one also goes OUT to the pipeline, which is what
			// moves the forwarded counter.
			ws.LogService.LogWarning($"a library-side warning at {DateTime.Now:HH:mm:ss}", "Render");
			RefreshStatus();
		}), 3, 0);

		feedButtons.Place(Action($"Burst of {BusyBurstCount}", () =>
		{
			// Volume from both sources at once: tail-follow and the virtualized table under
			// load, and proof the bridges do not amplify each other (the forwarded count rises
			// by the number of entries, not a multiple of it).
			for (int i = 0; i < BusyBurstCount; i++)
			{
				if (i % 3 == 0) stockLogger.LogInformation($"burst line {i}");
				else if (i % 3 == 1) ws.LogService.LogInfo($"burst note {i}", "Focus");
				else appLogger.LogWarning($"burst warning {i}");
			}
			RefreshStatus();
		}), 4, 0);

		var viewButtons = Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(
				GridLength.Auto(),   // errors only
				GridLength.Auto(),   // orders only
				GridLength.Auto(),   // clear
				GridLength.Auto(),   // row separators
				GridLength.Auto())   // capture level
			.RowGap(1)
			.WithAlignment(HorizontalAlignment.Stretch);

		viewButtons.Place(Action("Errors only", () =>
		{
			// The table's grammar, applied programmatically. '/' does the same by hand.
			viewer.ApplyFilterText("level:ERROR");
			RefreshStatus();
		}), 0, 0);

		viewButtons.Place(Action("Orders only", () =>
		{
			// Matches the FULL stored category even though the column shows only its tail.
			viewer.ApplyFilterText("category:OrderProcessor");
			RefreshStatus();
		}), 1, 0);

		viewButtons.Place(Action("Show everything", () =>
		{
			viewer.ClearFilterText();
			viewer.FilterLevel = OurLevel.Trace;
			RefreshStatus();
		}), 2, 0);

		// Click hands back the button itself, so the label retitles in place.
		var rowsButton = Controls.Button("Separators: off")
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		rowsButton.Click += (_, b) =>
		{
			viewer.ShowRowSeparators = !viewer.ShowRowSeparators;
			b.Text = viewer.ShowRowSeparators ? "Separators: on" : "Separators: off";
		};
		viewButtons.Place(rowsButton, 3, 0);

		var captureButton = Controls.Button(CaptureLabel(ws))
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		captureButton.Click += (_, b) =>
		{
			// What is RECORDED, not what is shown. At Trace the renderer logs a line per frame,
			// which is the clearest demonstration of what a low capture level costs.
			ws.LogService.MinimumLevel = ws.LogService.MinimumLevel switch
			{
				OurLevel.Trace => OurLevel.Information,
				OurLevel.Information => OurLevel.Warning,
				OurLevel.Warning => OurLevel.Error,
				_ => OurLevel.Trace
			};
			b.Text = CaptureLabel(ws);
			RefreshStatus();
		};
		viewButtons.Place(captureButton, 4, 0);

		var feedPanel = Controls.Panel()
			.WithHeader("Write a log entry")
			.HeaderCenter()
			.Rounded()
			.WithPadding(1, 0, 1, 0)
			.AddControl(feedButtons.Build())
			.StretchHorizontal()
			.Build();

		var viewPanel = Controls.Panel()
			.WithHeader("What you see")
			.HeaderCenter()
			.Rounded()
			.WithPadding(1, 0, 1, 0)
			.AddControl(viewButtons.Build())
			.StretchHorizontal()
			.Build();

		// Lift the two panels off the window with their own surface, derived from the theme
		// rather than a fixed colour: a step lighter on a dark theme, a step darker on a light
		// one, so the separation reads the same either way.
		void ApplyPanelSurface()
		{
			var surface = SidePanelSurface(ws);
			feedPanel.BackgroundColor = surface;
			viewPanel.BackgroundColor = surface;
		}

		ApplyPanelSurface();

		// Themes are switched from the launcher's dropdown while this page is open, so the
		// derived colour has to be recomputed rather than captured once at build time.
		EventHandler<SharpConsoleUI.Core.ThemeChangedEventArgs> onThemeChanged =
			(_, _) => ws.EnqueueOnUIThread(ApplyPanelSurface);
		ws.ThemeStateService.ThemeChanged += onThemeChanged;

		// Auto, Auto, then Star: the two panels take their natural height and the slack falls
		// below them, so they sit together at the top instead of being spread apart.
		var actionPanel = Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(GridLength.Auto(), GridLength.Auto(), GridLength.Star(1))
			.RowGap(1)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(feedPanel, 0, 0)
			.Place(viewPanel, 1, 0)
			.Build();

		var help = new MarkupControl(new List<string>
		{
			"[bold]Try it[/]  " +
			"[cyan]/[/] filter (works unfocused, this page opts in)   " +
			"[cyan]click a header[/] sort, cycles asc/desc/off   " +
			"[cyan]drag a column border[/] resize   " +
			"[cyan]select a row[/] detail pane",
			$"[grey]The Category column is {CategoryColumnHint} wide, so a full type name is shown by its TAIL: " +
			"\"DemoApp.Services.OrderProcessor\" reads as \"OrderProcessor\". The whole value is still " +
			"stored, so the grammar matches it. Widen the column to see it.[/]"
		})
		{
			Wrap = true,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		// The left column has its OWN rows, in its own grid. Sharing rows with the side panel
		// was the mistake: the panels are ~22 lines tall, a shared Auto row grows to the
		// tallest cell in it, and the log table (in the Star row) was squeezed to a single
		// line while the right column had all the height. Each column now sizes independently
		// and only the outer columns are shared.
		// A titled rule separates the page's own chrome (the hints and the status line) from
		// the log itself, which is otherwise one undivided column of text.
		var rule = Controls.Rule("log");

		var content = Controls.Grid()
			.Columns(GridLength.Star(1))
			.Rows(
				GridLength.Auto(),          // hints
				GridLength.Auto(),          // status line
				GridLength.Auto(),          // rule
				GridLength.Star(1, min: 8)) // the viewer takes the rest
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(help, 0, 0)
			.Place(status, 1, 0)
			.Place(rule, 2, 0)
			.Place(viewer, 3, 0)
			.Build();

		var grid = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Cells(ActionPanelWidth, min: 18))
			.Rows(GridLength.Star(1))
			.ColumnGap(2)
			.WithPadding(1, 0, 1, 0)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Place(content, 0, 0)
			.Place(actionPanel, 0, 1)
			.Build();

		var window = new WindowBuilder(ws)
			.WithTitle("Logging - two-way ILogger bridge, filter, sort, resize")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.AddControl(grid)
			// No Escape-to-close here. Escape belongs to the controls on the page: it cancels
			// the table's filter prompt and leaves a focused control. Closing the window on it
			// meant that cancelling a filter threw the whole page away.
			.BuildAndShow();

		// The forwarder holds a subscription on the log service, so it has to come off when the
		// page closes or a reopened page would forward twice.
		window.OnClosed += (_, _) =>
		{
			ws.ThemeStateService.ThemeChanged -= onThemeChanged;
			forwarder.Dispose();
			pipeline.Dispose();
		};

		return window;
	}

	/// <summary>
	/// The capture button's label, short enough not to truncate in the side panel.
	/// </summary>
	private static string CaptureLabel(ConsoleWindowSystem ws)
		=> $"Capture: {ws.LogService.MinimumLevel switch
		{
			OurLevel.Information => "Info",
			OurLevel.Critical => "Crit",
			var other => other.ToString()
		}}";

	/// <summary>
	/// A surface one step away from the window's own background, in whichever direction the
	/// theme leaves room for.
	/// </summary>
	private static Color SidePanelSurface(ConsoleWindowSystem ws)
	{
		var windowBackground = ws.Theme.WindowBackgroundColor;
		return windowBackground.IsDark()
			? windowBackground.Tint(SidePanelContrast)
			: windowBackground.Shade(SidePanelContrast);
	}

	/// <summary>
	/// One action button, stretched so a column of them lines up rather than each taking its
	/// own natural width.
	/// </summary>
	private static ButtonControl Action(string text, Action onClick)
	{
		// Stretch, so a column of them is one width rather than each button hugging its label.
		var button = Controls.Button(text)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();
		button.Click += (_, _) => onClick();
		return button;
	}

	/// <summary>A mutable count, so the sink and the status line share one number.</summary>
	private sealed class Counter
	{
		private int _value;
		public int Value => Volatile.Read(ref _value);
		public void Increment() => Interlocked.Increment(ref _value);
	}

	/// <summary>
	/// Stands in for a real sink (Seq, Serilog, a file). It only counts, because a console sink
	/// would write over the UI that is displaying it.
	/// </summary>
	private sealed class CountingLoggerProvider : ILoggerProvider
	{
		private readonly Counter _counter;

		public CountingLoggerProvider(Counter counter) => _counter = counter;

		public ILogger CreateLogger(string categoryName) => new CountingLogger(_counter);

		public void Dispose() { }

		private sealed class CountingLogger : ILogger
		{
			private readonly Counter _counter;

			public CountingLogger(Counter counter) => _counter = counter;

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();

			public bool IsEnabled(MelLevel logLevel) => true;

			public void Log<TState>(
				MelLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter)
				=> _counter.Increment();

			private sealed class NullScope : IDisposable
			{
				public void Dispose() { }
			}
		}
	}
}
