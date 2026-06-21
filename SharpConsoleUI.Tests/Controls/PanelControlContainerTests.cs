// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class PanelControlContainerTests
{
	[Fact]
	public void PanelControl_IsA_CollapsiblePanel()
	{
		var p = new PanelControl();
		Assert.IsAssignableFrom<CollapsiblePanel>(p);
	}

	[Fact]
	public void PanelControl_IsNonCollapsible_AndCannotBeToggled()
	{
		var p = new PanelControl();
		Assert.False(p.Collapsible);
		Assert.True(p.IsExpanded);
		p.Toggle();
		Assert.True(p.IsExpanded);
		p.Collapsible = true;            // no-op
		Assert.False(p.Collapsible);
	}

	[Fact]
	public void Content_AddsASingleMarkupChild()
	{
		var p = new PanelControl();
		p.Content = "hello";
		Assert.Single(p.Children);
		Assert.IsType<MarkupControl>(p.Children[0]);
		Assert.Equal("hello", ((MarkupControl)p.Children[0]).Text);
	}

	[Fact]
	public void SetContent_ReplacesAllContent()
	{
		var p = new PanelControl();
		p.AddControl(new MarkupControl(new System.Collections.Generic.List<string> { "other" }));
		p.SetContent("only");
		Assert.Single(p.Children);
		Assert.Equal("only", ((MarkupControl)p.Children[0]).Text);
	}

	[Fact]
	public void Content_Getter_ReturnsContentText_OrNull()
	{
		var p = new PanelControl();
		Assert.Null(p.Content);
		p.Content = "x";
		Assert.Equal("x", p.Content);
	}

	[Fact]
	public void BackgroundColor_NullableContract_Preserved()
	{
		var p = new PanelControl();
		SharpConsoleUI.Color? bg = p.BackgroundColor;     // must compile as Color?
		Assert.Null(bg);
		var fallback = SharpConsoleUI.Color.Blue;
		var resolved = p.BackgroundColor ?? fallback;       // `??` must compile
		Assert.Equal(fallback, resolved);
		p.BackgroundColor = SharpConsoleUI.Color.Red;
		Assert.Equal(SharpConsoleUI.Color.Red, p.BackgroundColor);
	}

	[Fact]
	public void ForegroundColor_NullReset_DoesNotLeaveStaleBase()
	{
		var p = new PanelControl();
		p.ForegroundColor = SharpConsoleUI.Color.Red;
		p.ForegroundColor = null;
		Assert.Null(p.ForegroundColor);
		// base must not retain Red: a null shadow means "inherit/theme", not the prior explicit color.
		Assert.NotEqual(SharpConsoleUI.Color.Red, ((CollapsiblePanel)p).ForegroundColor);
	}

	[Fact]
	public void ForegroundColor_NullableContract_Preserved()
	{
		var p = new PanelControl();
		SharpConsoleUI.Color? fg = p.ForegroundColor;
		Assert.Null(fg);
		var fallback = SharpConsoleUI.Color.Blue;
		Assert.Equal(fallback, p.ForegroundColor ?? fallback);
		p.ForegroundColor = SharpConsoleUI.Color.Green;
		Assert.Equal(SharpConsoleUI.Color.Green, p.ForegroundColor);
	}

	[Fact]
	public void Header_And_Title_StayInSync_BothDirections()
	{
		var p = new PanelControl();
		p.Header = "H1";
		Assert.Equal("H1", ((CollapsiblePanel)p).Title);
		((CollapsiblePanel)p).Title = "H2";   // write via base
		Assert.Equal("H2", p.Header);          // must reflect (no desync)
	}

	[Fact]
	public void Builder_Build_ReturnsPanelControl_WithBorderlessContentChild()
	{
		PanelControl p = SharpConsoleUI.Builders.Controls.Panel()
			.WithContent("body")
			.Rounded()
			.WithHeader("Title")
			.Build();
		Assert.IsType<PanelControl>(p);                 // Build() return type stays PanelControl
		Assert.Equal("body", p.Content);
		Assert.Equal("Title", p.Header);
		Assert.Single(p.Children);
		Assert.IsType<MarkupControl>(p.Children[0]);
		Assert.Equal(BorderStyle.None, ((MarkupControl)p.Children[0]).Border); // child borderless; PANEL draws border
	}

	[Fact]
	public void Builder_AddControl_HostsExtraChildAfterContent()
	{
		var extra = new MarkupControl(new System.Collections.Generic.List<string> { "extra" });
		PanelControl p = SharpConsoleUI.Builders.Controls.Panel()
			.WithContent("body")
			.AddControl(extra)
			.Build();
		Assert.Equal(2, p.Children.Count);
		Assert.Equal("body", ((MarkupControl)p.Children[0]).Text); // content first
		Assert.Same(extra, p.Children[1]);                          // extra after
	}
}
