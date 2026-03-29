using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Diagnostics;
using SharpConsoleUI.Tests.Infrastructure;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Rendering.Performance;

/// <summary>
/// Rendering performance benchmark suite. Run manually to measure frame times,
/// throughput, and scaling across scenarios. Outputs formatted reports to test output.
///
/// Usage:
///   dotnet test --filter "Category=Benchmark" --logger "console;verbosity=detailed"
///   dotnet test --filter "Category=Profile"   --logger "console;verbosity=detailed"
/// </summary>
public class RenderingBenchmarks
{
	private const int WarmupFrames = 10;
	private const int BenchmarkFrames = 500;

	private readonly ITestOutputHelper _output;

	private static bool IsBenchmarkRun =>
		Environment.GetEnvironmentVariable("SHARPCONSOLEUI_BENCHMARK") == "1";

	public RenderingBenchmarks(ITestOutputHelper output) => _output = output;

	#region Benchmark Scenarios

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_FullRedraw_AlphaBlending()
	{
		if (!IsBenchmarkRun) return;
		var (system, window) = CreateAlphaBlendingScene(110, 38);
		var result = RunBenchmark(system, window,
			(w, i) => RotateGradient(w, i),
			"Full Redraw — Alpha Blending (110x38)");

		PrintResult(result);
		PrintFrameBudget(result);
	}

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_StaticFrame_IdleCost()
	{
		if (!IsBenchmarkRun) return;
		var (system, window) = CreateAlphaBlendingScene(110, 38);

		// Initial render to populate buffers
		system.Render.UpdateDisplay();

		// Benchmark static frames — no mutations
		var result = RunBenchmark(system, window,
			(w, i) => { /* no changes */ },
			"Static Frame — Idle Cost (110x38)");

		PrintResult(result);
		Assert.Equal(0, result.LastMetrics?.BytesWritten ?? -1);
	}

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_PartialUpdate_SingleControl()
	{
		if (!IsBenchmarkRun) return;
		var (system, window, label) = CreatePartialUpdateScene(110, 38);

		// Initial render
		system.Render.UpdateDisplay();

		// Benchmark: change one label per frame
		var result = RunBenchmark(system, window,
			(w, i) => label.SetContent(new List<string> { $"Counter: {i:D6}" }),
			"Partial Update — Single Label (110x38)");

		PrintResult(result);
	}

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_WindowOverlap_Compositing()
	{
		if (!IsBenchmarkRun) return;
		const int Size = 130;
		var system = TestWindowSystemBuilder.CreateTestSystem(Size, 50);

		// Create 3 overlapping windows
		var windows = new Window[3];
		for (int i = 0; i < 3; i++)
		{
			var w = new Window(system)
			{
				Left = 5 + i * 15,
				Top = 2 + i * 5,
				Width = 60,
				Height = 25,
				Title = $"Window {i + 1}"
			};
			w.AddControl(new MarkupControl(new List<string>
			{
				$"[bold]Overlapping Window {i + 1}[/]",
				"This window overlaps with others.",
				"The compositor must handle z-order."
			}));
			w.BackgroundGradient = new GradientBackground(
				ColorGradient.FromColors(
					new Color((byte)(50 + i * 60), 100, 200),
					new Color(200, (byte)(50 + i * 60), 100)),
				GradientDirection.DiagonalDown);
			system.WindowStateService.AddWindow(w);
			windows[i] = w;
		}

		// Initial render
		system.Render.UpdateDisplay();

		// Benchmark: cycle active window each frame (triggers z-order recompositing)
		var result = RunBenchmark(system, windows[0],
			(w, i) =>
			{
				var target = windows[i % 3];
				system.WindowStateService.SetActiveWindow(target);
				target.IsDirty = true;
			},
			"Window Overlap — 3 Windows Cycling (130x50)");

		PrintResult(result);
	}

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_BufferScaling()
	{
		if (!IsBenchmarkRun) return;
		var sizes = new[] { (80, 25), (120, 40), (200, 50), (300, 80) };
		var results = new List<(string label, BenchmarkResult result)>();

		foreach (var (w, h) in sizes)
		{
			var (system, window) = CreateAlphaBlendingScene(w - 10, h - 5, screenW: w, screenH: h);
			var result = RunBenchmark(system, window,
				(win, i) => RotateGradient(win, i),
				$"Buffer {w}x{h}");
			results.Add(($"{w}x{h} ({w * h} cells)", result));
		}

		PrintScalingTable(results);
	}

