// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;

namespace SharpConsoleUI.Builders;

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

	/// <summary>
	/// Creates a vertical separator control
	/// </summary>
	/// <returns>A configured separator control</returns>
	public static SeparatorControl VerticalSeparator() => new SeparatorControl();

	/// <summary>
	/// Creates a vertical separator control with horizontal margin
	/// </summary>
	/// <param name="horizontalMargin">The margin on left and right sides</param>
	/// <returns>A configured separator control</returns>
	public static SeparatorControl VerticalSeparator(int horizontalMargin) =>
		new SeparatorControl { Margin = new Margin(horizontalMargin, 0, horizontalMargin, 0) };

	/// <summary>
	/// Creates a new toolbar builder
	/// </summary>
	/// <returns>A new toolbar builder</returns>
	public static ToolbarBuilder Toolbar() => new ToolbarBuilder();

	/// <summary>
	/// Creates a new list builder
	/// </summary>
	/// <param name="title">The initial list title</param>
	/// <returns>A new list builder</returns>
	public static ListBuilder List(string? title = null)
	{
		var builder = new ListBuilder();
		if (title != null)
			builder.WithTitle(title);
		return builder;
	}

	/// <summary>
	/// Creates a new checkbox builder
	/// </summary>
	/// <param name="label">The checkbox label</param>
	/// <returns>A new checkbox builder</returns>
	public static CheckboxBuilder Checkbox(string label) => new CheckboxBuilder().WithLabel(label);

	/// <summary>
	/// Creates a new dropdown builder
	/// </summary>
	/// <param name="prompt">The dropdown prompt text</param>
	/// <returns>A new dropdown builder</returns>
	public static DropdownBuilder Dropdown(string? prompt = null)
	{
		var builder = new DropdownBuilder();
		if (prompt != null)
			builder.WithPrompt(prompt);
		return builder;
	}

	/// <summary>
	/// Creates a new prompt builder
	/// </summary>
	/// <param name="prompt">The prompt text</param>
	/// <returns>A new prompt builder</returns>
	public static PromptBuilder Prompt(string prompt = "> ") => new PromptBuilder().WithPrompt(prompt);

	/// <summary>
	/// Creates a new tree control builder
	/// </summary>
	/// <returns>A new tree control builder</returns>
	public static TreeControlBuilder Tree() => new TreeControlBuilder();

	/// <summary>
	/// Creates a new multiline edit control builder
	/// </summary>
	/// <param name="content">Optional initial content</param>
	/// <returns>A new multiline edit control builder</returns>
	public static MultilineEditControlBuilder MultilineEdit(string? content = null)
	{
		var builder = new MultilineEditControlBuilder();
		if (content != null)
			builder.WithContent(content);
		return builder;
	}

	/// <summary>
	/// Creates a new Figlet text control builder
	/// </summary>
	/// <param name="text">The FIGlet ASCII art text</param>
	/// <returns>A new Figlet control builder</returns>
	public static FigleControlBuilder Figlet(string? text = null)
	{
		var builder = new FigleControlBuilder();
		if (text != null)
			builder.WithText(text);
		return builder;
	}

	/// <summary>
	/// Creates a new menu builder
	/// </summary>
	/// <returns>A new menu builder</returns>
	public static MenuBuilder Menu() => new MenuBuilder();

	/// <summary>
	/// Creates a new horizontal grid builder
	/// </summary>
	/// <returns>A new horizontal grid builder</returns>
	public static HorizontalGridBuilder HorizontalGrid() => new HorizontalGridBuilder();

	/// <summary>
	/// Creates a new scrollable panel builder
	/// </summary>
	/// <returns>A new scrollable panel builder</returns>
	public static ScrollablePanelBuilder ScrollablePanel() => new ScrollablePanelBuilder();

	/// <summary>
	/// Creates a new rule builder for horizontal separator lines
	/// </summary>
	/// <returns>A new rule builder</returns>
	public static RuleBuilder RuleBuilder() => new RuleBuilder();

	/// <summary>
	/// Creates a new sparkline graph builder
	/// </summary>
	/// <returns>A new sparkline builder</returns>
	public static SparklineBuilder Sparkline() => new SparklineBuilder();

	/// <summary>
	/// Creates a new bar graph builder
	/// </summary>
	/// <returns>A new bar graph builder</returns>
	public static BarGraphBuilder BarGraph() => new BarGraphBuilder();

	/// <summary>
	/// Creates a new progress bar builder
	/// </summary>
	/// <returns>A new progress bar builder</returns>
	public static ProgressBarBuilder ProgressBar() => new ProgressBarBuilder();

	/// <summary>
	/// Creates a new panel builder for bordered content panels
	/// </summary>
	/// <returns>A new panel builder</returns>
	public static PanelBuilder Panel() => new PanelBuilder();

	/// <summary>
	/// Creates a panel control with text content
	/// </summary>
	/// <param name="content">The text content (supports Spectre markup)</param>
	/// <returns>A configured panel control</returns>
	public static PanelControl Panel(string content) => new PanelControl(content);
}
