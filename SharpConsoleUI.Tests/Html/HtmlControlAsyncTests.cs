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

public class HtmlControlAsyncTests
{
    [Fact]
    public void SetContent_IsNotLoading()
    {
        var html = new HtmlControl();
        html.SetContent("<p>Hello</p>");
        Assert.False(html.IsLoading);
    }

    [Fact]
    public void SetContent_SetsRawHtml()
    {
        var html = new HtmlControl();
        html.SetContent("<p>Test content</p>");
        Assert.Equal("<p>Test content</p>", html.RawHtml);
    }

    [Fact]
    public void SetContent_WithBaseUrl_SetsContent()
    {
        var html = new HtmlControl();
        html.SetContent("<p>Content</p>", "https://example.com");
        Assert.Equal("<p>Content</p>", html.RawHtml);
        Assert.False(html.IsLoading);
    }

    [Fact]
    public void ContentHeight_MatchesLayout()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>Line one</p><p>Line two</p><p>Line three</p>")
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        Assert.True(html.ContentHeight > 0);
    }

    [Fact]
    public void ScrollOffset_ClampedToRange()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>Short content</p>")
            .WithHeight(5)
            .Build();
        window.AddControl(html);
        window.RenderAndGetVisibleContent();

        // Try to set scroll offset beyond content
        html.ScrollOffset = 9999;
        Assert.True(html.ScrollOffset >= 0);
        // For short content that fits in viewport, max scroll should be 0
        int maxExpected = Math.Max(0, html.ContentHeight - 5);
        Assert.True(html.ScrollOffset <= maxExpected);

        // Negative should clamp to 0
        html.ScrollOffset = -10;
        Assert.Equal(0, html.ScrollOffset);
    }

    [Fact]
    public async Task LoadUrlAsync_InvalidUrl_FiresError()
    {
        var html = new HtmlControl();
        string? errorUrl = null;
        Exception? errorEx = null;

        html.LoadError += (s, e) =>
        {
            errorUrl = e.Url;
            errorEx = e.Error;
        };

        await html.LoadUrlAsync("not-a-valid-url://invalid");

        Assert.Equal("not-a-valid-url://invalid", errorUrl);
        Assert.NotNull(errorEx);
        Assert.False(html.IsLoading);
    }
}
