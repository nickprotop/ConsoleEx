using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Scroll-extent regression coverage: a Top-aligned MarkupControl taller than the viewport, inside a
/// Fill ScrollablePanelControl that shares the window with fixed-height siblings, must drive the panel's
/// vertical scroll so its overflow is reachable (last line scrollable into view).
///
/// NOTE: these were written while chasing a (disproven) theory that this nesting caused ServerHub's
/// "half-empty scroll" bug. That bug's real cause was CollapsibleLayout clamping a scrolled Panel's body
/// off-screen — see <see cref="PanelInScrollPanelExtentTests"/> for the actual reproduction. These tests
/// assert correct behavior and are kept as plain scroll-extent regression coverage.
/// </summary>
public class MarkupFillScrollExtentTests
{
	private const int LineCount = 100;

	private static (ConsoleWindowSystem system, Window window, ScrollablePanelControl panel)
		BuildScene()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 20);

		// Small window so the panel viewport is far smaller than 100 lines.
		var window = new Window(system) { Title = "W", Left = 0, Top = 0, Width = 40, Height = 20 };

		// Build 100 distinct content lines that WRAP at the narrow panel width. Each line is far wider than
		// the ~36-col content area, so wrapping nearly doubles the displayed-row count. This is the case that
		// exposes a measure-width vs paint-width divergence: if the panel MEASURES the markup's wrapped extent
		// at the full width but PAINTS it at width-minus-scrollbar (or vice versa), the wrapped row counts
		// differ and the scroll extent won't match the painted rows → bottom content unreachable (half-empty).
		var lines = new List<string>();
		for (int i = 0; i < LineCount; i++)
			lines.Add($"line {i:D2} ssssssssss tttttttttt uuuuuuuuuu vvvvvvvvvv wwwwwwwwww xxxxxxxxxx");

		// Match ServerHub's actual config: the MARKUP is default Top alignment (NOT Fill — Fill means
		// "fill the viewport, don't scroll" by design); the PANEL is Fill. A Top markup taller than the
		// viewport must drive the panel's vertical scroll so its overflow is reachable.
		var markup = new MarkupControl(lines)
		{
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Stretch
		};

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithVerticalScroll(ScrollMode.Scroll)
			.WithScrollbar(true)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.AddControl(markup)
			.Build();

		// REAL-THING NESTING (mirrors ServerHub's ActionExecutionDialog): the Fill ScrollablePanel does NOT
		// own the whole window — it shares it with several fixed-height controls ABOVE (header + hints) and
		// BELOW (spacer + buttons). The panel's viewport is therefore "window height minus the siblings", which
		// is where a Fill-slot miscomputation hides (an isolated panel-fills-window test misses it).
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("[bold]Header[/]").Build());
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("hint line one").Build());
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("hint line two").Build());
		window.AddControl(panel); // the Fill panel in the middle
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("").Build()); // spacer
		window.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("[ Execute ]  [ Cancel ]").Build());
		system.AddWindow(window);
		return (system, window, panel);
	}

	[Fact]
	public void FillMarkupInPanel_ContentExceedsViewport_CanScrollDownToReachLastLines()
	{
		var (system, window, panel) = BuildScene();
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };

		window.RenderAndGetVisibleContent(region);

		int viewport = panel.ViewportHeight;
		int contentHeight = panel.TotalContentHeight;

		// Diagnostic numbers (surface them through the assertion messages).
		Assert.True(viewport > 0, $"viewport should be > 0, was {viewport}");

		// EXPECTED (correct behavior): content height ~= 100 lines, far larger than the viewport,
		// so the panel must be able to scroll down. BUG reproduces if this fails.
		Assert.True(
			contentHeight >= LineCount,
			$"BUG A: panel content height should be >= {LineCount} (full markup), but was {contentHeight} (viewport={viewport}). " +
			"Fill markup clamped its height to the viewport, so scroll extent is wrong.");

		Assert.True(
			panel.CanScrollDown,
			$"BUG A: panel.CanScrollDown should be true ({LineCount} lines in a {viewport}-row viewport), but was false. " +
			$"contentHeight={contentHeight}, viewport={viewport}.");
	}

	[Fact]
	public void FillMarkupInPanel_ScrollToBottom_LastLineIsRendered()
	{
		var (system, window, panel) = BuildScene();
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };

		window.RenderAndGetVisibleContent(region);

		// Try to scroll well past the end, then re-render.
		panel.ScrollVerticalBy(500);
		var rendered = window.RenderAndGetVisibleContent(region);

		int maxOffset = panel.TotalContentHeight - panel.ViewportHeight;
		bool lastLineVisible = rendered.Any(l => l.Contains($"line {LineCount - 1}"));

		Assert.True(
			lastLineVisible,
			$"BUG A: after scrolling to bottom, 'line {LineCount - 1}' should be rendered, but it was not. " +
			$"VerticalScrollOffset={panel.VerticalScrollOffset}, TotalContentHeight={panel.TotalContentHeight}, " +
			$"ViewportHeight={panel.ViewportHeight}, expected maxScrollOffset ~= {LineCount - panel.ViewportHeight} but is {maxOffset}.\n" +
			"Rendered lines:\n" + string.Join("\n", rendered));
	}
}
