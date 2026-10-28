// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Activating a list item with one click.
/// </summary>
/// <remarks>
/// A list activates on a double click, which is right for a file list and
/// wrong for a menu: nobody double clicks a start menu. A single click only
/// ever set SelectedIndex, so a menu built from a list - which is what the
/// start menu is - could not be driven the way every other menu is.
/// </remarks>
public class ListSingleClickActivateTests
{
	private sealed class Fixture
	{
		public ListControl List { get; }
		public int Activations;

		public Fixture(bool singleClick)
		{
			var driver = new HeadlessConsoleDriver(60, 20);
			var system = new ConsoleWindowSystem(driver);

			List = new ListControl { ActivateOnSingleClick = singleClick };
			List.AddItem("one");
			List.AddItem("two");
			List.ItemActivated += (_, _) => Activations++;

			var window = new Window(system) { Width = 40, Height = 10, Top = 0, Left = 0 };
			window.AddControl(List);
			system.WindowStateService.AddWindow(window);
			system.Render.UpdateDisplay();
		}

		public bool Send(params MouseFlags[] flags)
		{
			// Row 0 of the list's own content.
			var at = new Point(List.ActualX + 1, List.ActualY);
			var args = new MouseEventArgs([.. flags], at, at, at);
			return ((IMouseAwareControl)List).ProcessMouseEvent(args);
		}
	}

	[Fact]
	public void By_default_a_single_click_only_selects()
	{
		var f = new Fixture(singleClick: false);

		f.Send(MouseFlags.Button1Clicked);

		Assert.Equal(0, f.Activations);
	}

	[Fact]
	public void With_the_flag_on_a_single_click_activates()
	{
		var f = new Fixture(singleClick: true);

		f.Send(MouseFlags.Button1Clicked);

		Assert.Equal(1, f.Activations);
	}

	[Fact]
	public void A_double_click_activates_once_not_twice()
	{
		// The recognizer reports the click on the release AND the double on the
		// next press, so a careless implementation fires once per flag. That is
		// issue #92 wearing a different control.
		var f = new Fixture(singleClick: true);

		// The FULL sequence a real double click delivers, read off a running
		// application's event tape. The second release carries Button1Clicked too,
		// which is the half a shorter test misses - and the half that activated
		// a second time.
		f.Send(MouseFlags.Button1Pressed);
		f.Send(MouseFlags.Button1Released, MouseFlags.Button1Clicked);
		f.Send(MouseFlags.Button1Pressed, MouseFlags.Button1DoubleClicked);
		f.Send(MouseFlags.Button1Released, MouseFlags.Button1Clicked);

		Assert.Equal(1, f.Activations);
	}

	[Fact]
	public void The_double_click_path_still_works_when_single_click_is_off()
	{
		var f = new Fixture(singleClick: false);

		f.Send(MouseFlags.Button1Pressed, MouseFlags.Button1DoubleClicked);

		Assert.Equal(1, f.Activations);
	}
}
