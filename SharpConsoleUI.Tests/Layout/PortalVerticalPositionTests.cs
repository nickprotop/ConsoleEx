using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Reproduction tests for BUG B: the LSP completion portal's vertical position appears wrong
/// after migrating to host its ListControl via Content. These tests pin down the framework
/// primitives the consumer relies on:
///   1-2. PortalPositioner.CalculateFromPoint vertical placement (below / flip-to-above).
///   3.   The hosted-child portal paint Y matches GetPortalBounds().Y.
/// </summary>
public class PortalVerticalPositionTests
{
	// Minimal concrete PortalContentBase that hosts a child via Content (copied from PortalHostedChildTests).
	private sealed class TestPortal : PortalContentBase
	{
		private readonly Rectangle _bounds;
		public TestPortal(Rectangle bounds, IWindowControl? content = null)
		{
			_bounds = bounds;
			BorderStyle = BoxChars.Rounded;
			Content = content;
		}
		public override Rectangle GetPortalBounds() => _bounds;
		public override bool ProcessMouseEvent(MouseEventArgs args) => ProcessHostedMouseEvent(args);
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{ }
	}

	[Fact]
	public void CalculateFromPoint_BelowOrAbove_PlentyOfSpace_PlacesBelowAtCursorRowPlusOne()
	{
		var result = PortalPositioner.CalculateFromPoint(
			new Point(20, 10),
			new Size(30, 8),
			new Rectangle(0, 0, 120, 40),
			PortalPlacement.BelowOrAbove);

		Assert.Equal(PortalPlacement.Below, result.ActualPlacement);
		// anchor.Bottom = anchor.Y(10) + height(1) = 11 → directly below the cursor row.
		Assert.Equal(11, result.Bounds.Y);
		Assert.Equal(20, result.Bounds.X);
	}

	[Fact]
	public void CalculateFromPoint_BelowOrAbove_NearBottom_FlipsAbove()
	{
		// Cursor at row 38 in a 40-row screen with an 8-tall popup → no room below → flip above.
		var result = PortalPositioner.CalculateFromPoint(
			new Point(20, 38),
			new Size(30, 8),
			new Rectangle(0, 0, 120, 40),
			PortalPlacement.BelowOrAbove);

		Assert.Equal(PortalPlacement.Above, result.ActualPlacement);
		// Above: y = anchor.Y(38) - height(8) = 30.
		Assert.Equal(30, result.Bounds.Y);
	}

	[Fact]
	public void HostedPortal_PaintsBorderTopRow_AtGetPortalBoundsY()
	{
		// Real ConsoleWindowSystem + Window hosting a portal whose Content is a real ListControl.
		// GetPortalBounds returns a KNOWN rect with top at row 11. The painted border top row must
		// land on buffer row 11, not shifted.
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 40);
		var window = new Window(system) { Title = "W", Left = 0, Top = 0, Width = 80, Height = 40 };
		var owner = new MarkupControl(new List<string> { "owner" });
		window.AddControl(owner);
		system.AddWindow(window);

		var list = new ListControl();
		list.AddItem("alpha");
		list.AddItem("beta");
		list.AddItem("gamma");

		const int PortalTopRow = 11;
		var portalBounds = new Rectangle(5, PortalTopRow, 30, 8);
		var portal = new TestPortal(portalBounds, list);

		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		// Render once so the owner is laid out before CreatePortal looks it up.
		window.RenderAndGetVisibleContent(region);

		window.CreatePortal(owner, portal);

		var rendered = window.RenderAndGetVisibleContent(region);

		// Find the row index of the portal's top border (rounded top-left '╭').
		int topBorderRow = -1;
		for (int i = 0; i < rendered.Count; i++)
		{
			if (rendered[i].Contains('╭'))
			{
				topBorderRow = i;
				break;
			}
		}

		Assert.True(topBorderRow >= 0,
			"Portal top border '╭' was not found in the rendered output.\nRendered:\n" +
			string.Join("\n", rendered.Select((l, i) => $"{i,2}: {l}")));

		Assert.Equal(PortalTopRow, topBorderRow);
	}
}
