// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.FocusReach
{
	/// <summary>
	/// Which controls Tab can actually reach.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>focus</c> says what HAS focus. The harder question is what COULD: a
	/// control that is present in the layout tree, enabled, and marked focusable
	/// can still be unreachable because the scope it sits in never offers it.
	/// That is invisible in a snapshot, invisible in the tree, and the usual way
	/// it is found is a user reporting that Tab skips a field.
	/// </para>
	/// <para>
	/// Reachability is reported by walking focus the way Tab does rather than by
	/// reimplementing the rules, so the answer cannot drift from the behaviour
	/// it describes.
	/// </para>
	/// </remarks>
	public class FocusReachabilityTests
	{
		private static (ConsoleWindowSystem System, Window Window) Build()
		{
			var system = new ConsoleWindowSystem(new HeadlessConsoleDriver(60, 20),
				options: ConsoleWindowSystemOptions.Create() with
				{
					PipedInput = new PipedInputOptions(Enabled: false),
				});

			var window = new Window(system) { Left = 0, Top = 0, Width = 40, Height = 12 };
			system.AddWindow(window);
			return (system, window);
		}

		[Fact]
		public void EveryFocusableControlIsReported()
		{
			var (_, window) = Build();

			window.AddControl(new ButtonControl { Name = "first", Text = "One" });
			window.AddControl(new ButtonControl { Name = "second", Text = "Two" });
			window.AddControl(new ButtonControl { Name = "third", Text = "Three" });
			window.RenderAndGetVisibleContent();

			var reachable = window.FocusManager.ReachableControls()
				.Select(c => (c as IWindowControl)?.Name).ToList();

			Assert.Equal(new[] { "first", "second", "third" }, reachable);
		}

		[Fact]
		public void ADisabledControlIsNotReachable()
		{
			// The commonest cause of "Tab skips my field", and the one a snapshot
			// cannot show: a disabled control looks the same as an enabled one in
			// the layout tree.
			var (_, window) = Build();

			window.AddControl(new ButtonControl { Name = "first", Text = "One" });
			window.AddControl(new ButtonControl { Name = "off", Text = "Two", IsEnabled = false });
			window.AddControl(new ButtonControl { Name = "third", Text = "Three" });
			window.RenderAndGetVisibleContent();

			var reachable = window.FocusManager.ReachableControls()
				.Select(c => (c as IWindowControl)?.Name).ToList();

			Assert.DoesNotContain("off", reachable);
			Assert.Contains("first", reachable);
			Assert.Contains("third", reachable);
		}

		[Fact]
		public void TheWalkTerminatesOnAWindowWithNoFocusableControls()
		{
			// A window of labels has nowhere for focus to go, and asking must not
			// spin looking for a stop that does not exist.
			var (_, window) = Build();
			window.AddControl(new MarkupControl(new List<string> { "just text" }));
			window.RenderAndGetVisibleContent();

			Assert.Empty(window.FocusManager.ReachableControls());
		}

		[Fact]
		public void AskingDoesNotMoveTheRealFocus()
		{
			// The walk drives the real focus machinery, so it must put it back:
			// a probe that answered the question by changing the answer would be
			// worse than no probe.
			var (_, window) = Build();

			window.AddControl(new ButtonControl { Name = "first", Text = "One" });
			var second = new ButtonControl { Name = "second", Text = "Two" };
			window.AddControl(second);
			window.AddControl(new ButtonControl { Name = "third", Text = "Three" });
			window.RenderAndGetVisibleContent();

			// Deliberately NOT the first stop: the walk ends on the first control,
			// so parking focus there would pass whether or not it was restored.
			window.FocusManager.SetFocus(second, FocusReason.Programmatic);

			window.FocusManager.ReachableControls();

			Assert.Same(second, window.FocusManager.FocusedControl);
		}

		[Fact]
		public void ControlsInsideAPanelAreStillReachable()
		{
			// The real nesting: a scope inside the window, which is where the
			// scope rules actually bite.
			var (_, window) = Build();

			var panel = new ScrollablePanelControl { Name = "panel" };
			panel.AddControl(new ButtonControl { Name = "inner", Text = "In" });
			window.AddControl(new ButtonControl { Name = "outer", Text = "Out" });
			window.AddControl(panel);
			window.RenderAndGetVisibleContent();

			var reachable = window.FocusManager.ReachableControls()
				.Select(c => (c as IWindowControl)?.Name).ToList();

			Assert.Contains("outer", reachable);
			Assert.Contains("inner", reachable);
		}
	}
}
