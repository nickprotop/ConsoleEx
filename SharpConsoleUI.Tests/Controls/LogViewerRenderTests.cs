// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for <see cref="LogViewerControl"/> rendering through the real window pipeline.
/// </summary>
public class LogViewerRenderTests
{
	/// <summary>
	/// A LogViewerControl processes its queued log entries during the Measure pass
	/// (<c>MeasureDOM</c> -> <c>ProcessPendingEntries</c> -> inner panel <c>AddControl</c>). The panel's
	/// AddControl calls <c>ForceRebuildLayout()</c> on the parent window, which nulls the renderer's
	/// root layout node MID-PASS. The arrange pass that immediately follows must not dereference the
	/// now-null root and NRE. This reproduces the MultiDashboard "Log Stream" window crash on first render.
	/// </summary>
	[Fact]
	public void LogViewer_WithPendingEntries_RendersWithoutCrashing()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);

		// Boundary-stressing small window (mirrors the real LogStream window: 100x12).
		var window = new Window(system) { Title = "Log Stream", Width = 100, Height = 12 };
		system.AddWindow(window);

		// Queue real log entries so the control has pending work to flush during measure.
		system.LogService.MinimumLevel = LogLevel.Trace;
		for (int i = 0; i < 20; i++)
			system.LogService.Log(LogLevel.Information, $"log line {i}", "MultiDashboard");

		var logViewer = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			FilterLevel = LogLevel.Trace,
			AutoScroll = true
		};
		window.AddControl(logViewer);

		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };

		// First render: previously threw NullReferenceException inside the arrange pass.
		window.RenderAndGetVisibleContent(region);

		// Re-render: the rebuilt tree (the in-measure invalidation requested a relayout) must
		// survive and stay renderable.
		window.RenderAndGetVisibleContent(region);
	}

	/// <summary>
	/// Real-thing test: a LogViewerControl in a real window, fed by background-thread logging through
	/// the actual ILogService, must actually RENDER log rows and keep them after a re-render. This is
	/// the exact failure that shipped (empty Log Stream window) — an isolated component test missed it.
	/// </summary>
	[Fact]
	public void LogViewer_FedByLogService_RendersRowsThatSurviveReRender()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Log Stream", Width = 100, Height = 12 };
		system.AddWindow(window);

		system.LogService.MinimumLevel = LogLevel.Trace;

		var logViewer = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			FilterLevel = LogLevel.Trace,
			AutoScroll = true
		};
		window.AddControl(logViewer);

		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region); // attach + first frame

		// Log AFTER attach, mimicking the async-window-thread path, then pump the marshalled work.
		for (int i = 0; i < 8; i++)
			system.LogService.Log(LogLevel.Information, $"UNIQUELINE{i}", "MultiDashboard");
		system.DrainUiThreadQueueForTests();

		// Assert on the NEWEST row: this window (100x12, the real Log Stream size) shows ~6 data rows,
		// and AutoScroll (tail-follow) correctly sticks to the newest entry, so the oldest rows scroll
		// off-screen — asserting the last-logged line is what proves both "rows actually rendered" AND
		// "auto-scroll tailed to the newest" (the empty-window bug rendered NO rows at all).
		var lines1 = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines1, l => l.Contains("UNIQUELINE7"));

		// Survives a re-render (the shipped bug lost content on the next frame).
		var lines2 = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines2, l => l.Contains("UNIQUELINE7"));
	}

	/// <summary>
	/// Tail-follow at the bottom: as 100 log entries are added while the viewer sits at the bottom,
	/// it keeps the newest entry visible. Renamed from the old ...UnlessScrolledUp, which overpromised:
	/// its body never scrolled up. The scrolled-up cases are the two tests below.
	/// </summary>
	[Fact]
	public void LogViewer_TailFollow_KeepsNewestVisible_WhenAtBottom()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 12 };
		system.AddWindow(window);
		system.LogService.MinimumLevel = LogLevel.Trace;

		var lv = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = true
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		for (int i = 0; i < 100; i++)
			system.LogService.Log(LogLevel.Information, $"NEWEST{i}", "Cat");
		system.DrainUiThreadQueueForTests();
		var lines = window.RenderAndGetVisibleContent(region);

		// Newest line should be visible under tail-follow.
		Assert.Contains(lines, l => l.Contains("NEWEST99"));
	}

	/// <summary>
	/// With the viewer scrolled up, a newly-logged line must NOT yank the viewport back to the
	/// bottom (follow is paused). The one-row StickyBottomThreshold guard in ApplyTailFollow makes this
	/// hold; before the guard the control pinned to the bottom on every incoming line.
	/// </summary>
	[Fact]
	public void LogViewer_TailFollow_PausesWhenScrolledUp()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 12 };
		system.AddWindow(window);

		// A DEDICATED log service so the viewer shows only these lines, not the window system's own
		// framework chatter (this is exactly why production feeds LogViewerControl a dedicated LogService).
		var logSvc = new LogService { MinimumLevel = LogLevel.Trace };
		var lv = new LogViewerControl(logSvc)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = true
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		// Seed well over one screen (stay under the LogService MaxBufferSize of 1000).
		for (int i = 0; i < 60; i++)
			logSvc.Log(LogLevel.Information, $"SEED{i}", "Cat");
		system.DrainUiThreadQueueForTests();
		var atBottom = window.RenderAndGetVisibleContent(region);
		Assert.Contains(atBottom, l => l.Contains("SEED59")); // tail visible at the bottom

		// Scroll to the top (ScrollOffset clamps at 0): the tail must leave the viewport.
		for (int i = 0; i < 80; i++) lv.ProcessMouseEvent(Wheel(MouseFlags.WheeledUp));
		var afterUp = window.RenderAndGetVisibleContent(region);
		Assert.Contains(afterUp, l => l.Contains("SEED0"));        // oldest visible: at the top
		Assert.DoesNotContain(afterUp, l => l.Contains("SEED59")); // moved away from the bottom

		// A new line arrives while scrolled up: follow paused, viewport must not jump.
		logSvc.Log(LogLevel.Information, "AFTERSCROLL", "Cat");
		system.DrainUiThreadQueueForTests();
		var after = window.RenderAndGetVisibleContent(region);
		Assert.DoesNotContain(after, l => l.Contains("AFTERSCROLL")); // not yanked to the bottom
		Assert.Contains(after, l => l.Contains("SEED0"));            // viewport held at the top
	}

	/// <summary>
	/// After scrolling back to the bottom, tail-follow resumes: the next logged line is visible.
	/// </summary>
	[Fact]
	public void LogViewer_TailFollow_ResumesAtBottom()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 12 };
		system.AddWindow(window);

		var logSvc = new LogService { MinimumLevel = LogLevel.Trace };
		var lv = new LogViewerControl(logSvc)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = true
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		for (int i = 0; i < 60; i++)
			logSvc.Log(LogLevel.Information, $"SEED{i}", "Cat");
		system.DrainUiThreadQueueForTests();
		window.RenderAndGetVisibleContent(region);

		// Scroll up, then back down to the bottom.
		for (int i = 0; i < 80; i++) lv.ProcessMouseEvent(Wheel(MouseFlags.WheeledUp));
		window.RenderAndGetVisibleContent(region);
		for (int i = 0; i < 80; i++) lv.ProcessMouseEvent(Wheel(MouseFlags.WheeledDown));
		window.RenderAndGetVisibleContent(region);

		// Follow resumed at the bottom: the next line is visible.
		logSvc.Log(LogLevel.Information, "RESUMED", "Cat");
		system.DrainUiThreadQueueForTests();
		var after = window.RenderAndGetVisibleContent(region);
		Assert.Contains(after, l => l.Contains("RESUMED"));
	}

	private static MouseEventArgs Wheel(MouseFlags flag) =>
		new(new List<MouseFlags> { flag }, new Point(1, 1), new Point(1, 1), new Point(1, 1));

	/// <summary>
	/// Large buffer virtualization test: adding 50,000 entries should not crash and the tail entry
	/// should be visible. This validates that the LogViewerControl does not create per-entry UI
	/// controls but instead uses virtualized rendering via TableControl.
	/// </summary>
	[Fact]
	public void LogViewer_LargeBuffer_DoesNotCrashAndShowsTail()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 12 };
		system.AddWindow(window);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.MaxBufferSize = 50000;

		var lv = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = true
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		for (int i = 0; i < 50000; i++)
			system.LogService.Log(LogLevel.Information, $"L{i}", "Cat");
		system.DrainUiThreadQueueForTests();

		var lines = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines, l => l.Contains("L49999"));
	}

	[Fact]
	public void LogViewer_LevelDropdown_SetsLogServiceMinimumLevel()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 14 };
		system.AddWindow(window);
		system.LogService.MinimumLevel = LogLevel.Warning;

		var lv = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		lv.SetCaptureLevel(LogLevel.Trace); // dropdown-equivalent programmatic entry (Task 6 API)

		Assert.Equal(LogLevel.Trace, system.LogService.MinimumLevel);
	}

	[Fact]
	public void LogViewer_Pause_FreezesTailFollow()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 14 };
		system.AddWindow(window);
		system.LogService.MinimumLevel = LogLevel.Trace;

		var lv = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			AutoScroll = true
		};
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		// Seed, follow to bottom.
		for (int i = 0; i < 30; i++) system.LogService.Log(LogLevel.Information, $"A{i}", "Cat");
		system.DrainUiThreadQueueForTests();
		window.RenderAndGetVisibleContent(region);

		lv.IsPaused = true;
		for (int i = 0; i < 30; i++) system.LogService.Log(LogLevel.Information, $"B{i}", "Cat");
		system.DrainUiThreadQueueForTests();
		var linesPaused = window.RenderAndGetVisibleContent(region);

		// Paused: the newest B-lines should NOT have forced the view to jump.
		Assert.DoesNotContain(linesPaused, l => l.Contains("B29"));
	}

	[Fact]
	public void LogViewer_SelectingRow_ShowsFullMessageInDetail()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		// Taller window so the expanded detail pane (~4 rows) has room to wrap the full message.
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 24 };
		system.AddWindow(window);
		system.LogService.MinimumLevel = LogLevel.Trace;

		var lv = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};
		// Restrict the VIEW to Error level so the single Error we log below is display row 0 —
		// otherwise the many DEBUG/TRACE framework entries (window add, renderer relayout) fill
		// the earlier rows and SelectEntry(0) would target the wrong entry.
		lv.FilterLevel = LogLevel.Error;
		window.AddControl(lv);
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		// The unique tail marker sits ~155 chars into the message — FAR past the table
		// Message-column truncation width (~67 cols with fade). It therefore CANNOT appear
		// in the truncated single-line table row; it only appears when the detail pane
		// wraps the full message across multiple lines.
		var msg = "HEAD_" + new string('x', 150) + "_PANE_ONLY_TAIL";
		system.LogService.Log(LogLevel.Error, msg, "Cat");
		system.DrainUiThreadQueueForTests();

		// NEGATIVE CONTROL: with the detail pane collapsed, the table row truncates the
		// deep tail marker away — it must NOT be visible anywhere.
		var linesBefore = window.RenderAndGetVisibleContent(region);
		Assert.DoesNotContain(linesBefore, l => l.Contains("_PANE_ONLY_TAIL"));

		lv.SelectEntry(0); // programmatic selection (Task 7 API) — expands + fills detail pane
		var linesAfter = window.RenderAndGetVisibleContent(region);

		// OBSERVABLE END STATE of the detail-pane feature: the wrapped full message now
		// reveals the deep tail marker that the truncated table row could never show.
		Assert.Contains(linesAfter, l => l.Contains("_PANE_ONLY_TAIL"));
	}
}
