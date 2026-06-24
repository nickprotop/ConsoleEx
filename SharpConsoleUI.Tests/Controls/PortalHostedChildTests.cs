using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class PortalHostedChildTests
{
	// Minimal concrete PortalContentBase that hosts a child via the new Content property.
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
		// With a hosted Content, the base handles paint+mouse; ProcessMouseEvent override is still
		// required by the abstract contract but should defer to base hosting when Content is set.
		public override bool ProcessMouseEvent(MouseEventArgs args) => ProcessHostedMouseEvent(args);
		// Required by the abstract contract; unused while Content is set (the base paints the child instead).
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{ }
	}

	[Fact]
	public void PortalContentBase_IsIContainer()
	{
		var portal = new TestPortal(new Rectangle(0, 0, 10, 5));
		Assert.IsAssignableFrom<IContainer>(portal);
	}

	[Fact]
	public void HostedChild_Invalidate_PropagatesThroughPortalToWindowContainer()
	{
		// portal.Container = a fake IContainer; child.Container = portal; child.Invalidate reaches the fake.
		var fake = new RecordingContainer();
		var child = new ListControl();
		var portal = new TestPortal(new Rectangle(0, 0, 10, 5), child) { Container = fake };

		// Setting Content must have wired child.Container = portal.
		Assert.Same(portal, ((IWindowControl)child).Container);

		fake.Reset();
		child.Invalidate(Invalidation.Relayout); // child → portal → fake
		Assert.True(fake.WasInvalidated);
	}

	[Fact]
	public void HostedListInPortal_ItemsChange_SelfInvalidatesWindow_NoManualInvalidate()
	{
		// "Real thing" end-to-end: a real ConsoleWindowSystem + Window hosts a portal whose Content is a
		// real ListControl. Mutating the list with AddItem (NO manual Invalidate) must propagate
		// child -> portal -> window so the window has pending work for the next frame.
		var system = TestWindowSystemBuilder.CreateTestSystem(60, 24);
		var window = new Window(system) { Title = "W", Left = 0, Top = 0, Width = 50, Height = 18 };
		var owner = new MarkupControl(new List<string> { "owner" });
		window.AddControl(owner);
		system.AddWindow(window);

		var list = new ListControl();
		list.AddItem("a");
		list.AddItem("b");
		var portal = new TestPortal(new Rectangle(2, 2, 20, 8), list);

		// Render once so the owner is laid out in the DOM before CreatePortal looks it up.
		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);

		window.CreatePortal(owner, portal); // DOM-hosts the portal; sets portal.Container = window

		// Drain pending work to None with a full visible render.
		window.RenderAndGetVisibleContent(region);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Mutate the hosted list — NO manual Invalidate. It must self-invalidate the window via
		// list.Container (= portal) -> portal.Container (= window).
		list.AddItem("c");

		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void GetVisibleHeightForControl_ReturnsInnerContentHeight_NotFullPortalHeight()
	{
		// Regression: a bordered portal paints its child into bounds shrunk by 1 per side. The child's
		// scroll/ensure-visible math asks Container.GetVisibleHeightForControl — that MUST return the inner
		// content height (portal height - 2 for the border), NOT the full portal height. Returning the full
		// height made a hosted ListControl believe it had 2 extra rows, so the selection moved one row past
		// the viewport before scrolling (while the scrollbar — which uses the painted inner rect — stayed
		// correct). https://(this session)
		var list = new ListControl();
		var portal = new TestPortal(new Rectangle(0, 0, 20, 10), list); // rounded border -> inner height = 8

		// Before any paint, derived from bounds - border.
		Assert.Equal(8, ((IContainer)portal).GetVisibleHeightForControl(list));

		// After a paint the recorded inner rect height is used (same value here).
		var buf = new CharacterBuffer(20, 10);
		((IDOMPaintable)portal).PaintDOM(buf, new LayoutRect(0, 0, 20, 10), new LayoutRect(0, 0, 20, 10),
			Color.White, Color.Black);
		Assert.Equal(8, ((IContainer)portal).GetVisibleHeightForControl(list));
	}

	[Fact]
	public void GetVisibleHeightForControl_Borderless_ReturnsFullHeight()
	{
		// A borderless portal paints the child into the full bounds, so no border rows are subtracted.
		var list = new ListControl();
		var portal = new TestPortal(new Rectangle(0, 0, 20, 10), list) { BorderStyle = null };
		Assert.Equal(10, ((IContainer)portal).GetVisibleHeightForControl(list));
	}

	// Simple IContainer spy.
	private sealed class RecordingContainer : IContainer
	{
		public bool WasInvalidated { get; private set; }
		public void Reset() => WasInvalidated = false;
		public Color BackgroundColor { get; set; } = Color.Black;
		public Color ForegroundColor { get; set; } = Color.White;
		public ConsoleWindowSystem? GetConsoleWindowSystem => null;
		public void Invalidate(Invalidation work, IWindowControl? callerControl = null) => WasInvalidated = true;
		public int? GetVisibleHeightForControl(IWindowControl control) => null;
	}
}
