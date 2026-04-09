// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Html;

public class HtmlControlMouseTests
{
    [Fact]
    public void LinkClick_FiresEvent()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p><a href=\"https://example.com\">Click Me</a></p>")
            .Build();
        window.AddControl(html);

        // Render to trigger layout
        window.RenderAndGetVisibleContent();

        string? clickedUrl = null;
        html.LinkClicked += (s, e) => clickedUrl = e.Url;

        // The link text "Click Me" should start near x=0, y=0 in control-relative coords.
        // We need to account for the window's content area position.
        // In a window with no title/borders, content starts at (0,0).
        var click = ContainerTestHelpers.CreateClick(2, 0);
        html.ProcessMouseEvent(click);

        Assert.Equal("https://example.com", clickedUrl);
    }

    [Fact]
    public void ClickOffLink_DoesNotFireLinkEvent()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p><a href=\"https://example.com\">Link</a></p>")
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        string? clickedUrl = null;
        html.LinkClicked += (s, e) => clickedUrl = e.Url;

        // Click at a position far from the link text
        var click = ContainerTestHelpers.CreateClick(80, 10);
        html.ProcessMouseEvent(click);

        Assert.Null(clickedUrl);
    }

    [Fact]
    public void MouseWheel_Scrolls()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var longContent = string.Join("", Enumerable.Range(1, 100).Select(i => $"<p>Line {i}</p>"));
        var html = HtmlBuilder.Create()
            .WithContent(longContent)
            .WithHeight(10)
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        Assert.Equal(0, html.ScrollOffset);

        var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 5);
        bool handled = html.ProcessMouseEvent(wheelDown);

        Assert.True(handled);
        Assert.True(html.ScrollOffset > 0);
    }

    [Fact]
    public void MouseWheel_AtBottom_Bubbles()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var longContent = string.Join("", Enumerable.Range(1, 50).Select(i => $"<p>Line {i}</p>"));
        var html = HtmlBuilder.Create()
            .WithContent(longContent)
            .WithHeight(10)
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        // Scroll to the very bottom
        html.ScrollOffset = int.MaxValue;
        int bottomOffset = html.ScrollOffset;

        var wheelDown = ContainerTestHelpers.CreateWheelDown(5, 5);
        bool handled = html.ProcessMouseEvent(wheelDown);

        // Should return false since we're already at the bottom
        Assert.False(handled);
        Assert.Equal(bottomOffset, html.ScrollOffset);
    }

    [Fact]
    public void MouseWheel_AtTop_Bubbles()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var longContent = string.Join("", Enumerable.Range(1, 50).Select(i => $"<p>Line {i}</p>"));
        var html = HtmlBuilder.Create()
            .WithContent(longContent)
            .WithHeight(10)
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        Assert.Equal(0, html.ScrollOffset);

        var wheelUp = ContainerTestHelpers.CreateWheelUp(5, 5);
        bool handled = html.ProcessMouseEvent(wheelUp);

        // Should return false since we're already at the top
        Assert.False(handled);
    }
}
