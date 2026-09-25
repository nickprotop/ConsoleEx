// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

/// <summary>
/// Regression tests for issue #81: GotFocus / LostFocus depended on a subscription made when a
/// control's Container was set. With the fluent builders a control joins its container BEFORE the
/// container joins a window, so at subscription time there was no window and no subscription was
/// made. Containers that re-set their children's Container (GridControl, TabControl) happened to
/// work; ScrollablePanel, Panel, CollapsiblePanel and HorizontalGrid columns did not.
/// </summary>
public class FocusEventsAcrossContainersTests
{
	/// <summary>
	/// Builds bottom-up exactly as the fluent builders do: the button joins its host, and only then
	/// does the host join the window.
	/// </summary>
	private static (ButtonControl button, Window window, int[] counters) Build(
		Func<ButtonControl, IWindowControl> host)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system) { Width = 40, Height = 10 };

		var button = SharpConsoleUI.Builders.Controls.Button("Nap").Build();
		var counters = new int[2]; // [0] = GotFocus, [1] = LostFocus
		button.GotFocus += (_, _) => counters[0]++;
		button.LostFocus += (_, _) => counters[1]++;

		window.AddControl(host(button));
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		return (button, window, counters);
	}

	public static TheoryData<string> Hosts => new()
	{
		"window", "grid", "tab", "scrollablepanel", "panel", "collapsiblepanel", "horizontalgrid",
	};

	private static IWindowControl MakeHost(string kind, ButtonControl button)
	{
		switch (kind)
		{
			case "window":
				return button;

			case "grid":
				{
					var grid = new GridControl();
					grid.ColumnDefinitions.Add(GridLength.Star());
					grid.RowDefinitions.Add(GridLength.Star());
					grid.Place(button, 0, 0);
					return grid;
				}

			case "tab":
				{
					var tabs = new TabControl();
					tabs.AddTab("One", button);
					return tabs;
				}

			case "scrollablepanel":
				return SharpConsoleUI.Builders.Controls.ScrollablePanel().AddControl(button).Build();

			case "panel":
				return SharpConsoleUI.Builders.Controls.Panel().AddControl(button).Build();

			case "collapsiblepanel":
				{
					var panel = new CollapsiblePanel { Title = "One" };
					panel.AddControl(button);
					return panel;
				}

			case "horizontalgrid":
				return SharpConsoleUI.Builders.Controls.HorizontalGrid()
					.Column(column => column.Add(button))
					.Build();

			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	[Theory]
	[MemberData(nameof(Hosts))]
	public void GotFocus_FiresRegardlessOfHost(string kind)
	{
		var (button, window, counters) = Build(b => MakeHost(kind, b));

		window.FocusControl(button);

		Assert.Same(button, window.FocusManager.FocusedControl);
		Assert.Equal(1, counters[0]);
	}

	[Theory]
	[MemberData(nameof(Hosts))]
	public void LostFocus_FiresRegardlessOfHost(string kind)
	{
		var (button, window, counters) = Build(b => MakeHost(kind, b));
		window.FocusControl(button);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		Assert.Equal(1, counters[1]);
	}

	[Theory]
	[MemberData(nameof(Hosts))]
	public void GotFocus_FiresOncePerFocusChange(string kind)
	{
		// Guards against the double-firing that a "subscribe in every container" fix could introduce
		// if a control ends up subscribed through more than one path.
		var (button, window, counters) = Build(b => MakeHost(kind, b));

		window.FocusControl(button);
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);
		window.FocusControl(button);

		Assert.Equal(2, counters[0]);
		Assert.Equal(1, counters[1]);
	}
}
