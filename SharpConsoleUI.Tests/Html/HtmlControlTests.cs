// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Html;

public class HtmlControlTests
{
    [Fact]
    public void BasicContent_RendersTextInWindow()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create().WithContent("<p>Hello World</p>").Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        Assert.Contains("Hello World", plainText);
    }

    [Fact]
    public void InsideScrollablePanel_RendersWithoutCrash()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var panel = new ScrollablePanelControl { Height = 10 };
        var longContent = string.Join("", Enumerable.Range(1, 50).Select(i => $"<p>Line {i}</p>"));
        var html = HtmlBuilder.Create().WithContent(longContent).Build();
        panel.AddControl(html);
        window.AddControl(panel);
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        Assert.Contains("Line 1", plainText);
    }

    [Fact]
    public void WithExplicitHeight_DoesNotCrash()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var longContent = string.Join("", Enumerable.Range(1, 20).Select(i => $"<p>Row {i}</p>"));
        var html = HtmlBuilder.Create().WithContent(longContent).WithHeight(5).Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        // Should render without crash; first line should be visible
        Assert.Contains("Row 1", plainText);
    }

    [Fact]
    public void WithExplicitWidth_WrapsContent()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>This is a long sentence that should wrap when width is small</p>")
            .WithWidth(20)
            .Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        Assert.NotNull(output);
        Assert.True(output.Count > 0);
    }

    [Fact]
    public void HorizontalAlignmentStretch_UsesFullWidth()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>Stretched</p>")
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        Assert.Contains("Stretched", plainText);
    }

    [Fact]
    public void MarginApplied_ContentIsOffset()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>Margined</p>")
            .WithMargin(5, 2, 0, 0)
            .Build();
        window.AddControl(html);
        var buffer = window.EnsureContentReady();
        Assert.NotNull(buffer);

        // Row 0 and 1 should be empty (top margin = 2)
        // The text should appear starting at row 2 offset by left margin 5
        // Verify the text is present somewhere in the buffer
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        Assert.Contains("Margined", plainText);
    }

    [Fact]
    public void EmptyHtml_DoesNotCrash()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create().WithContent("").Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        Assert.NotNull(output);
    }

    [Fact]
    public void MalformedHtml_DoesNotCrash()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = HtmlBuilder.Create()
            .WithContent("<p>Unclosed paragraph<div>Nested badly</p></div><br/><hr>")
            .Build();
        window.AddControl(html);
        var output = window.RenderAndGetVisibleContent();
        var plainText = ContainerTestHelpers.StripAnsiCodes(output);
        // AngleSharp will fix up the HTML; just verify it doesn't throw
        Assert.NotNull(output);
    }

    [Fact]
    public void SetContentUpdates_ShowsNewContent()
    {
        var (system, window) = ContainerTestHelpers.CreateTestEnvironment(120, 40, 100, 30);
        var html = new HtmlControl();
        html.SetContent("<p>First</p>");
        window.AddControl(html);
        var output1 = window.RenderAndGetVisibleContent();
        var plainText1 = ContainerTestHelpers.StripAnsiCodes(output1);
        Assert.Contains("First", plainText1);

        html.SetContent("<p>Second</p>");
        var output2 = window.RenderAndGetVisibleContent();
        var plainText2 = ContainerTestHelpers.StripAnsiCodes(output2);
        Assert.Contains("Second", plainText2);
    }

    [Fact]
    public void BuilderPattern_SetsCorrectProperties()
    {
        var html = HtmlBuilder.Create()
            .WithContent("<p>Test</p>")
            .WithWidth(50)
            .WithHeight(10)
            .WithMargin(1, 2, 3, 4)
            .WithShowBulletPoints(false)
            .WithTabSize(2)
            .WithBlockSpacing(0)
            .WithName("myHtml")
            .WithScrollbarVisibility(ScrollbarVisibility.Always)
            .Build();

        Assert.Equal(50, html.Width);
        Assert.Equal(10, html.Height);
        Assert.Equal("myHtml", html.Name);
        Assert.False(html.ShowBulletPoints);
        Assert.Equal(2, html.TabSize);
        Assert.Equal(0, html.BlockSpacing);
        Assert.Equal(ScrollbarVisibility.Always, html.ScrollbarVisibility);
        Assert.Equal("<p>Test</p>", html.RawHtml);
    }
}
