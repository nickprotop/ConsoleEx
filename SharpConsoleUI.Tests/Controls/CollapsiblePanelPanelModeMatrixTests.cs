// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using ControlsFactory = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Exhaustive coverage for the "panel mode" of <see cref="CollapsiblePanel"/> — the two flags
/// <see cref="CollapsiblePanel.Collapsible"/> and <see cref="CollapsiblePanel.ShowHeader"/> that let
/// a collapsible section degrade into a plain (optionally bordered) container. These tests assert
/// effective-header geometry, focus participation, indicator suppression, locked expansion,
/// runtime flag toggling, body interactivity/cursor reporting, and host integration.
/// </summary>
public class CollapsiblePanelPanelModeMatrixTests
{
	private const string ExpandedIndicator = "▾"; // ▾
	private const string CollapsedIndicator = "▸"; // ▸

	#region helpers (window/system setup; rendering reuses ContainerTestHelpers)

	/// <summary>
	/// Builds a window hosting <paramref name="panel"/>, adds it to the system, and renders twice so
	/// the DOM layout tree and absolute bounds are current. Mirrors CollapsiblePanelMouseFocusTests.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window) HostInWindow(
		CollapsiblePanel panel, int width = 40, int height = 16)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		return (system, window);
	}

	private static MouseEventArgs Click(int x, int y, Window window)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked }, pos, pos, pos, window);
	}

	private static ConsoleKeyInfo EnterKey =>
		new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

	private static ConsoleKeyInfo SpaceKey =>
		new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false);

	private static string RenderedText(CollapsiblePanel panel, int width = 30, int height = 12) =>
		ContainerTestHelpers.StripAnsiCodes(
			ContainerTestHelpers.RenderToLines(panel, width, height));

	#endregion

	#region A. Combination matrix — effective header geometry + focus participation

	[Theory]
	[InlineData(true, true, CollapsibleHeaderStyle.Borderless)]
	[InlineData(true, true, CollapsibleHeaderStyle.Bordered)]
	[InlineData(true, false, CollapsibleHeaderStyle.Borderless)] // invalid combo → header shown
	[InlineData(true, false, CollapsibleHeaderStyle.Bordered)]   // invalid combo → header shown
	[InlineData(false, true, CollapsibleHeaderStyle.Borderless)]
	[InlineData(false, true, CollapsibleHeaderStyle.Bordered)]
	[InlineData(false, false, CollapsibleHeaderStyle.Borderless)]
	[InlineData(false, false, CollapsibleHeaderStyle.Bordered)]
	public void Matrix_EffectiveHeaderAndFocus(bool collapsible, bool showHeader, CollapsibleHeaderStyle style)
	{
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = collapsible,
			ShowHeader = showHeader,
			HeaderStyle = style,
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		bool effectiveHeader = showHeader || collapsible;
		int expectedHeaderH = style == CollapsibleHeaderStyle.Bordered ? 1 : (effectiveHeader ? 1 : 0);

		Assert.Equal(expectedHeaderH, panel.HeaderHeightForTest);
		Assert.Equal(collapsible, panel.CanReceiveFocus);
		Assert.True(panel.IsExpanded);
	}

	[Theory]
	[InlineData(true, true, CollapsibleHeaderStyle.Borderless)]
	[InlineData(true, false, CollapsibleHeaderStyle.Bordered)]
	[InlineData(false, false, CollapsibleHeaderStyle.Borderless)]
	[InlineData(false, true, CollapsibleHeaderStyle.Bordered)]
	public void Matrix_HeaderSeparatorAddsRow_OnlyWhenBorderlessHeaderShown(
		bool collapsible, bool showHeader, CollapsibleHeaderStyle style)
	{
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = collapsible,
			ShowHeader = showHeader,
			HeaderStyle = style,
			ShowHeaderSeparator = true,
			Width = 24
		};

		bool effectiveHeader = showHeader || collapsible;
		int baseH;
		int withSep;
		if (style == CollapsibleHeaderStyle.Bordered)
		{
			// Bordered ignores the separator entirely: always 1.
			baseH = withSep = 1;
		}
		else if (effectiveHeader)
		{
			baseH = 1;
			withSep = 2; // header + separator
		}
		else
		{
			baseH = withSep = 0; // suppressed header has no separator
		}

		Assert.Equal(withSep, panel.HeaderHeightForTest);

		// Disable separator and re-check the base height for the same combo.
		panel.ShowHeaderSeparator = false;
		Assert.Equal(baseH, panel.HeaderHeightForTest);
	}

	#endregion

	#region B. Indicator presence (rendered)

	[Theory]
	[InlineData(CollapsibleHeaderStyle.Borderless)]
	[InlineData(CollapsibleHeaderStyle.Bordered)]
	public void Collapsible_Expanded_RendersExpandedIndicator(CollapsibleHeaderStyle style)
	{
		var panel = new CollapsiblePanel { Title = "T", Collapsible = true, HeaderStyle = style, Width = 24 };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		var text = RenderedText(panel);
		Assert.Contains(ExpandedIndicator, text);
		Assert.DoesNotContain(CollapsedIndicator, text);
	}

	[Theory]
	[InlineData(CollapsibleHeaderStyle.Borderless)]
	[InlineData(CollapsibleHeaderStyle.Bordered)]
	public void Collapsible_Collapsed_RendersCollapsedIndicator(CollapsibleHeaderStyle style)
	{
		var panel = new CollapsiblePanel { Title = "T", Collapsible = true, HeaderStyle = style, Width = 24 };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		panel.Collapse();

		var text = RenderedText(panel);
		Assert.Contains(CollapsedIndicator, text);
		Assert.DoesNotContain(ExpandedIndicator, text);
	}

	[Theory]
	[InlineData(CollapsibleHeaderStyle.Borderless)]
	[InlineData(CollapsibleHeaderStyle.Bordered)]
	public void NonCollapsible_RendersNeitherIndicator(CollapsibleHeaderStyle style)
	{
		var panel = new CollapsiblePanel { Title = "T", Collapsible = false, HeaderStyle = style, Width = 24 };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		var text = RenderedText(panel);
		Assert.DoesNotContain(ExpandedIndicator, text);
		Assert.DoesNotContain(CollapsedIndicator, text);
		// The title still renders (header is shown when ShowHeader defaults true).
		Assert.Contains("T", text);
	}

	#endregion

	#region C. Locked expansion is bulletproof

	[Fact]
	public void NonCollapsible_AllCollapseAttempts_AreNoOps_NoEvent()
	{
		var panel = new CollapsiblePanel { Title = "S", Collapsible = false };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		int events = 0;
		panel.ExpandedChanged += (_, __) => events++;

		panel.Collapse();
		panel.Toggle();
		panel.IsExpanded = false;
		panel.ProcessKey(EnterKey);
		panel.ProcessKey(SpaceKey);

		Assert.True(panel.IsExpanded);
		Assert.Equal(0, events);
	}

	[Fact]
	public void NonCollapsible_ProcessKey_ReturnsFalse_ForEnterAndSpace()
	{
		var panel = new CollapsiblePanel { Title = "S", Collapsible = false };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		Assert.False(panel.ProcessKey(EnterKey));
		Assert.False(panel.ProcessKey(SpaceKey));
	}

	[Fact]
	public void NonCollapsible_HeaderClick_NotConsumed_NoToggle()
	{
		var panel = new CollapsiblePanel { Title = "S", Collapsible = false, Width = 30 };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		var (system, window) = HostInWindow(panel);

		int events = 0;
		panel.ExpandedChanged += (_, __) => events++;

		// Header row is y=0 (borderless). Click it.
		bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(Click(1, 0, window));

		Assert.False(handled);
		Assert.True(panel.IsExpanded);
		Assert.Equal(0, events);
	}

	#endregion

	#region D. Runtime flag toggling

	[Fact]
	public void SettingCollapsibleFalse_WhenCollapsed_ForcesExpanded()
	{
		var panel = new CollapsiblePanel { Title = "S" };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		panel.Collapse();
		Assert.False(panel.IsExpanded);

		panel.Collapsible = false;

		Assert.True(panel.IsExpanded);
	}

	[Fact]
	public void SettingCollapsibleFalse_WhenCollapsed_FiresExpandedChangedOnce()
	{
		var panel = new CollapsiblePanel { Title = "S" };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		panel.Collapse();

		int events = 0;
		bool? last = null;
		panel.ExpandedChanged += (_, v) => { events++; last = v; };

		panel.Collapsible = false;

		Assert.Equal(1, events);
		Assert.True(last);
	}

	[Fact]
	public void SettingCollapsibleFalse_WhenAlreadyExpanded_DoesNotFireEvent()
	{
		var panel = new CollapsiblePanel { Title = "S" };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		Assert.True(panel.IsExpanded);

		int events = 0;
		panel.ExpandedChanged += (_, __) => events++;

		panel.Collapsible = false;

		Assert.True(panel.IsExpanded);
		Assert.Equal(0, events); // already expanded: no redundant event
	}

	[Fact]
	public void NonCollapsibleThenCollapsible_RegainsTabStop_AndIndicator()
	{
		var panel = new CollapsiblePanel { Title = "T", Collapsible = false, Width = 24 };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		// Disabled: not a Tab stop, no indicator.
		Assert.False(panel.CanReceiveFocus);
		Assert.DoesNotContain(ExpandedIndicator, RenderedText(panel));

		// Re-enable: Tab stop restored, indicator returns.
		panel.Collapsible = true;

		Assert.True(panel.CanReceiveFocus);
		Assert.Contains(ExpandedIndicator, RenderedText(panel));
	}

	[Fact]
	public void TogglingShowHeader_OnNonCollapsibleBorderless_ChangesHeaderHeight()
	{
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = false,
			ShowHeader = true,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			Width = 24
		};
		Assert.Equal(1, panel.HeaderHeightForTest);

		panel.ShowHeader = false;
		Assert.Equal(0, panel.HeaderHeightForTest);

		panel.ShowHeader = true;
		Assert.Equal(1, panel.HeaderHeightForTest);
	}

	#endregion

	#region E. Non-collapsible BORDERED header click + body focus

	[Fact]
	public void NonCollapsibleBordered_HeaderClickInert_BodyClickFocusesButton()
	{
		var button = ControlsFactory.Button("Body Button").Build();
		var panel = ControlsFactory.CollapsiblePanel("Header")
			.NonCollapsible()
			.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
			.WithWidth(30)
			.AddControl(button)
			.Build();

		var (system, window) = HostInWindow(panel);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Header is content row y=0 (bordered top border). Clicking it must NOT toggle and must
		// NOT be consumed by the panel.
		bool headerHandled = ((IMouseAwareControl)panel).ProcessMouseEvent(Click(2, 0, window));
		Assert.False(headerHandled);
		Assert.True(panel.IsExpanded);

		// Body click: the bordered body starts below the top border (HeaderHeight=1). The button is
		// the first body child. Use its own arranged Y (panel-relative) to address it precisely.
		int bodyRowInPanel = button.ActualY - panel.ActualY;
		Assert.True(bodyRowInPanel >= 1,
			$"Bordered body must start below the top border (got panel-relative row {bodyRowInPanel}).");

		bool bodyHandled = ((IMouseAwareControl)panel).ProcessMouseEvent(Click(2, bodyRowInPanel, window));
		Assert.True(bodyHandled);
		Assert.True(window.FocusManager.IsFocused(button),
			"Clicking the bordered panel's body button must focus it even though the panel is non-collapsible.");
	}

	#endregion

	#region F. Body interactivity in panel mode

	[Fact]
	public void PanelMode_BodyButton_IsFocusable_AndEnterFiresClick()
	{
		bool clicked = false;
		var button = ControlsFactory.Button("Go").Build();
		button.Click += (_, __) => clicked = true;

		var panel = ControlsFactory.CollapsiblePanel("Header")
			.NonCollapsible()
			.HideHeader()
			.WithWidth(30)
			.AddControl(button)
			.Build();

		var (system, window) = HostInWindow(panel);

		// The non-collapsible panel is not itself a Tab stop; focus must pass through to the button.
		window.FocusManager.SetFocus(button, FocusReason.Programmatic);
		Assert.True(window.FocusManager.IsFocused(button));

		// Enter on the focused button fires its click handler.
		bool handled = button.ProcessKey(EnterKey);

		Assert.True(handled);
		Assert.True(clicked, "Enter on a focused body button must fire its Click handler in panel mode.");
	}

	[Fact]
	public void PanelMode_BodyPrompt_ReceivesTypedCharacter()
	{
		var prompt = new PromptControl { Prompt = "> " };
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = false,
			ShowHeader = false
		};
		panel.AddControl(prompt);

		var (system, window) = HostInWindow(panel);
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);

		// Type a character into the focused prompt.
		bool handled = prompt.ProcessKey(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false));

		Assert.True(handled);
		Assert.Equal("x", prompt.Input);
	}

	[Fact]
	public void PanelMode_NonCollapsible_PassesFocusThroughToBodyChildViaTab()
	{
		var button = ControlsFactory.Button("Body Button").Build();
		var panel = ControlsFactory.CollapsiblePanel("Header")
			.NonCollapsible()
			.WithWidth(30)
			.AddControl(button)
			.Build();

		var (system, window) = HostInWindow(panel);

		// The panel is not a focus stop; Tab traversal lands directly on the body button.
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		for (int i = 0; i < 4 && !window.FocusManager.IsFocused(button); i++)
			window.SwitchFocus(backward: false);

		Assert.True(window.FocusManager.IsFocused(button),
			"A non-collapsible panel must pass focus through to its body children (transparent container).");
	}

	#endregion

	#region G. Cursor reporting with header hidden

	[Fact]
	public void Cursor_HeaderlessBorderless_ReportsYOffsetZero()
	{
		var prompt = new PromptControl { Prompt = "> " };
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Borderless
		};
		panel.AddControl(prompt);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var cursor = ((ILogicalCursorProvider)panel).GetLogicalCursorPosition();

		Assert.NotNull(cursor);
		// Borderless + headerless: prompt is the first body child at panel-relative Y=0.
		Assert.Equal(0, cursor!.Value.Y);
	}

	[Fact]
	public void Cursor_HeaderlessBordered_ReportsYOffsetOne()
	{
		var prompt = new PromptControl { Prompt = "> " };
		var panel = new CollapsiblePanel
		{
			Title = "T",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered
		};
		panel.AddControl(prompt);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus(prompt, FocusReason.Programmatic);
		window.RenderAndGetVisibleContent();

		var cursor = ((ILogicalCursorProvider)panel).GetLogicalCursorPosition();

		Assert.NotNull(cursor);
		// Bordered + headerless: top border occupies panel-relative Y=0, so the body prompt is at Y=1.
		Assert.Equal(1, cursor!.Value.Y);
	}

	#endregion

	#region H. Host integration

	[Fact]
	public void HostIntegration_NonCollapsibleHeaderless_InsideScrollablePanel_RendersTallContent()
	{
		var panel = ControlsFactory.CollapsiblePanel("Header")
			.NonCollapsible()
			.HideHeader()
			.WithWidth(30)
			.Build();
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"row {i:00}"));

		var scroller = ControlsFactory.ScrollablePanel()
			.WithHeight(8)
			.AddControl(panel)
			.Build();

		var lines = ContainerTestHelpers.RenderToLines(scroller, width: 40, height: 12);
		var text = ContainerTestHelpers.StripAnsiCodes(lines);

		// Renders without error and shows top-of-content rows; no header chrome.
		Assert.Contains("row 00", text);
		Assert.DoesNotContain("Header", text); // header suppressed
	}

	[Fact]
	public void HostIntegration_NonCollapsibleHeaderless_StackedWithCollapsibleSibling_TogglesIndependently()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(50, 24);
		var window = new Window(system) { Width = 50, Height = 24 };

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid) { Width = 44 };

		var plain = ControlsFactory.CollapsiblePanel("PlainPanel")
			.NonCollapsible()
			.HideHeader()
			.WithWidth(40)
			.AddControl(ContainerTestHelpers.CreateLabel("PLAIN BODY"))
			.Build();

		var collapsible = ControlsFactory.CollapsiblePanel("Collapsible Q")
			.WithWidth(40)
			.AddControl(ContainerTestHelpers.CreateLabel("COLLAPSIBLE BODY"))
			.Build();

		column.AddContent(plain);
		column.AddContent(collapsible);
		grid.AddColumn(column);
		window.AddControl(grid);

		var both = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("PLAIN BODY", both);
		Assert.Contains("COLLAPSIBLE BODY", both);

		// Collapse the collapsible sibling: its body hides, the plain panel is unaffected.
		collapsible.Collapse();
		var afterCollapse = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("PLAIN BODY", afterCollapse);
		Assert.DoesNotContain("COLLAPSIBLE BODY", afterCollapse);
		Assert.True(plain.IsExpanded, "Non-collapsible panel must stay expanded.");
		Assert.False(collapsible.IsExpanded);

		// Re-expand the sibling: both bodies show again, plain panel still expanded.
		collapsible.Expand();
		var afterExpand = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());
		Assert.Contains("PLAIN BODY", afterExpand);
		Assert.Contains("COLLAPSIBLE BODY", afterExpand);
		Assert.True(plain.IsExpanded);
		Assert.True(collapsible.IsExpanded);
	}

	#endregion

	#region I. Structural rendering snapshot — bordered headerless box

	[Fact]
	public void BorderedHeaderless_DrawsCleanBoxAroundBody_NoTitle()
	{
		var panel = new CollapsiblePanel
		{
			Title = "SECRET TITLE",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered,
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 30, height: 10);

		// Find the content rows that actually carry the box (some leading/trailing window rows blank).
		var boxRows = lines.Where(l => l.Contains('│')).ToList(); // │ side rows
		Assert.NotEmpty(boxRows);

		// Top border corners ┌ ... ┐
		Assert.Contains(lines, l => l.Contains('┌') && l.Contains('┐'));
		// Bottom border corners └ ... ┘
		Assert.Contains(lines, l => l.Contains('└') && l.Contains('┘'));
		// At least one middle row has │ on both sides.
		Assert.Contains(lines, l =>
		{
			int first = l.IndexOf('│');
			int lastIdx = l.LastIndexOf('│');
			return first >= 0 && lastIdx > first;
		});
		// Body text appears inside the box.
		Assert.Contains(lines, l => l.Contains("body"));
		// The title must NOT appear anywhere (header suppressed).
		Assert.DoesNotContain(lines, l => l.Contains("SECRET TITLE"));
	}

	[Fact]
	public void DoubleLine_DrawsDoubleLineBoxWithTitle()
	{
		// DoubleLine header style: a full ╔═╗║╚╝ box with the title embedded in the top border.
		var panel = new CollapsiblePanel
		{
			Title = "DBL",
			HeaderStyle = CollapsibleHeaderStyle.DoubleLine,
			Width = 20
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 24, height: 8);

		// Double-line corners + side rows prove a full double-line box (not single-line, not a top rule only).
		Assert.Contains(lines, l => l.Contains('╔') && l.Contains('╗'));
		Assert.Contains(lines, l => l.Contains('╚') && l.Contains('╝'));
		Assert.Contains(lines, l => l.Contains('║'));
		Assert.Contains(lines, l => l.Contains("body"));
		// Must not fall back to single-line or rounded corners.
		Assert.DoesNotContain(lines, l => l.Contains('┌'));
		Assert.DoesNotContain(lines, l => l.Contains('╭'));
	}

	[Fact]
	public void Padding_InsetsBodyContent_DefaultZeroIsNoOp()
	{
		// Default padding (0) — body child sits flush against the border-inset content area.
		var noPad = new CollapsiblePanel
		{
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered,
			Width = 24
		};
		noPad.AddControl(ContainerTestHelpers.CreateLabel("X"));
		var noPadLines = ContainerTestHelpers.RenderToLines(noPad, width: 30, height: 8);
		int noPadCol = ColumnOf(noPadLines, 'X');

		// With left padding 3, the same 'X' shifts right by 3 columns.
		var pad = new CollapsiblePanel
		{
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered,
			Padding = new SharpConsoleUI.Layout.Padding(3, 0, 0, 0),
			Width = 24
		};
		pad.AddControl(ContainerTestHelpers.CreateLabel("X"));
		var padLines = ContainerTestHelpers.RenderToLines(pad, width: 30, height: 8);
		int padCol = ColumnOf(padLines, 'X');

		Assert.True(noPadCol >= 0 && padCol >= 0, "X must render in both panels");
		Assert.Equal(noPadCol + 3, padCol);
	}

	private static int ColumnOf(System.Collections.Generic.List<string> lines, char c)
	{
		foreach (var l in lines)
		{
			int i = l.IndexOf(c);
			if (i >= 0) return i;
		}
		return -1;
	}

	[Fact]
	public void DoubleLine_NonCollapsibleHeaderless_DrawsTitlelessDoubleBox()
	{
		// Panel mode: a plain double-line box with no title in the top border.
		var panel = new CollapsiblePanel
		{
			Title = "SECRET TITLE",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.DoubleLine,
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 30, height: 10);

		Assert.Contains(lines, l => l.Contains('╔') && l.Contains('╗'));
		Assert.Contains(lines, l => l.Contains('╚') && l.Contains('╝'));
		Assert.Contains(lines, l => l.Contains("body"));
		Assert.DoesNotContain(lines, l => l.Contains("SECRET TITLE")); // header suppressed
	}

	#endregion

	#region J. Borderless headerless = zero chrome

	[Fact]
	public void BorderlessHeaderless_ZeroChrome_FirstRowIsBody()
	{
		var panel = new CollapsiblePanel
		{
			Title = "HIDDEN",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("FIRSTBODY"));
		panel.AddControl(ContainerTestHelpers.CreateLabel("SECONDBODY"));

		Assert.Equal(0, panel.HeaderHeightForTest);

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 30, height: 10);

		// The first non-empty rendered row must be the first body child's text — no header/separator row.
		int firstNonEmpty = lines.FindIndex(l => l.Trim().Length > 0);
		Assert.True(firstNonEmpty >= 0, "Expected at least one non-empty row.");
		Assert.Contains("FIRSTBODY", lines[firstNonEmpty]);
		Assert.DoesNotContain(lines, l => l.Contains("HIDDEN"));
	}

	#endregion

	#region K. ShowHeaderSeparator interaction

	[Fact]
	public void NonCollapsibleBorderless_WithSeparator_RendersSeparatorBelowHeader()
	{
		var panel = new CollapsiblePanel
		{
			Title = "HEAD",
			Collapsible = false,
			ShowHeader = true,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			ShowHeaderSeparator = true,
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		Assert.Equal(2, panel.HeaderHeightForTest); // header + separator

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 30, height: 10);
		int headerRow = lines.FindIndex(l => l.Contains("HEAD"));
		Assert.True(headerRow >= 0, "Header row must render.");
		// The next row is the separator made of '─'.
		Assert.True(headerRow + 1 < lines.Count, "Separator row must exist below the header.");
		Assert.Contains('─', lines[headerRow + 1]);
	}

	[Fact]
	public void NonCollapsibleBorderless_HeaderHidden_NoSeparatorRow_ZeroHeader()
	{
		var panel = new CollapsiblePanel
		{
			Title = "HEAD",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			ShowHeaderSeparator = true, // requested but must be ignored when header hidden
			Width = 24
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("BODYONLY"));

		Assert.Equal(0, panel.HeaderHeightForTest);

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 30, height: 10);
		int firstNonEmpty = lines.FindIndex(l => l.Trim().Length > 0);
		Assert.True(firstNonEmpty >= 0);
		// First non-empty row is body, not a separator rule.
		Assert.Contains("BODYONLY", lines[firstNonEmpty]);
		Assert.DoesNotContain(lines, l => l.Contains("HEAD"));
	}

	#endregion
}
