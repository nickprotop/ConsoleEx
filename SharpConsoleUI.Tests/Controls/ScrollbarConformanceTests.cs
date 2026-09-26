// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Reflection;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// One behaviour suite every scrollbar-bearing control must satisfy, so the shared engine cannot
/// drift apart again per control (issue #85).
/// </summary>
public class ScrollbarConformanceTests
{
	private const int ViewportRows = 10;
	private const int ItemCount = 100;
	private const int WindowWidth = 40;

	public static TheoryData<string> Controls => new() { "list", "tree", "html", "scrollablepanel", "table" };

	/// <summary>
	/// Builds the control with ~100 items in a ~10-row window, rendered, so a scrollbar is showing.
	/// Returns the control, its window/system, and a reader for its current scroll offset.
	/// </summary>
	private static (IWindowControl control, Window window, ConsoleWindowSystem system, HeadlessConsoleDriver driver, Func<int> offset) Build(string kind, SharpConsoleUI.Themes.ITheme? theme = null)
	{
		var driver = new HeadlessConsoleDriver(WindowWidth, ViewportRows + 4);
		var system = theme == null
			? new ConsoleWindowSystem(
				driver,
				new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false))
			: new ConsoleWindowSystem(
				driver,
				theme,
				new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = WindowWidth,
			Height = ViewportRows,
			BorderStyle = BorderStyle.Frameless
		};

		IWindowControl control;
		Func<int> offset;

		switch (kind)
		{
			case "list":
				{
					var list = SharpConsoleUI.Builders.Controls.List()
						.AddItems(Enumerable.Range(1, ItemCount).Select(i => $"item {i}").ToArray())
						.MaxVisibleItems(ViewportRows)
						.WithScrollbarVisibility(ScrollbarVisibility.Always)
						.Build();
					control = list;
					offset = () => GetPrivateInt(list, "_scrollOffset");
					break;
				}
			case "tree":
				{
					var builder = SharpConsoleUI.Builders.Controls.Tree()
						.WithMaxVisibleItems(ViewportRows)
						.WithScrollbarVisibility(ScrollbarVisibility.Always);
					for (int i = 1; i <= ItemCount; i++)
						builder.AddRootNode($"node {i}");
					var tree = builder.Build();
					control = tree;
					offset = () => GetPrivateInt(tree, "_scrollOffset");
					break;
				}
			case "html":
				{
					string body = string.Concat(Enumerable.Range(1, ItemCount).Select(i => $"<p>line {i}</p>"));
					var html = HtmlBuilder.Create()
						.WithContent(body)
						.WithHeight(ViewportRows)
						.WithScrollbarVisibility(ScrollbarVisibility.Always)
						.Build();
					control = html;
					offset = () => html.ScrollOffset;
					break;
				}
			case "scrollablepanel":
				{
					var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
						.WithHeight(ViewportRows)
						.WithScrollbar(true)
						.Build();
					for (int i = 1; i <= ItemCount; i++)
						panel.AddControl(SharpConsoleUI.Builders.Controls.Markup($"line {i}").Build());
					control = panel;
					offset = () => panel.VerticalScrollOffset;
					break;
				}
			case "table":
				{
					var builder = SharpConsoleUI.Builders.Controls.Table()
						.NoBorder()
						.HideHeader()
						.WithHeight(ViewportRows)
						.WithColumns("Col")
						.WithVerticalScrollbar(ScrollbarVisibility.Always);
					for (int i = 1; i <= ItemCount; i++)
						builder.AddRow($"row {i}");
					var table = builder.Build();
					control = table;
					offset = () => table.ScrollOffset;
					break;
				}
			default:
				throw new ArgumentException($"Unknown control kind: {kind}", nameof(kind));
		}

		window.AddControl(control);
		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		Assert.True(
			SpinWait.SpinUntil(() => ((BaseControl)control).ActualHeight > 0
				&& system.WindowStateService.ActiveWindow == window, 5000),
			"the window did not become active in time");

