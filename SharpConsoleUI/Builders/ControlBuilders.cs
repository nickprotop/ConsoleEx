// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using Spectre.Console;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for button controls
/// </summary>
public sealed class ButtonBuilder
{
    private string _text = "Button";
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _enabled = true;
    private bool _visible = true;
    private int? _width;
    private object? _tag;
    private EventHandler<ButtonControl>? _clickHandler;

    /// <summary>
    /// Sets the button text
    /// </summary>
    /// <param name="text">The button text</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithText(string text)
    {
        _text = text ?? "Button";
        return this;
    }

    /// <summary>
    /// Sets the button alignment
    /// </summary>
    /// <param name="alignment">The alignment</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Centers the button
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Centered()
    {
        _alignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>
    /// Sets the button margin
    /// </summary>
    /// <param name="left">Left margin</param>
    /// <param name="top">Top margin</param>
    /// <param name="right">Right margin</param>
    /// <param name="bottom">Bottom margin</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    /// <param name="margin">The margin value for all sides</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the enabled state
    /// </summary>
    /// <param name="enabled">Whether the button is enabled</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Enabled(bool enabled = true)
    {
        _enabled = enabled;
        return this;
    }

    /// <summary>
    /// Disables the button
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Disabled()
    {
        _enabled = false;
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    /// <param name="visible">Whether the button is visible</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the button width
    /// </summary>
    /// <param name="width">The button width</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    /// <param name="tag">The tag object</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the click event handler
    /// </summary>
    /// <param name="handler">The click handler</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder OnClick(EventHandler<ButtonControl> handler)
    {
        _clickHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the click event handler with action
    /// </summary>
    /// <param name="action">The click action</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder OnClick(Action action)
    {
        _clickHandler = (_, _) => action();
        return this;
    }

    /// <summary>
    /// Builds the button control
    /// </summary>
    /// <returns>The configured button control</returns>
    public ButtonControl Build()
    {
        var button = new ButtonControl
        {
            Text = _text,
            HorizontalAlignment = _alignment,
            Margin = _margin,
            IsEnabled = _enabled,
            Visible = _visible,
            Width = _width,
            Tag = _tag
        };

        if (_clickHandler != null)
        {
            button.Click += _clickHandler;
        }

        return button;
    }

    /// <summary>
    /// Implicit conversion to ButtonControl
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <returns>The built button control</returns>
    public static implicit operator ButtonControl(ButtonBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for markup controls
/// </summary>
public sealed class MarkupBuilder
{
    private readonly List<string> _lines = new();
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private object? _tag;

    /// <summary>
    /// Adds a line of markup text
    /// </summary>
    /// <param name="markup">The markup text</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddLine(string markup)
    {
        _lines.Add(markup ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Adds multiple lines of markup text
    /// </summary>
    /// <param name="markupLines">The markup lines</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddLines(params string[] markupLines)
    {
        foreach (var line in markupLines)
        {
            AddLine(line);
        }
        return this;
    }

    /// <summary>
    /// Adds an empty line
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddEmptyLine()
    {
        _lines.Add(string.Empty);
        return this;
    }

    /// <summary>
    /// Clears all lines
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Clear()
    {
        _lines.Clear();
        return this;
    }

    /// <summary>
    /// Sets the alignment
    /// </summary>
    /// <param name="alignment">The alignment</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Centers the content
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Centered()
    {
        _alignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    /// <param name="left">Left margin</param>
    /// <param name="top">Top margin</param>
    /// <param name="right">Right margin</param>
    /// <param name="bottom">Bottom margin</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    /// <param name="margin">The margin value for all sides</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    /// <param name="visible">Whether the control is visible</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    /// <param name="width">The control width</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    /// <param name="tag">The tag object</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Builds the markup control
    /// </summary>
    /// <returns>The configured markup control</returns>
    public MarkupControl Build()
    {
        var markup = new MarkupControl(_lines.ToList())
        {
            HorizontalAlignment = _alignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Tag = _tag
        };

        return markup;
    }

    /// <summary>
    /// Implicit conversion to MarkupControl
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <returns>The built markup control</returns>
    public static implicit operator MarkupControl(MarkupBuilder builder) => builder.Build();
}

/// <summary>
/// Static factory class for creating control builders
/// </summary>
public static class Controls
{
    /// <summary>
    /// Creates a new button builder
    /// </summary>
    /// <param name="text">The initial button text</param>
    /// <returns>A new button builder</returns>
    public static ButtonBuilder Button(string text = "Button") => new ButtonBuilder().WithText(text);

    /// <summary>
    /// Creates a new markup builder
    /// </summary>
    /// <param name="initialLine">The initial line of markup</param>
    /// <returns>A new markup builder</returns>
    public static MarkupBuilder Markup(string? initialLine = null)
    {
        var builder = new MarkupBuilder();
        if (initialLine != null)
        {
            builder.AddLine(initialLine);
        }
        return builder;
    }

    /// <summary>
    /// Creates a new rule control
    /// </summary>
    /// <param name="title">The rule title</param>
    /// <returns>A configured rule control</returns>
    public static RuleControl Rule(string? title = null)
    {
        var rule = new RuleControl();
        if (title != null)
        {
            rule.Title = title;
        }
        return rule;
    }

    /// <summary>
    /// Creates a horizontal separator rule
    /// </summary>
    /// <returns>A configured rule control</returns>
    public static RuleControl Separator() => new RuleControl { Title = string.Empty };

    /// <summary>
    /// Creates a text label (markup without formatting)
    /// </summary>
    /// <param name="text">The label text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Label(string text) => new MarkupControl(new List<string> { text });

    /// <summary>
    /// Creates a header text (bold and colored)
    /// </summary>
    /// <param name="text">The header text</param>
    /// <param name="color">The header color (default: yellow)</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Header(string text, string color = "yellow") =>
        new MarkupControl(new List<string> { $"[bold {color}]{text}[/]" });

    /// <summary>
    /// Creates an info message (blue color)
    /// </summary>
    /// <param name="text">The info text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Info(string text) =>
        new MarkupControl(new List<string> { $"[blue]{text}[/]" });

    /// <summary>
    /// Creates a warning message (orange color)
    /// </summary>
    /// <param name="text">The warning text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Warning(string text) =>
        new MarkupControl(new List<string> { $"[orange3]{text}[/]" });

    /// <summary>
    /// Creates an error message (red color)
    /// </summary>
    /// <param name="text">The error text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Error(string text) =>
        new MarkupControl(new List<string> { $"[red]{text}[/]" });

    /// <summary>
    /// Creates a success message (green color)
    /// </summary>
    /// <param name="text">The success text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Success(string text) =>
        new MarkupControl(new List<string> { $"[green]{text}[/]" });
}