// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.InputHandling;

/// <summary>
/// A desktop portal only takes the keyboard when it wants it. A passive overlay — a toast — is open
/// on top of the window but must let the user keep typing; an interactive one — a dropdown — must not.
/// These drive the REAL input path (EnqueueKey + ProcessInput) rather than calling the coordinator,
/// because the bug these cover was the dispatch decision, not any control's own key handling.
/// </summary>
public class PortalInputCaptureTests
{
	/// <summary>Builds a system with one window holding a focused editor, as a composer would be.</summary>
	private static (ConsoleWindowSystem System, MultilineEditControl Editor) SystemWithFocusedEditor()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var editor = new MultilineEditControl();

		window.AddControl(editor);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(editor, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		// The editor only accepts text once it is in edit mode (Enter), as a live composer would be.
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
		system.Input.ProcessInput();

		return (system, editor);
	}

	private static void Type(ConsoleWindowSystem system, char c, ConsoleKey key)
	{
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(c, key, false, false, false));
		system.Input.ProcessInput();
	}

	[Fact]
	public void Baseline_NoPortalAtAll_KeyReachesEditor()
	{
		var (system, editor) = SystemWithFocusedEditor();
		Type(system, 'X', ConsoleKey.X);
		Assert.Equal("X", editor.Content);
	}

	[Fact]
	public void APassivePortal_DoesNotTakeTheKeyboard()
	{
		var (system, editor) = SystemWithFocusedEditor();

		// A toast: content that is not interactive, declared non-capturing.
		var toast = new ToastContent("Copied", NotificationSeverity.Info, ColorRole.Info);
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: toast,
			Bounds: new Rectangle(40, 20, 20, 3),
			CapturesInput: false));

		Type(system, 'X', ConsoleKey.X);

		Assert.Equal("X", editor.Content);
	}

	[Fact]
	public void AnInteractivePortal_StillTakesTheKeyboard()
	{
		var (system, editor) = SystemWithFocusedEditor();

		// A dropdown-like portal: interactive content, so it captures by default.
		var menu = new ListControl();
		menu.AddItem("one");
		menu.AddItem("two");
		var portal = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: menu,
			Bounds: new Rectangle(10, 5, 20, 6)));

		Assert.True(portal.CapturesInput);

		Type(system, 'X', ConsoleKey.X);

		// The key went to the portal, not the window underneath.
		Assert.Equal(string.Empty, editor.Content);
	}

	[Fact]
	public void AToastOverADropdown_LeavesTheDropdownItsKeys()
	{
		var (system, editor) = SystemWithFocusedEditor();

		var menu = new ListControl();
		menu.AddItem("one");
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: menu,
			Bounds: new Rectangle(10, 5, 20, 6)));

		// The toast opens LATER, so it is topmost — but it does not want keys, and must not
		// take them away from the dropdown that does.
		var toast = new ToastContent("Saved", NotificationSeverity.Info, ColorRole.Info);
		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: toast,
			Bounds: new Rectangle(40, 20, 20, 3),
			CapturesInput: false));

		Type(system, 'X', ConsoleKey.X);

		Assert.Equal(string.Empty, editor.Content);
	}

	[Fact]
	public void CapturesInput_DefaultsFromTheContent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var passive = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ToastContent("hi", NotificationSeverity.Info, ColorRole.Info),
			Bounds: new Rectangle(0, 0, 10, 3)));
		Assert.False(passive.CapturesInput);

		var interactive = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ListControl(),
			Bounds: new Rectangle(0, 5, 10, 3)));
		Assert.True(interactive.CapturesInput);
	}

	[Fact]
	public void AnExplicitCapturesInput_OverridesTheContent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var forcedOff = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ListControl(),
			Bounds: new Rectangle(0, 0, 10, 3),
			CapturesInput: false));
		Assert.False(forcedOff.CapturesInput);

		var forcedOn = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ToastContent("hi", NotificationSeverity.Info, ColorRole.Info),
			Bounds: new Rectangle(0, 5, 10, 3),
			CapturesInput: true));
		Assert.True(forcedOn.CapturesInput);
	}

	[Fact]
	public void TopCapturingPortal_LooksPastPassiveOnes()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		var menu = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ListControl(),
			Bounds: new Rectangle(0, 0, 10, 3)));

		var toast = system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ToastContent("hi", NotificationSeverity.Info, ColorRole.Info),
			Bounds: new Rectangle(0, 5, 10, 3),
			CapturesInput: false));

		// The toast is on top...
		Assert.Equal(toast, system.DesktopPortalService.TopPortal);
		// ...but the keyboard belongs to the menu.
		Assert.Equal(menu, system.DesktopPortalService.TopCapturingPortal);
	}

	[Fact]
	public void WithOnlyPassivePortals_ThereIsNoCapturingPortal()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();

		system.DesktopPortalService.CreatePortal(new DesktopPortalOptions(
			Content: new ToastContent("hi", NotificationSeverity.Info, ColorRole.Info),
			Bounds: new Rectangle(0, 0, 10, 3),
			CapturesInput: false));

		Assert.True(system.DesktopPortalService.HasPortals);
		Assert.Null(system.DesktopPortalService.TopCapturingPortal);
	}
}
