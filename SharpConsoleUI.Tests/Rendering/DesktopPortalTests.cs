using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

public class DesktopPortalTests
{
	#region Portal Lifecycle

	[Fact]
	public void CreatePortal_SetsUpPortalState()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(5, 5, 30, 10)));

		Assert.NotNull(portal);
		Assert.True(system.DesktopPortalService.HasPortals);
		Assert.Equal(content, portal.Content);
		Assert.Equal(new Rectangle(5, 5, 30, 10), portal.Bounds);
		Assert.True(portal.IsDirty);
	}

	[Fact]
	public void CreatePortal_ComputesInitialControlBounds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		// Control bounds should be computed immediately (not waiting for first render)
		Assert.NotEmpty(portal.ControlBounds);
	}

	[Fact]
	public void CreatePortal_RootNodeIsMeasuredAndArranged()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new MarkupControl(new List<string> { "Test content" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		// RootNode should have non-empty bounds after arrange
		Assert.False(portal.RootNode.AbsoluteBounds.IsEmpty);
		Assert.Equal(40, portal.RootNode.AbsoluteBounds.Width);
		Assert.Equal(10, portal.RootNode.AbsoluteBounds.Height);
	}

	[Fact]
	public void RemovePortal_ClearsPortalState()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(5, 5, 30, 10)));

		system.DesktopPortalService.RemovePortal(portal);

		Assert.False(system.DesktopPortalService.HasPortals);
	}

	[Fact]
	public void DismissAllPortals_RemovesAllPortals()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "One" }),
			Bounds: new Rectangle(0, 0, 20, 5)));
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Two" }),
			Bounds: new Rectangle(0, 0, 20, 5)));

		Assert.Equal(2, system.DesktopPortalService.Portals.Count);

		system.DesktopPortalService.DismissAllPortals();

		Assert.False(system.DesktopPortalService.HasPortals);
	}

	[Fact]
	public void RemovePortal_InvokesOnDismissCallback()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		bool dismissed = false;

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Hello" }),
			Bounds: new Rectangle(0, 0, 20, 5),
			OnDismiss: () => dismissed = true));

		system.DesktopPortalService.RemovePortal(portal);

		Assert.True(dismissed);
	}

	#endregion

	#region Dirty Tracking

	[Fact]
	public void NewPortal_IsMarkedDirty()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Hello" }),
			Bounds: new Rectangle(0, 0, 20, 5)));

		Assert.True(system.DesktopPortalService.AnyPortalDirty());
	}

	#endregion

	#region Rendering

	[Fact]
	public void RenderDesktopPortals_CreatesBufferAndPaintsContent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Portal Text" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		// Trigger render
		system.Render.UpdateDisplay();

		// Buffer should have been created
		Assert.NotNull(portal.Buffer);
		// Portal should be clean after render
		Assert.False(portal.IsDirty);
		// Control bounds should be populated
		Assert.NotEmpty(portal.ControlBounds);
	}

	[Fact]
	public void RenderDesktopPortals_PaintsToBuffer()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Visible Text" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		// Trigger render
		system.Render.UpdateDisplay();

		// Buffer should exist and have non-default content
		Assert.NotNull(portal.Buffer);
		Assert.True(portal.Buffer.Width > 0);
		Assert.True(portal.Buffer.Height > 0);

		// Check that the buffer has actual content (not all default bg)
		bool hasContent = false;
		var defaultBg = system.Theme.DesktopBackgroundColor;
		for (int x = 0; x < Math.Min(portal.Buffer.Width, 12); x++)
		{
			var cell = portal.Buffer.GetCell(x, 0);
			if (cell.Character != new System.Text.Rune(' ') || cell.Background != defaultBg)
			{
				hasContent = true;
				break;
			}
		}
		Assert.True(hasContent, "Buffer should contain non-empty content from MarkupControl painting");
	}

	[Fact]
	public void RenderDesktopPortals_CollectsControlBoundsAfterRender()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		system.Render.UpdateDisplay();

		Assert.NotEmpty(portal.ControlBounds);
		// Bounds should have positive width and height
		var firstBound = portal.ControlBounds[0];
		Assert.True(firstBound.Width > 0, $"Control bound width should be > 0, got {firstBound.Width}");
		Assert.True(firstBound.Height > 0, $"Control bound height should be > 0, got {firstBound.Height}");
	}

	[Fact]
	public void RenderDesktopPortals_TracksPreviousControlBounds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 40, 10)));

		// First render — PreviousControlBounds starts empty
		Assert.Empty(portal.PreviousControlBounds);

		system.Render.UpdateDisplay();

		// After first render, ControlBounds should be populated
		Assert.NotEmpty(portal.ControlBounds);

		// Force a second render by marking dirty
		portal.IsDirty = true;
		system.Render.UpdateDisplay();

		// PreviousControlBounds should now contain the bounds from the first render
		Assert.NotEmpty(portal.PreviousControlBounds);
	}

	[Fact]
	public void RemovePortal_RestoresScreenWithoutCleanupFrame()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(5, 5, 30, 10)));

		// Render so portal has a buffer and control bounds
		system.Render.UpdateDisplay();
		Assert.NotNull(portal.Buffer);
		Assert.NotEmpty(portal.ControlBounds);

		// Remove portal — should restore regions immediately
		system.DesktopPortalService.RemovePortal(portal);

		Assert.False(system.DesktopPortalService.HasPortals);
		// DesktopNeedsRender should be set for the next frame
		Assert.True(system.Render.DesktopNeedsRender);
	}

	[Fact]
	public void RenderWithPortalOpen_DoesNotForceAllWindowsDirty()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		// Create a window and render it
		var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
		window.AddControl(new MarkupControl(new List<string> { "Window content" }));
		system.WindowStateService.AddWindow(window);

		system.Render.UpdateDisplay();
		Assert.False(window.IsDirty); // Window should be clean after render

		// Open a portal
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Portal" }),
			Bounds: new Rectangle(50, 5, 20, 5)));

		// Render — portal is dirty, but window should NOT be force-dirtied
		system.Render.UpdateDisplay();

		// Window should still be clean (not force-invalidated)
		Assert.False(window.IsDirty);
	}

	#endregion

	#region Hit Testing

	[Fact]
	public void HitTest_ReturnsPortal_WhenPointInsideControlBounds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Click Me" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(10, 5, 30, 10)));

		// The control bounds should be at offset (0,0) relative to portal
		// In screen space that's (10,5)
		Assert.NotEmpty(portal.ControlBounds);

		var firstBound = portal.ControlBounds[0];
		int screenX = portal.Bounds.X + firstBound.X;
		int screenY = portal.Bounds.Y + firstBound.Y;

		var hit = system.DesktopPortalService.HitTest(new Point(screenX, screenY));
		Assert.Equal(portal, hit);
	}

	[Fact]
	public void HitTest_ReturnsNull_WhenPointOutsideControlBounds()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var content = new MarkupControl(new List<string> { "Hello" });

		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(10, 5, 30, 10)));

		// Point way outside the portal
		var hit = system.DesktopPortalService.HitTest(new Point(79, 23));
		Assert.Null(hit);
	}

	#endregion

	#region Input Routing

	[Fact]
	public void KeyboardInput_WhenNoPortals_ReachesWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var button = new ButtonControl { Text = "Test" };
		window.AddControl(button);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(button, FocusReason.Programmatic);

		bool clicked = false;
		button.Click += (s, e) => clicked = true;

		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
		system.Input.ProcessInput();

		Assert.True(clicked, "Key should reach button when no portals exist");
	}

	[Fact]
	public void KeyboardInput_WhenPortalOpen_DoesNotReachWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var button = new ButtonControl { Text = "Test" };
		window.AddControl(button);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(button, FocusReason.Programmatic);

		bool clicked = false;
		button.Click += (s, e) => clicked = true;

		// Open a portal
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Portal" }),
			Bounds: new Rectangle(0, 0, 20, 5)));

		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
		system.Input.ProcessInput();

		Assert.False(clicked, "Key should NOT reach button when portal is open");
	}

	[Fact]
	public void EscapeKey_DismissesPortal()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new MarkupControl(new List<string> { "Portal" }),
			Bounds: new Rectangle(0, 0, 20, 5)));

		Assert.True(system.DesktopPortalService.HasPortals);

		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false));
		system.Input.ProcessInput();

		Assert.False(system.DesktopPortalService.HasPortals);
	}

	#endregion

	#region IPortalHost

	[Fact]
	public void Window_ImplementsIPortalHost()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);

		Assert.IsAssignableFrom<IPortalHost>(window);
	}

	[Fact]
	public void DesktopPortalContainer_ImplementsIPortalHost()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var content = new MarkupControl(new List<string> { "Hello" });

		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: content,
			Bounds: new Rectangle(0, 0, 20, 5)));

		Assert.IsAssignableFrom<IPortalHost>(portal.Container);
	}

	#endregion
}
