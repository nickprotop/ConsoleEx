using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using CB = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Reproduction for the ServerHub WidgetExpansionDialog "half-empty scroll" bug. The real nesting is
/// ScrollablePanel(Fill) → borderless PanelControl(WordWrap=false, WithContent(N lines)) → content.
/// Symptom (observed live): scrolling to the bottom leaves ~14 EMPTY rows below the real content, i.e. the
/// panel reports a content height LARGER than what it paints, so the scroll extent has phantom rows.
///
/// This is the "real thing" nesting (SPC → PanelControl) — an SPC → MarkupControl test does NOT reproduce it.
/// </summary>
public class PanelInScrollPanelExtentTests
{
	private const int LineCount = 60;

	[Fact]
	public void PanelInScrollPanel_ContentHeightMatchesPaintedLines_NoPhantomRows()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "W", Left = 0, Top = 0, Width = 50, Height = 20 };

		// Distinct, short (non-wrapping) lines so painted-row-count == line-count exactly.
		var lines = Enumerable.Range(0, LineCount).Select(i => $"row {i:D2}").ToList();
		var text = string.Join("\n", lines);

		// Borderless, no-wrap PanelControl populated via WithContent — exactly WidgetExpansionDialog's widgetPanel.
		var panel = CB.Panel()
			.WithContent(text)
			.NoBorder()
			.WordWrap(false)
			.Build();

		var scroll = CB.ScrollablePanel()
			.WithVerticalScroll(ScrollMode.Scroll)
			.WithScrollbar(true)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(panel)
			.Build();

		window.AddControl(scroll);
		system.AddWindow(window);

		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		int viewport = scroll.ViewportHeight;
		int contentHeight = scroll.TotalContentHeight;

		// The panel has LineCount content rows (+ possibly a header row). The reported content height must NOT
		// exceed the real content by more than the panel chrome (a borderless headerless panel has ~0 header).
		// A large overshoot is the phantom-rows bug.
		Assert.True(viewport > 0, $"viewport should be > 0, was {viewport}");
		Assert.True(
			contentHeight <= LineCount + 2,
			$"PHANTOM ROWS: panel content height {contentHeight} should be ~{LineCount} (the painted rows), " +
			$"not larger. viewport={viewport}. Overshoot = {contentHeight - LineCount} phantom rows.");

		// Scroll to the bottom: the last content rows MUST become visible. The bug: when the SPC scrolls the
		// panel (arranges it at a negative Y), CollapsibleLayout clamps the body region to the WINDOW viewport
		// and positions it at that negative Y, so the visible body window lands off-screen → nothing paints
		// ("there are lines, but none visible" — the scrolled rows vanish).
		scroll.ScrollVerticalBy(500);
		var rendered = window.RenderAndGetVisibleContent(region);
		bool lastVisible = rendered.Any(l => l.Contains($"row {LineCount - 1:D2}"));
		Assert.True(lastVisible,
			$"BUG: after scrolling a Panel-in-ScrollablePanel to the bottom, the last row 'row {LineCount - 1:D2}' " +
			$"must be visible, but the body painted nothing. contentHeight={contentHeight}, viewport={viewport}, " +
			$"scrollOffset={scroll.VerticalScrollOffset}.");
	}
}
