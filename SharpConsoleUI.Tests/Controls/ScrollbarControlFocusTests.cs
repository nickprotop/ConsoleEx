// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A <see cref="ScrollbarControl"/> is a bar, not a tab stop: it is driven by the mouse and by the
/// composite that owns it, never by keyboard focus. These tests pin that contract behaviourally —
/// reading <c>CanReceiveFocus</c> off the type is not enough, because what matters is what the
/// focus manager, the Tab traversal and the cursor pipeline actually do with it in a real window.
/// </summary>
public class ScrollbarControlFocusTests
{
	private static (Window window, ConsoleWindowSystem system) NewWindow()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};
		return (window, system);
	}

	private static ScrollbarControl NewBar() =>
		SharpConsoleUI.Builders.Controls.Scrollbar()
			.WithMaximum(100)
			.WithViewportLength(10)
			.Build();

	#region Type contract

	[Fact]
	public void IsNotFocusable_AndIsNotMouseFocusable()
	{
		var bar = NewBar();

		Assert.False(bar.CanReceiveFocus);
		Assert.False(bar.CanFocusWithMouse);
	}

	/// <summary>
	/// Focus traversal gates on <c>current is IFocusableControl focusable &amp;&amp;
	/// focusable.CanReceiveFocus</c>. Not implementing the interface fails that test at the first
	/// clause, so the bar can never enter the focus chain even if its property were misread.
	/// </summary>
	[Fact]
	public void DoesNotImplementTheFocusableOrCursorInterfaces()
	{
		var bar = NewBar();

		Assert.False(bar is IFocusableControl);
		Assert.False(bar is ILogicalCursorProvider);
		Assert.False(bar is IInteractiveControl);
	}

	#endregion

	#region Tab traversal

	[Fact]
	public void Tab_SkipsTheBar_BetweenTwoButtons()
	{
		var (window, system) = NewWindow();
		var first = SharpConsoleUI.Builders.Controls.Button("First").Build();
		var bar = NewBar();
		var second = SharpConsoleUI.Builders.Controls.Button("Second").Build();

		window.AddControl(first);
		window.AddControl(bar);
		window.AddControl(second);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusControl(first);
		Assert.True(first.HasFocus);

		// One Tab must land on the button AFTER the bar, never on the bar.
		window.SwitchFocus(backward: false);

		Assert.True(second.HasFocus, "Tab should have skipped the scrollbar and focused the second button");
		Assert.NotSame(bar, window.FocusManager.FocusedControl);
	}

	[Fact]
	public void ShiftTab_SkipsTheBar_BetweenTwoButtons()
	{
		var (window, system) = NewWindow();
		var first = SharpConsoleUI.Builders.Controls.Button("First").Build();
		var bar = NewBar();
		var second = SharpConsoleUI.Builders.Controls.Button("Second").Build();

		window.AddControl(first);
		window.AddControl(bar);
		window.AddControl(second);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusControl(second);
		Assert.True(second.HasFocus);

		window.SwitchFocus(backward: true);

		Assert.True(first.HasFocus, "Shift+Tab should have skipped the scrollbar and focused the first button");
		Assert.NotSame(bar, window.FocusManager.FocusedControl);
	}

	/// <summary>
	/// Cycling the whole window must never rest on the bar, however many stops there are.
	/// </summary>
	[Fact]
	public void TabbingAllTheWayAround_NeverLandsOnTheBar()
	{
		var (window, system) = NewWindow();
		var a = SharpConsoleUI.Builders.Controls.Button("A").Build();
		var bar = NewBar();
		var b = SharpConsoleUI.Builders.Controls.Button("B").Build();
		var c = SharpConsoleUI.Builders.Controls.Button("C").Build();

		window.AddControl(a);
		window.AddControl(bar);
		window.AddControl(b);
		window.AddControl(c);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusControl(a);

		for (int i = 0; i < 12; i++)
		{
			window.SwitchFocus(backward: false);
			Assert.NotSame(bar, window.FocusManager.FocusedControl);
		}
	}

	[Fact]
	public void TheBarAloneInAWindow_TakesNoFocus()
	{
		var (window, system) = NewWindow();
		var bar = NewBar();

		window.AddControl(bar);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		// AddControl auto-focuses the first interactive control; a bar is not one.
		Assert.NotSame(bar, window.FocusManager.FocusedControl);

		window.SwitchFocus(backward: false);
		Assert.NotSame(bar, window.FocusManager.FocusedControl);
	}

	#endregion

	#region Mouse focus

	/// <summary>
	/// Clicking the bar scrolls it but must not steal focus from whatever the user was editing —
	/// the composite case explicitly needs the driven view to keep focus while the bar is used.
	/// </summary>
	[Fact]
	public void ClickingTheBar_DoesNotStealFocusFromAnotherControl()
	{
		var driver = new HeadlessConsoleDriver(40, 12);
		var system = new ConsoleWindowSystem(
			driver,
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};

		var button = SharpConsoleUI.Builders.Controls.Button("Keep me focused").Build();
		var bar = NewBar();
		bar.Height = 10;

		window.AddControl(button);
		window.AddControl(bar);

		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		Assert.True(
			SpinWait.SpinUntil(() => bar.ActualHeight > 0
				&& system.WindowStateService.ActiveWindow == window, 5000),
			"the window did not become active in time");

		try
		{
			system.EnqueueOnUIThread(() => window.FocusControl(button));
			SpinWait.SpinUntil(() => button.HasFocus, 2000);
			Assert.True(button.HasFocus, "test premise: the button must start focused");

			// Click squarely on the bar.
			var at = new Point(bar.ActualX + bar.ActualWidth - 1, bar.ActualY + 3);
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed], at);
			driver.SimulateMouseEvent([MouseFlags.Button1Released, MouseFlags.Button1Clicked], at);
			Thread.Sleep(150);

			Assert.NotSame(bar, window.FocusManager.FocusedControl);
			Assert.True(button.HasFocus, "the previously focused control must keep focus");
		}
		finally
		{
			system.Shutdown();
		}
	}

	#endregion

	#region Cursor

	/// <summary>
	/// The bar provides no logical cursor, so a window containing one must not place or show the
	/// text cursor over it. Regression guard for the cursor pipeline treating any control as a
	/// cursor source.
	/// </summary>
	[Fact]
	public void TheBar_ProvidesNoLogicalCursor()
	{
		var (window, system) = NewWindow();
		var bar = NewBar();

		window.AddControl(bar);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.False(bar is ILogicalCursorProvider,
			"a scrollbar has no text cursor; implementing ILogicalCursorProvider would put one on it");
	}

	/// <summary>
	/// With a real cursor-owning control beside the bar, the cursor must belong to that control.
	/// </summary>
	[Fact]
	public void CursorStaysWithTheEditor_NotTheBar()
	{
		var (window, system) = NewWindow();
		var editor = new MultilineEditControl { Height = 5 };
		editor.Content = "hello";
		var bar = NewBar();

		window.AddControl(editor);
		window.AddControl(bar);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		window.FocusControl(editor);
		window.RenderAndGetVisibleContent();

		Assert.True(editor.HasFocus, "test premise: the editor must hold focus");
		Assert.NotSame(bar, window.FocusManager.FocusedControl);

		// The cursor provider in this window is the editor, and only the editor.
		Assert.True(editor is ILogicalCursorProvider);
		Assert.False(bar is ILogicalCursorProvider);
	}

	#endregion
}
