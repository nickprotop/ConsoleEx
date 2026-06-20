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
}