	[Fact]
	[Trait("Category", "Benchmark")]
	public void Bench_DeepControlTree()
	{
		if (!IsBenchmarkRun) return;
		var system = TestWindowSystemBuilder.CreateTestSystem(140, 50);
		var window = new Window(system)
		{
			Left = 2, Top = 2, Width = 130, Height = 42,
			Title = "Deep Control Tree"
		};

		// 4-level nested grids: 4 columns x 3 rows of panels, each with markup
		var outerGrid = SharpConsoleUI.Builders.Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill);

		for (int col = 0; col < 4; col++)
		{
			var innerPanel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.WithBackgroundColor(new Color((byte)(40 + col * 30), 60, 120, 200))
				.Build();

			for (int row = 0; row < 5; row++)
			{
				var nestedGrid = SharpConsoleUI.Builders.Controls.HorizontalGrid()
					.WithAlignment(HorizontalAlignment.Stretch);

				for (int sub = 0; sub < 3; sub++)
				{
					nestedGrid = nestedGrid.Column(c => c.Flex(1)
						.Add(SharpConsoleUI.Builders.Controls.Markup()
							.AddLine($"[dim]C{col}R{row}S{sub}[/]")
							.Build()));
				}
				innerPanel.AddControl(nestedGrid.Build());
			}

			outerGrid = outerGrid.Column(c => c.Flex(1).Add(innerPanel));
		}