		return (control, window, system, driver, offset);
	}

	private static int GetPrivateInt(object instance, string fieldName)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.NotNull(field);
		return (int)field!.GetValue(instance)!;
	}

	/// <summary>The X column the scrollbar paints on, given no margin/title: the control's last column.</summary>
	private static int ScrollbarX(IWindowControl control) => ((BaseControl)control).ActualWidth - 1;

	/// <summary>The scrollbar's screen Y range: [top, top + height), given no margin/title.</summary>
	private static (int top, int height) ScrollbarYRange(IWindowControl control)
	{
		var c = (BaseControl)control;
		return (0, c.ActualHeight);
	}

	private static (int trackTop, int trackHeight, int thumbY, int thumbHeight) Geometry(IWindowControl control)
	{
		var (_, height) = ScrollbarYRange(control);
		int totalItems = control switch
		{
			HtmlControl h => h.ContentHeight,
			ScrollablePanelControl p => p.TotalContentHeight,
			_ => ItemCount
		};
		return ScrollbarHelper.GetVerticalGeometry(height, totalItems, ViewportRows, GetVisibleOffset(control));
	}

	private static int GetVisibleOffset(IWindowControl control) => control switch
	{
		ListControl l => GetPrivateInt(l, "_scrollOffset"),
		TreeControl t => GetPrivateInt(t, "_scrollOffset"),
		HtmlControl h => h.ScrollOffset,
		ScrollablePanelControl p => p.VerticalScrollOffset,
		TableControl tc => tc.ScrollOffset,
		_ => 0
	};

	/// <summary>
	/// The maximum scroll offset for the built control. List/Tree/Table scroll by item (ItemCount
	/// total); Html scrolls by rendered line (its ContentHeight, which is more than ItemCount since
	/// each paragraph renders with blank-line spacing); ScrollablePanel scrolls by row, one per markup
	/// child.
	/// </summary>
	private static int MaxOffset(IWindowControl control) => control switch
	{
		HtmlControl h => Math.Max(0, h.ContentHeight - ViewportRows),
		ScrollablePanelControl p => Math.Max(0, p.TotalContentHeight - ViewportRows),
		_ => ItemCount - ViewportRows
	};

	[Theory]
	[MemberData(nameof(Controls))]
	public void ClickingTheDownArrow_StepsOneItem(string kind)
	{
		var (control, window, system, driver, offset) = Build(kind);
		try
		{
			int before = offset();
			int x = ScrollbarX(control);
			var (top, height) = ScrollbarYRange(control);
			int downArrowY = top + height - 1;

			driver.SimulateMouseEvent([MouseFlags.Button1Clicked], new Point(x, downArrowY));
			SpinWait.SpinUntil(() => offset() != before, 2000);

			Assert.Equal(before + 1, offset());
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Theory]
	[MemberData(nameof(Controls))]
	public void ClickingTheTrackBelow_PagesOneViewport(string kind)
	{
		var (control, window, system, driver, offset) = Build(kind);
		try
		{
			int before = offset();
			int x = ScrollbarX(control);
			var (trackTop, trackHeight, thumbY, thumbHeight) = Geometry(control);
			int belowThumbY = trackTop + thumbY + thumbHeight + 1;
			Assert.True(belowThumbY < trackTop + trackHeight - 1, "test premise: there must be track below the thumb, before the down arrow");

			driver.SimulateMouseEvent([MouseFlags.Button1Clicked], new Point(x, belowThumbY));
			SpinWait.SpinUntil(() => offset() != before, 2000);

			Assert.Equal(before + ViewportRows, offset());
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Theory]
	[MemberData(nameof(Controls))]
	public void DraggingTheThumbToTheBottom_ReachesTheLastItem(string kind)
	{
		var (control, window, system, driver, offset) = Build(kind);
		try
		{
			int x = ScrollbarX(control);
			var (trackTop, trackHeight, thumbY, thumbHeight) = Geometry(control);
			int pressY = trackTop + thumbY + (thumbHeight / 2);

			driver.SimulateMouseEvent([MouseFlags.Button1Pressed], new Point(x, pressY));
			// Drag far past the bottom of the track - the thumb must clamp to the maximum offset.
			int dragY = trackTop + trackHeight + 50;
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed, MouseFlags.Button1Dragged], new Point(x, dragY));
			driver.SimulateMouseEvent([MouseFlags.Button1Released], new Point(x, dragY));

			int maxOffset = MaxOffset(control);
			SpinWait.SpinUntil(() => offset() == maxOffset, 2000);
			Assert.Equal(maxOffset, offset());

			// Survives a re-render.
			system.EnqueueOnUIThread(() => window.RenderAndGetVisibleContent());
			Thread.Sleep(50);
			Assert.Equal(maxOffset, offset());
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Theory]
	[MemberData(nameof(Controls))]
	public void DragDoesNotLeakIntoContent_WhenThePointerLeavesTheBar(string kind)
	{
		var (control, window, system, driver, offset) = Build(kind);
		try
		{
			int x = ScrollbarX(control);
			var (trackTop, trackHeight, thumbY, thumbHeight) = Geometry(control);
			int pressY = trackTop + thumbY + (thumbHeight / 2);

			int contentClicks = 0;
			SubscribeMouseClick(control, () => contentClicks++);

			driver.SimulateMouseEvent([MouseFlags.Button1Pressed], new Point(x, pressY));
			// Move off the bar into the content column, still "dragging" per SGR's resent press.
			int contentX = 2;
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed, MouseFlags.Button1Dragged], new Point(contentX, pressY + 1));
			driver.SimulateMouseEvent([MouseFlags.Button1Released, MouseFlags.Button1Clicked], new Point(contentX, pressY + 1));
			Thread.Sleep(50);

			// The gesture stayed captured by the scrollbar (region hit-tested only on the fresh press),
			// so the release/click landing over content must NOT fire as a content click.
			Assert.Equal(0, contentClicks);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Theory]
	[MemberData(nameof(Controls))]
	public void ClickingTheThumbAndReleasing_LeavesNoStaleCapture(string kind)
	{
		var (control, window, system, driver, offset) = Build(kind);
		try
		{
			int x = ScrollbarX(control);
			var (trackTop, _, thumbY, thumbHeight) = Geometry(control);
			int pressY = trackTop + thumbY + (thumbHeight / 2);

			// Press and release on the thumb (a click, no drag) - MouseGestureCapture releases on
			// Released OR Clicked, unlike the old latch which released only on Released.
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed], new Point(x, pressY));
			driver.SimulateMouseEvent([MouseFlags.Button1Released, MouseFlags.Button1Clicked], new Point(x, pressY));
			Thread.Sleep(50);

			// Now click content: it must respond normally (fire MouseClick), proving no stale
			// capture is still swallowing input as "scrollbar".
			int contentX = 2;
			int contentY = 1;
			int contentClicks = 0;
			SubscribeMouseClick(control, () => contentClicks++);
			driver.SimulateMouseEvent([MouseFlags.Button1Clicked], new Point(contentX, contentY));
			Thread.Sleep(50);

			Assert.Equal(1, contentClicks);
		}
		finally
		{
			system.Shutdown();
		}
	}

	/// <summary>Subscribes to whichever control's MouseClick event fires on a content click (not a scrollbar click).</summary>
	private static void SubscribeMouseClick(IWindowControl control, Action onClick)
	{
		switch (control)
		{
			case ListControl l:
				l.MouseClick += (_, _) => onClick();
				break;
			case TreeControl t:
				t.MouseClick += (_, _) => onClick();
				break;
			case HtmlControl h:
				h.MouseClick += (_, _) => onClick();
				break;
			case ScrollablePanelControl p:
				// SPC forwards content clicks to the child under the cursor rather than firing its own
				// MouseClick (which the control never raises) - subscribe to every child (one row each,
				// so any content click in the test viewport lands on one of the first few).
				foreach (var child in p.GetChildren())
					if (child is MarkupControl m)
						m.MouseClick += (_, _) => onClick();
				break;
			case TableControl tc:
				tc.MouseClick += (_, _) => onClick();
				break;
		}
	}

	// A theme that inherits ModernGrayTheme's abstract implementations but overrides only the
	// general scrollbar colours via explicit interface members, so a control reading them proves it
	// consults the ACTIVE THEME rather than a hardcoded literal.
	private sealed class CustomScrollbarTheme : ModernGrayTheme, SharpConsoleUI.Themes.ITheme
	{
		Color? SharpConsoleUI.Themes.ITheme.ScrollbarThumbColor => Color.Red;
		Color? SharpConsoleUI.Themes.ITheme.ScrollbarThumbUnfocusedColor => Color.Green;
		Color? SharpConsoleUI.Themes.ITheme.ScrollbarTrackColor => Color.Blue;
		Color? SharpConsoleUI.Themes.ITheme.ScrollbarTrackUnfocusedColor => Color.Yellow;

		// Table reads its OWN theme keys (no unfocused variant). Nulling them out here forces Table
		// onto the shared resolver's focus-aware FALLBACK tier, which IS distinct per focus state -
		// unlike ModernGrayTheme's TableScrollbarThumbColor default, which is the same Cyan1
		// regardless of focus and would make a focused-vs-unfocused comparison meaningless.
		Color? SharpConsoleUI.Themes.ITheme.TableScrollbarThumbColor => null;
		Color? SharpConsoleUI.Themes.ITheme.TableScrollbarTrackColor => null;
	}

	/// <summary>Moves focus off the built control onto a dummy button, so its own scrollbar palette
	/// resolves as unfocused.</summary>
	private static void BlurByFocusingADummyButton(Window window)
	{
		var dummy = new ButtonControl { Text = "dummy" };

		void DoFocus()
		{
			window.AddControl(dummy);
			window.FocusManager.SetFocus(dummy, FocusReason.Programmatic);
		}

		var system = window.GetConsoleWindowSystem;
		if (system != null)
		{
			system.EnqueueOnUIThread(DoFocus);
			SpinWait.SpinUntil(() => window.FocusManager.FocusedControl == dummy, 2000);
		}
		else
		{
			DoFocus();
		}
	}

	/// <summary>
	/// Tree used to paint a hardcoded Cyan1/Grey scrollbar regardless of the active theme; the
	/// retrofit onto <see cref="ScrollbarPaletteResolver"/> made it theme-aware like every other
	/// scrollbar-bearing control (accepted change, task 6).
	/// </summary>
	[Fact]
	public void Tree_ScrollbarColor_HonoursTheActiveTheme()
	{
		var (control, _, system, _, _) = Build("tree", new CustomScrollbarTheme());
		try
		{
			var tree = (TreeControl)control;
			var palette = tree.ResolveScrollbarPalette();

			Assert.Equal(Color.Red, palette.Thumb);
			Assert.Equal(Color.Blue, palette.Track);
		}
		finally
		{
			system.Shutdown();
		}
	}

	/// <summary>
	/// List and Table must resolve a visibly different unfocused colour from their focused one, once
	/// a theme supplies both — proving the shared resolver's focus-aware theme tier is actually wired
	/// up per control, not just implemented once in isolation (accepted change, tasks 6 and 8).
	/// </summary>
	[Fact]
	public void List_ScrollbarColor_UsesADistinctUnfocusedColor()
	{
		var (control, window, system, _, _) = Build("list", new CustomScrollbarTheme());
		try
		{
			var list = (ListControl)control;
			var focusedThumb = list.ResolveScrollbarThumbColor();

			BlurByFocusingADummyButton(window);
			var unfocusedThumb = list.ResolveScrollbarThumbColor();

			Assert.Equal(Color.Red, focusedThumb);
			Assert.Equal(Color.Green, unfocusedThumb);
			Assert.NotEqual(focusedThumb, unfocusedThumb);
		}
		finally
		{
			system.Shutdown();
		}
	}

	/// <summary>
	/// Table's own theme keys (<c>TableScrollbarThumbColor</c>/<c>TableScrollbarTrackColor</c>) have
	/// no unfocused variant, so — per task 8's accepted change — Table now falls back to the shared
	/// resolver's focus-aware FALLBACK tier (not the theme tier) once no table-specific theme colour
	/// is set, giving it a distinct unfocused colour where it previously repainted the same one
	/// regardless of focus.
	/// </summary>
	[Fact]
	public void Table_ScrollbarColor_UsesADistinctUnfocusedColor()
	{
		var (control, window, system, _, _) = Build("table", new CustomScrollbarTheme());
		try
		{
			var table = (TableControl)control;
			var focusedPalette = InvokeTablePalette(table);

			BlurByFocusingADummyButton(window);
			var unfocusedPalette = InvokeTablePalette(table);

			Assert.NotEqual(focusedPalette.Thumb, unfocusedPalette.Thumb);
		}
		finally
		{
			system.Shutdown();
		}
	}

	private static ScrollbarPalette InvokeTablePalette(TableControl table)
	{
		var method = typeof(TableControl).GetMethod("ResolveScrollbarPalette", BindingFlags.NonPublic | BindingFlags.Instance)!;
		return (ScrollbarPalette)method.Invoke(table, new object[] { Color.Black })!;
	}

	/// <summary>
	/// A disabled scrollbar-bearing control has no dedicated "disabled" theme colour, so the shared
	/// resolver dims it to its unfocused colours regardless of the focus flag (task 5's ruling).
	/// </summary>
	[Theory]
	[InlineData("list")]
	[InlineData("tree")]
	[InlineData("table")]
	public void DisabledControl_DimsToTheUnfocusedColor(string kind)
	{
		var (control, window, system, _, _) = Build(kind, new CustomScrollbarTheme());
		try
		{
			BlurByFocusingADummyButton(window);
			var unfocusedPalette = GetPalette(control, kind);

			SetEnabled(control, kind, false);
			var disabledPalette = GetPalette(control, kind);

			Assert.Equal(unfocusedPalette.Thumb, disabledPalette.Thumb);
			Assert.Equal(unfocusedPalette.Track, disabledPalette.Track);
		}
		finally
		{
			system.Shutdown();
		}
	}

	private static void SetEnabled(IWindowControl control, string kind, bool enabled)
	{
		switch (control)
		{
			case ListControl l: l.IsEnabled = enabled; break;
			case TreeControl t: t.IsEnabled = enabled; break;
			case TableControl tc: tc.IsEnabled = enabled; break;
			case ScrollablePanelControl p: p.IsEnabled = enabled; break;
			case HtmlControl h: h.IsEnabled = enabled; break;
		}
	}

	private static ScrollbarPalette GetPalette(IWindowControl control, string kind) => kind switch
	{
		"list" => new ScrollbarPalette(((ListControl)control).ResolveScrollbarThumbColor(), ((ListControl)control).ResolveScrollbarTrackColor(), Color.Black),
		"tree" => ((TreeControl)control).ResolveScrollbarPalette(),
		"table" => InvokeTablePalette((TableControl)control),
		_ => throw new ArgumentException($"Unsupported kind for palette comparison: {kind}", nameof(kind))
	};
}

