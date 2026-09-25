// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for issue #80: with a horizontal scrollbar shown, the vertical scroll clamp
/// used the full viewport height while every caller targeted VisibleContentHeight (viewport minus
/// the scrollbar row), so the last content line could never be scrolled into view.
/// </summary>
public class ScrollablePanelHorizontalScrollbarClampTests
{
	private const int WindowHeight = 10;
	private const int LineCount = 30;

	/// <summary>
	/// Builds the real usage path from the issue: a Fill ScrollablePanel in a frameless window,
	/// holding non-wrapping lines wider than the window so BOTH scrollbars are shown.
	/// </summary>
	private static (ScrollablePanelControl panel, Window window) CreatePanel(bool wideLines)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = WindowHeight,
			BorderStyle = BorderStyle.Frameless
		};

		string tail = wideLines ? " " + new string('-', 60) : string.Empty;
		var markup = SharpConsoleUI.Builders.Controls.Markup()
			.AddLines(Enumerable.Range(1, LineCount).Select(i => $"line {i}{tail}").ToArray())
			.Build();
		markup.Wrap = false;

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithVerticalScroll(ScrollMode.Scroll)
			.WithHorizontalScroll(ScrollMode.Scroll)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(markup)
			.Build();

		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		return (panel, window);
	}

	private static List<string> RenderPlainRows(Window window) =>
		window.RenderAndGetVisibleContent()
			.Select(r => Regex.Replace(r, @"\x1b\[[0-9;?]*[ -/]*[@-~]", ""))
			.ToList();

	[Fact]
	public void ScrollToBottom_WithHorizontalScrollbar_ReachesLastLine()
	{
		var (panel, window) = CreatePanel(wideLines: true);
		Assert.True(panel.HasHorizontalScrollbar, "test premise: the horizontal scrollbar must be shown");

		panel.ScrollToBottom();

		// The scrollbar takes one row, leaving WindowHeight - 1 rows for content.
		Assert.Equal(LineCount - (WindowHeight - 1), panel.VerticalScrollOffset);
	}

	[Fact]
	public void ScrollToBottom_WithHorizontalScrollbar_LastLineIsPainted()
	{
		var (panel, window) = CreatePanel(wideLines: true);

		panel.ScrollToBottom();

		// The "real thing" assert: the end state survives a re-render and the last line is visible.
		var rows = RenderPlainRows(window);
		Assert.Contains($"line {LineCount}", rows[^2]);
	}

	[Fact]
	public void ScrollVerticalBy_WithHorizontalScrollbar_ReachesLastLine()
	{
		var (panel, _) = CreatePanel(wideLines: true);

		panel.ScrollVerticalBy(100);

		Assert.Equal(LineCount - (WindowHeight - 1), panel.VerticalScrollOffset);
	}

	[Fact]
	public void CanScrollDown_IsFalseOnlyWhenLastLineIsReachable()
	{
		var (panel, _) = CreatePanel(wideLines: true);

		panel.ScrollVerticalBy(LineCount - WindowHeight); // the old (short) clamp target

		// One row of content is still hidden under the scrollbar, so the panel must admit it can scroll.
		Assert.True(panel.CanScrollDown);

		panel.ScrollToBottom();
		Assert.False(panel.CanScrollDown);
	}

	[Fact]
	public void ScrollToBottom_WithoutHorizontalScrollbar_IsUnchanged()
	{
		var (panel, window) = CreatePanel(wideLines: false);
		Assert.False(panel.HasHorizontalScrollbar);

		panel.ScrollToBottom();

		Assert.Equal(LineCount - WindowHeight, panel.VerticalScrollOffset);
		var rows = RenderPlainRows(window);
		Assert.Contains($"line {LineCount}", rows[^1]);
	}
}