		window.AddControl(outerGrid.Build());
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(new Color(72, 61, 139), Color.Teal),
			GradientDirection.Horizontal);

		system.WindowStateService.AddWindow(window);

		var result = RunBenchmark(system, window,
			(w, i) => RotateGradient(w, i),
			"Deep Control Tree — 4x5x3 Nested (130x42, 60 controls)");

		PrintResult(result);
		PrintFrameBudget(result);
	}

	#endregion

	#region Profiling Scenarios

	[Fact]
	[Trait("Category", "Profile")]
	public void Profile_FullRedraw_PhaseBreakdown()
	{
		if (!IsBenchmarkRun) return;
		var (system, window) = CreateAlphaBlendingScene(110, 38);

		// Warmup
		for (int i = 0; i < WarmupFrames; i++)
		{
			RotateGradient(window, i);
			system.Render.UpdateDisplay();
		}

		// Collect per-phase timings over N frames
		double totalDom = 0, totalAnsi = 0, totalCompare = 0, totalOutput = 0;
		int frames = BenchmarkFrames;

		for (int i = 0; i < frames; i++)
		{
			RotateGradient(window, WarmupFrames + i);
			system.Render.UpdateDisplay();

			var m = system.RenderingDiagnostics?.LastMetrics;
			if (m != null)
			{
				totalDom += m.DomLayoutTimeMs;
				totalAnsi += m.AnsiGenerationTimeMs;
				totalCompare += m.BufferComparisonTimeMs;
				totalOutput += m.ConsoleOutputTimeMs;
			}
		}

		double total = totalDom + totalAnsi + totalCompare + totalOutput;

		_output.WriteLine("=== Phase Breakdown: Full Redraw (110x38) ===");
		_output.WriteLine($"  Frames: {frames}");
		_output.WriteLine("");
		PrintPhaseBar("DOM Layout", totalDom, total, frames);
		PrintPhaseBar("ANSI Gen  ", totalAnsi, total, frames);
		PrintPhaseBar("Buf Compare", totalCompare, total, frames);
		PrintPhaseBar("Output    ", totalOutput, total, frames);
		_output.WriteLine($"  {"Total",-14} {total / frames,8:F3} ms/frame");
	}

	[Fact]
	[Trait("Category", "Profile")]
	public void Profile_DirtyRatio_Curve()
	{
		if (!IsBenchmarkRun) return;
		_output.WriteLine("=== Dirty Ratio Curve (110x38) ===");
		_output.WriteLine($"  {"Dirty%",-8} {"ms/frame",-10} {"bytes/f",-10} {"cells/f",-10} {"ratio",-8}");
		_output.WriteLine($"  {new string('-', 46)}");

		// 0% — static
		{
			var (system, window) = CreateAlphaBlendingScene(110, 38);
			system.Render.UpdateDisplay(); // initial
			var r = RunBenchmark(system, window, (w, i) => { }, "static", quiet: true);
			_output.WriteLine($"  {"0%",-8} {r.AvgMsPerFrame,-10:F3} {r.AvgBytesPerFrame,-10} {r.AvgCellsRendered,-10} {"baseline",-8}");
		}

		// 5% — single control update
		{
			var (system, window, label) = CreatePartialUpdateScene(110, 38);
			system.Render.UpdateDisplay();
			var r = RunBenchmark(system, window, (w, i) => label.SetContent(new List<string> { $"X{i:D6}" }), "5%", quiet: true);
			_output.WriteLine($"  {"~5%",-8} {r.AvgMsPerFrame,-10:F3} {r.AvgBytesPerFrame,-10} {r.AvgCellsRendered,-10} {RatioLabel(r),-8}");
		}

		// 25% — change title + partial background
		{
			var (system, window) = CreateAlphaBlendingScene(110, 38);
			system.Render.UpdateDisplay();
			var r = RunBenchmark(system, window, (w, i) => { w.Title = $"Frame {i}"; w.IsDirty = true; }, "25%", quiet: true);
			_output.WriteLine($"  {"~25%",-8} {r.AvgMsPerFrame,-10:F3} {r.AvgBytesPerFrame,-10} {r.AvgCellsRendered,-10} {RatioLabel(r),-8}");
		}

		// 100% — full gradient change
		{
			var (system, window) = CreateAlphaBlendingScene(110, 38);
			var r = RunBenchmark(system, window, (w, i) => RotateGradient(w, i), "100%", quiet: true);
			_output.WriteLine($"  {"100%",-8} {r.AvgMsPerFrame,-10:F3} {r.AvgBytesPerFrame,-10} {r.AvgCellsRendered,-10} {"1.00x",-8}");
		}
	}

	[Fact]
	[Trait("Category", "Profile")]
	public void Profile_BufferSizes()
	{
		if (!IsBenchmarkRun) return;
		_output.WriteLine("=== Buffer Size Scaling (Full Redraw) ===");
		_output.WriteLine($"  {"Size",-14} {"Cells",-8} {"ms/frame",-10} {"bytes/f",-10} {"ms/1K cells",-12}");
		_output.WriteLine($"  {new string('-', 54)}");

		var sizes = new[] { (80, 25), (120, 40), (160, 50), (200, 50), (250, 60), (300, 80) };

		foreach (var (w, h) in sizes)
		{
			int winW = Math.Min(w - 10, 200);
			int winH = Math.Min(h - 5, 60);
			var (system, window) = CreateAlphaBlendingScene(winW, winH, screenW: w, screenH: h);
			var r = RunBenchmark(system, window, (win, i) => RotateGradient(win, i), $"{w}x{h}", quiet: true);
			int cells = winW * winH;
			double msPerKCells = r.AvgMsPerFrame / (cells / 1000.0);
			_output.WriteLine($"  {$"{w}x{h}",-14} {cells,-8} {r.AvgMsPerFrame,-10:F3} {r.AvgBytesPerFrame,-10} {msPerKCells,-12:F3}");
		}
	}

	#endregion

	#region Scene Builders

	private (ConsoleWindowSystem system, Window window) CreateAlphaBlendingScene(
		int winW, int winH, int? screenW = null, int? screenH = null)
	{
		int sw = screenW ?? winW + 20;
		int sh = screenH ?? winH + 10;
		var system = TestWindowSystemBuilder.CreateTestSystem(sw, sh);

		var window = new Window(system)
		{
			Left = 5, Top = 3,
			Width = winW, Height = winH,
			Title = "Alpha Blending Benchmark"
		};

		// Zone 1: Alpha ladder
		byte[] alphaLevels = { 0, 36, 73, 109, 146, 182, 219, 255 };
		var z1Grid = SharpConsoleUI.Builders.Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch);
		foreach (var alpha in alphaLevels)
		{
			var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithBackgroundColor(new Color(255, 140, 0, alpha))
				.WithBorderStyle(BorderStyle.None)
				.Build();
			panel.AddControl(new MarkupControl(new List<string> { $"a={alpha}" }));
			z1Grid = z1Grid.Column(col => col.Flex(1).Add(panel));
		}

		// Zone 2: Fade strip
		const int FadeChars = 60;
		var fadeSb = new System.Text.StringBuilder();
		for (int i = 0; i < FadeChars; i++)
		{
			byte a = (byte)(255 - (int)(i * 255.0 / (FadeChars - 1)));
			fadeSb.Append($"[#00DCDC{a:X2}]█[/]");
		}

		// Zone 3: Glass panels
		var z3Grid = SharpConsoleUI.Builders.Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill);
		foreach (byte a in new byte[] { 64, 128, 192, 255 })
		{
			var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
				.WithBackgroundColor(new Color(30, 144, 255, a))
				.WithBorderStyle(BorderStyle.Rounded)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Build();
			panel.AddControl(new MarkupControl(new List<string> { $"{a / 2.55:F0}%" }));
			z3Grid = z3Grid.Column(col => col.Flex(1).Add(panel));
		}

		// Zone 4: Blend preview
		var blendPreview = new MarkupControl(new List<string>
		{
			"src  [#FF6432FF]████[/]  rgba(255,100,50,128)",
			"dst  [#1E90FFFF]████[/]  rgba(30,144,255,255)",
			"out  [#9484A3FF]████[/]  Color.Blend(src, dst)"
		});

		// Assemble
		window.AddControl(new MarkupControl(new List<string> { "[bold]Alpha Blending Benchmark[/]" }));
		window.AddControl(z1Grid.Build());
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine(fadeSb.ToString()).Build());
		window.AddControl(z3Grid.Build());
		window.AddControl(blendPreview);

		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.Blue, Color.MediumPurple, Color.Orange1),
			GradientDirection.DiagonalDown);

		system.WindowStateService.AddWindow(window);
		return (system, window);
	}

	private (ConsoleWindowSystem system, Window window, MarkupControl label) CreatePartialUpdateScene(int winW, int winH)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(winW + 20, winH + 10);
		var window = new Window(system)
		{
			Left = 5, Top = 3,
			Width = winW, Height = winH,
			Title = "Partial Update Benchmark"
		};

		window.AddControl(new MarkupControl(new List<string> { "[bold]Partial Update Test[/]" }));

		// Static content — doesn't change
		for (int i = 0; i < 10; i++)
			window.AddControl(new MarkupControl(new List<string> { $"Static line {i}: this content never changes during the benchmark" }));

		// Dynamic label — changes every frame
		var label = new MarkupControl(new List<string> { "Counter: 000000" });
		window.AddControl(label);

		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(new Color(72, 61, 139), Color.Teal),
			GradientDirection.DiagonalDown);

		system.WindowStateService.AddWindow(window);
		return (system, window, label);
	}

	#endregion

	#region Benchmark Runner

	private BenchmarkResult RunBenchmark(
		ConsoleWindowSystem system, Window window,
		Action<Window, int> frameMutator, string label, bool quiet = false)
	{
		// Warmup
		for (int i = 0; i < WarmupFrames; i++)
		{
			frameMutator(window, i);
			system.Render.UpdateDisplay();
		}

		// Benchmark
		var sw = Stopwatch.StartNew();
		long totalBytes = 0, totalCells = 0, totalDirty = 0;
		double totalFrameTime = 0;

		for (int i = 0; i < BenchmarkFrames; i++)
		{
			frameMutator(window, WarmupFrames + i);
			system.Render.UpdateDisplay();

			var m = system.RenderingDiagnostics?.LastMetrics;
			if (m != null)
			{
				totalBytes += m.BytesWritten;
				totalCells += m.CellsActuallyRendered;
				totalDirty += m.DirtyCellsMarked;
				totalFrameTime += m.TotalFrameTimeMs;
			}
		}

		sw.Stop();

		return new BenchmarkResult
		{
			Label = label,
			Frames = BenchmarkFrames,
			WindowWidth = window.Width,
			WindowHeight = window.Height,
			TotalWallMs = sw.Elapsed.TotalMilliseconds,
			TotalBytes = totalBytes,
			TotalCellsRendered = totalCells,
			TotalDirtyCells = totalDirty,
			TotalFrameTimeMs = totalFrameTime,
			LastMetrics = system.RenderingDiagnostics?.LastMetrics
		};
	}

	private record BenchmarkResult
	{
		public string Label { get; init; } = "";
		public int Frames { get; init; }
		public int WindowWidth { get; init; }
		public int WindowHeight { get; init; }
		public double TotalWallMs { get; init; }
		public long TotalBytes { get; init; }
		public long TotalCellsRendered { get; init; }
		public long TotalDirtyCells { get; init; }
		public double TotalFrameTimeMs { get; init; }
		public RenderingMetrics? LastMetrics { get; init; }

		public int TotalCells => (WindowWidth - 2) * (WindowHeight - 2); // content area
		public double AvgMsPerFrame => TotalWallMs / Frames;
		public double TheoreticalFps => 1000.0 / AvgMsPerFrame;
		public long AvgBytesPerFrame => TotalBytes / Frames;
		public long AvgCellsRendered => TotalCellsRendered / Frames;
		public long AvgDirtyCells => TotalDirtyCells / Frames;
	}

	#endregion

	#region Output Formatting

	private void PrintResult(BenchmarkResult r)
	{
		int barWidth = 40;
		double fps = r.TheoreticalFps;
		int fpsBar = (int)Math.Min(barWidth, fps / 5); // 200fps = full bar

		_output.WriteLine("");
		_output.WriteLine($"  === {r.Label} ===");
		_output.WriteLine($"  Window:   {r.WindowWidth}x{r.WindowHeight} ({r.TotalCells} content cells)");
		_output.WriteLine($"  Frames:   {r.Frames} (+ {WarmupFrames} warmup)");
		_output.WriteLine($"  Wall:     {r.TotalWallMs:F1} ms total");
		_output.WriteLine("");
		_output.WriteLine($"  ms/frame: {r.AvgMsPerFrame:F3}");
		_output.WriteLine($"  FPS:      {fps:F0}  [{new string('#', fpsBar)}{new string('.', barWidth - fpsBar)}]");
		_output.WriteLine($"  bytes/f:  {r.AvgBytesPerFrame}");
		_output.WriteLine($"  cells/f:  {r.AvgCellsRendered} rendered / {r.AvgDirtyCells} dirty");

		if (r.AvgDirtyCells > 0)
		{
			double efficiency = (double)r.AvgCellsRendered / r.AvgDirtyCells;
			_output.WriteLine($"  efficien: {efficiency:P0}");
		}
		_output.WriteLine("");
	}

	private void PrintFrameBudget(BenchmarkResult r)
	{
		double ms = r.AvgMsPerFrame;
		const double budget60 = 16.67;
		const double budget30 = 33.33;

		string verdict;
		if (ms < budget60)
			verdict = $"WITHIN 60fps budget ({budget60:F1}ms)";
		else if (ms < budget30)
			verdict = $"WITHIN 30fps budget ({budget30:F1}ms) -- exceeds 60fps";
		else
			verdict = $"EXCEEDS 30fps budget ({budget30:F1}ms)";

		double headroom60 = ((budget60 - ms) / budget60) * 100;
		_output.WriteLine($"  Budget:   {verdict}");
		_output.WriteLine($"  Headroom: {headroom60:F0}% at 60fps ({budget60 - ms:F2}ms spare)");
		_output.WriteLine("");
	}

	private void PrintPhaseBar(string name, double totalMs, double grandTotal, int frames)
	{
		double avg = totalMs / frames;
		double pct = grandTotal > 0 ? totalMs / grandTotal * 100 : 0;
		int barLen = (int)(pct / 2); // 50 chars = 100%
		_output.WriteLine($"  {name,-14} {avg,8:F3} ms/frame  {pct,5:F1}%  [{new string('=', barLen)}{new string(' ', 50 - barLen)}]");
	}

	private void PrintScalingTable(List<(string label, BenchmarkResult result)> results)
	{
		_output.WriteLine("");
		_output.WriteLine("  === Buffer Size Scaling ===");
		_output.WriteLine($"  {"Size",-20} {"ms/frame",-10} {"FPS",-8} {"bytes/f",-10} {"cells/f",-10}");
		_output.WriteLine($"  {new string('-', 58)}");

		double? baseMs = null;
		foreach (var (label, r) in results)
		{
			baseMs ??= r.AvgMsPerFrame;
			double ratio = r.AvgMsPerFrame / baseMs.Value;
			_output.WriteLine($"  {label,-20} {r.AvgMsPerFrame,-10:F3} {r.TheoreticalFps,-8:F0} {r.AvgBytesPerFrame,-10} {r.AvgCellsRendered,-10} {ratio:F2}x");
		}
		_output.WriteLine("");
	}

	private string RatioLabel(BenchmarkResult r)
	{
		// Can't compute ratio without full redraw reference in this context
		return $"{r.AvgMsPerFrame:F1}ms";
	}

	#endregion

	#region Helpers

	private static void RotateGradient(Window window, int frame)
	{
		double phase = frame * 0.08;
		var c1 = HueToColor(phase);
		var c2 = HueToColor(phase + 1.0 / 3.0);
		var c3 = HueToColor(phase + 2.0 / 3.0);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(c1, c2, c3),
			GradientDirection.DiagonalDown);
	}

	private static Color HueToColor(double h)
	{
		h = ((h % 1.0) + 1.0) % 1.0;
		double s = h * 6.0;
		int i = (int)s;
		double f = s - i;
		double q = 1.0 - f;
		return i switch
		{
			0 => new Color(255, (byte)(f * 255), 0),
			1 => new Color((byte)(q * 255), 255, 0),
			2 => new Color(0, 255, (byte)(f * 255)),
			3 => new Color(0, (byte)(q * 255), 255),
			4 => new Color((byte)(f * 255), 0, 255),
			_ => new Color(255, 0, (byte)(q * 255)),
		};
	}

	#endregion
}