/// <summary>
/// Table-specific scrollbar behaviour that the shared conformance theory above does not cover:
/// the four things Table keeps that the engine does not own (issue tracked alongside #85's retrofit).
/// </summary>
public class TableScrollbarTests
{
	/// <summary>
	/// Renders into a borderless window sized exactly to <paramref name="width"/>/<paramref name="height"/>,
	/// so a rendered line's column N is the table's own column N (unlike
	/// <see cref="ContainerTestHelpers.RenderToLines"/>, whose default bordered window eats a cell on
	/// every edge).
	/// </summary>
	private static List<string> RenderExact(TableControl table, int width, int height)
	{
		var system = SharpConsoleUI.Tests.Infrastructure.TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Width = width, Height = height, BorderStyle = BorderStyle.Frameless };
		window.AddControl(table);
		var output = window.RenderAndGetVisibleContent();
		return ContainerTestHelpers.StripAnsiCodes(output).Split('\n').ToList();
	}

	[Fact]
	public void MinScrollbarThumbSize_StillGrowsTheThumb()
	{
		// A table with many rows and MinScrollbarThumbSize = 4 must draw a thumb at least 4 tall.
		var table = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder()
			.HideHeader()
			.WithColumns("Col")
			.WithHeight(10)
			.WithVerticalScrollbar(ScrollbarVisibility.Always)
			.WithMinScrollbarThumbSize(4)
			.Build();
		for (int i = 1; i <= 200; i++)
			table.AddRow($"row {i}");

		RenderExact(table, 20, 10);

		var (_, _, _, thumbHeight) = table.GetVerticalScrollbarGeometry(table.GetVisibleRowCount());
		Assert.True(thumbHeight >= 4, $"expected thumb height >= 4, got {thumbHeight}");
	}

	[Fact]
	public void MinScrollbarThumbSize_DoesNotAffectTheHorizontalBar()
	{
		// Preserved asymmetry: the horizontal thumb still uses a minimum of 1, even with a large
		// MinScrollbarThumbSize set (that property is documented as vertical-only). Compare the
		// geometry with and without the large minimum, on the same content/viewport width: if the
		// property leaked into the horizontal axis, the second thumb would be wider than the first.
		const int contentAreaWidth = 10;
		const int totalColumnsWidth = 500; // Far in excess of the viewport, so the thumb starts tiny.

		var plain = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder().HideHeader().WithColumns("Col").Build();
		var (_, _, _, plainThumbWidth) = plain.GetHorizontalScrollbarGeometry(contentAreaWidth, totalColumnsWidth);

		var withLargeMin = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder().HideHeader().WithColumns("Col")
			.WithMinScrollbarThumbSize(10)
			.Build();
		var (_, _, _, thumbWidthWithLargeMin) = withLargeMin.GetHorizontalScrollbarGeometry(contentAreaWidth, totalColumnsWidth);

		Assert.Equal(plainThumbWidth, thumbWidthWithLargeMin);
		Assert.True(thumbWidthWithLargeMin < 10, $"expected the horizontal thumb to ignore MinScrollbarThumbSize (stay < 10), got {thumbWidthWithLargeMin}");
	}

	[Fact]
	public void ScrollbarVisibilityAlways_DrawsTheBarWithoutOverflow()
	{
		// Two rows in a ten-row table: the bar is still painted because ScrollbarVisibility.Always
		// bypasses the "content overflows the viewport" check.
		const int width = 20;
		var table = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder()
			.HideHeader()
			.WithColumns("Col")
			.WithWidth(width)
			.WithHeight(10)
			.WithVerticalScrollbar(ScrollbarVisibility.Always)
			.Build();
		table.AddRow("row 1");
		table.AddRow("row 2");

		var lines = RenderExact(table, width, 10);

		Assert.True(table.ShouldShowVerticalScrollbar());
		// The scrollbar paints in the table's last column (width - 1): track/thumb/arrow, never blank.
		Assert.All(lines.Where(l => l.Length >= width), line => Assert.NotEqual(' ', line[width - 1]));
	}

	[Fact]
	public void WithBothBars_BlanksTheCornerCell()
	{
		// The intersection where the vertical and horizontal bars meet is a space, not a stray track
		// glyph from either bar overdrawing the other.
		const int width = 20;
		const int height = 10;
		var table = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder()
			.HideHeader()
			.WithColumns(Enumerable.Range(1, 20).Select(i => $"Col{i}").ToArray())
			.WithWidth(width)
			.WithHeight(height)
			.WithVerticalScrollbar(ScrollbarVisibility.Always)
			.WithHorizontalScrollbar(ScrollbarVisibility.Always)
			.Build();
		for (int i = 1; i <= 50; i++)
			table.AddRow(Enumerable.Range(1, 20).Select(c => $"{i}.{c}").ToArray());

		var lines = RenderExact(table, width, height);

		Assert.True(table.ShouldShowVerticalScrollbar());
		// The horizontal scrollbar's row is the last row the table draws into; its rightmost cell
		// (where it meets the vertical bar's column) must be blanked, not a track/thumb glyph.
		string hScrollbarLine = lines.Last(l => l.Length >= width);
		Assert.Equal(' ', hScrollbarLine[width - 1]);
	}

	[Fact]
	public void ScrollbarGutter_KeepsTheBlankColumn()
	{
		// With the gutter on, columns lay out one cell narrower than the scrollbar column, and the
		// gap between the column content and the scrollbar stays blank.
		const int width = 20;
		var table = SharpConsoleUI.Builders.Controls.Table()
			.NoBorder()
			.HideHeader()
			.WithColumns("Col")
			.WithWidth(width)
			.WithHeight(10)
			.WithVerticalScrollbar(ScrollbarVisibility.Always)
			.ScrollbarGutter()
			.Build();
		for (int i = 1; i <= 50; i++)
			table.AddRow(new string('x', 30)); // wide enough to fill the column regardless of width

		var lines = RenderExact(table, width, 10);

		var dataLine = lines.First(l => l.Length >= width && l[0] == 'x');
		// Second-to-last cell is the gutter (blank); the last is the scrollbar.
		Assert.Equal(' ', dataLine[width - 2]);
	}
}
