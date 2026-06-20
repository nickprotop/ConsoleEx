// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class PanelControlFacadeTests
{
	private static string Render(PanelControl p, int w = 30, int h = 12) =>
		ContainerTestHelpers.StripAnsiCodes(ContainerTestHelpers.RenderToLines(p, w, h));

	[Fact]
	public void AddControl_HostsChildAlongsideContent()
	{
		var p = new PanelControl("HEADTEXT") { BorderStyle = BorderStyle.None, Width = 24 };
		p.AddControl(new MarkupControl(new List<string> { "CHILDROW" }));
		var text = Render(p);
		Assert.Contains("HEADTEXT", text);
		Assert.Contains("CHILDROW", text);
	}

	[Fact]
	public void Children_IncludesAddedChild_AndManagedContentChild()
	{
		var p = new PanelControl("hdr") { BorderStyle = BorderStyle.None };
		var child = new MarkupControl(new List<string> { "c" });
		p.AddControl(child);
		Assert.Contains(child, p.Children);
		Assert.Equal(2, p.Children.Count);
	}

	[Fact]
	public void Children_ContentNull_OnlyUserChild()
	{
		var p = new PanelControl { BorderStyle = BorderStyle.None };
		var child = new MarkupControl(new List<string> { "c" });
		p.AddControl(child);
		Assert.Single(p.Children);
		Assert.Contains(child, p.Children);
	}

	[Fact]
	public void ContentNull_NoMarkupChild_PureContainer()
	{
		var p = new PanelControl { BorderStyle = BorderStyle.None, Width = 24 };
		p.AddControl(new MarkupControl(new List<string> { "ONLYCHILD" }));
		var lines = ContainerTestHelpers.RenderToLines(p, 30, 8);
		int first = lines.FindIndex(l => l.Trim().Length > 0);
		Assert.True(first >= 0 && lines[first].Contains("ONLYCHILD"));
	}

	[Fact]
	public void BodyButton_ReceivesEnter_FiresClick()
	{
		bool clicked = false;
		var button = new ButtonControl { Text = "Go" };
		button.Click += (_, _) => clicked = true;
		var p = new PanelControl { BorderStyle = BorderStyle.None };
		p.AddControl(button);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(p);
		window.RenderAndGetVisibleContent();
		window.FocusManager.SetFocus((IFocusableControl)button, FocusReason.Programmatic);

		bool handled = button.ProcessKey(new System.ConsoleKeyInfo('\r', System.ConsoleKey.Enter, false, false, false));

		Assert.True(handled);
		Assert.True(clicked);
	}

	[Fact]
	public void NestedContainer_RendersItsChildren()
	{
		var inner = new ColumnContainer(new HorizontalGridControl()) { Width = 20 };
		inner.AddContent(new MarkupControl(new List<string> { "NESTED" }));
		var p = new PanelControl { BorderStyle = BorderStyle.None, Width = 24 };
		p.AddControl(inner);
		var text = Render(p);
		Assert.Contains("NESTED", text);
	}

	[Fact]
	public void ContentOnlyPanel_BodyClick_RaisesMouseClick_FullDispatch()
	{
		// The Examples/PanelDemo contract: a content-only panel's body click raises PanelControl.MouseClick.
		var system = SharpConsoleUI.Tests.Infrastructure.TestWindowSystemBuilder.CreateTestSystem(30, 12);
		var window = new SharpConsoleUI.Window(system) { Left = 0, Top = 0, Width = 30, Height = 12 };
		var p = new PanelControl("Click me!") { BorderStyle = BorderStyle.Rounded, Width = 20, Height = 6 };
		window.AddControl(p);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		bool clicked = false;
		p.MouseClick += (_, _) => clicked = true;

		int clickX = window.Left + 1 + 3;
		int clickY = window.Top + 1 + 2;
		var driver = (SharpConsoleUI.Tests.Infrastructure.MockConsoleDriver)system.ConsoleDriver;
		driver.SimulateMouseEvent(new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked }, new System.Drawing.Point(clickX, clickY));
		system.Input.ProcessInput();

		Assert.True(clicked, "content-only panel body click must raise PanelControl.MouseClick (PanelDemo contract)");
	}
}
