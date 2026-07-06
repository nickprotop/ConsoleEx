// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Runtime.Versioning;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

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
	/// Creates a new markup builder seeded with a Markdown block (rendered via the [markdown] tag).
	/// </summary>
	/// <param name="markdown">The initial Markdown content.</param>
	/// <returns>A new markup builder.</returns>
	public static MarkupBuilder Markdown(string? markdown = null)
	{
		var builder = new MarkupBuilder();
		if (markdown != null)
		{
			builder.AddMarkdown(markdown);
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
	/// Creates a new grid-backed horizontal grid builder. Produces a <see cref="GridBackedHGrid"/> — a
	/// grid-backed, drop-in equivalent of <see cref="HorizontalGrid"/>'s <see cref="HorizontalGridControl"/>.
	/// </summary>
	/// <returns>A new grid-backed horizontal grid builder</returns>
	public static GridBackedHGridBuilder GridBackedHGrid() => new GridBackedHGridBuilder();

	/// <summary>
	/// Creates a new grid builder for two-dimensional row/column layouts.
	/// </summary>
	/// <returns>A new grid builder</returns>
	public static GridBuilder Grid() => new GridBuilder();

	/// <summary>
	/// Creates a new scrollable panel builder
	/// </summary>
	/// <returns>A new scrollable panel builder</returns>
	public static ScrollablePanelBuilder ScrollablePanel() => new ScrollablePanelBuilder();

	/// <summary>
	/// Creates a new tab control builder
	/// </summary>
	/// <returns>A new tab control builder</returns>
	public static TabControlBuilder TabControl() => new TabControlBuilder();

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
	/// Creates a new line graph builder
	/// </summary>
	/// <returns>A new line graph builder</returns>
	public static LineGraphBuilder LineGraph() => new LineGraphBuilder();

	/// <summary>
	/// Creates a new progress bar builder
	/// </summary>
	/// <returns>A new progress bar builder</returns>
	public static ProgressBarBuilder ProgressBar() => new ProgressBarBuilder();

	/// <summary>
	/// Creates a new spinner builder
	/// </summary>
	/// <returns>A new spinner builder</returns>
	public static SpinnerBuilder Spinner() => new SpinnerBuilder();

	/// <summary>
	/// Creates a new table control builder
	/// </summary>
	/// <returns>A new table control builder</returns>
	public static TableControlBuilder Table() => new TableControlBuilder();

	/// <summary>
	/// Creates a new panel builder for bordered content panels
	/// </summary>
	/// <returns>A new panel builder</returns>
	public static PanelBuilder Panel() => new PanelBuilder();

	/// <summary>
	/// Creates a fluent builder for a <see cref="FlowControl"/> — an embeddable control that renders a
	/// flow inline (in place, inside an existing window's layout) rather than as a modal.
	/// </summary>
	/// <returns>A new <see cref="FlowControlBuilder"/>.</returns>
	public static FlowControlBuilder Flow() => new FlowControlBuilder();

	/// <summary>
	/// Creates a fluent builder for a <see cref="WizardControl"/> — the discoverable, wizard-named
	/// <see cref="FlowControl"/> that runs a multi-step <c>Flow.Wizard&lt;TState&gt;()</c> inline. Build
	/// it, add it to a container, then call <c>wizard.Run(Flow.Wizard&lt;TState&gt;()...)</c>.
	/// </summary>
	/// <returns>A new <see cref="WizardControlBuilder"/>.</returns>
	public static WizardControlBuilder Wizard() => new WizardControlBuilder();

	/// <summary>
	/// Creates a new chat transcript builder.
	/// </summary>
	/// <returns>A new chat transcript builder.</returns>
	public static ChatTranscriptBuilder ChatTranscript() => new ChatTranscriptBuilder();

	/// <summary>Creates a fluent builder for a CollapsiblePanel, optionally seeding the title.</summary>
	public static CollapsiblePanelBuilder CollapsiblePanel(string? title = null)
	{
		var b = new CollapsiblePanelBuilder();
		if (title != null) b.WithTitle(title);
		return b;
	}

	/// <summary>
	/// Creates a panel control with text content
	/// </summary>
	/// <param name="content">The text content (supports Spectre markup)</param>
	/// <returns>A configured panel control</returns>
	public static PanelControl Panel(string content) => new PanelControl(content);

	/// <summary>
	/// Creates a terminal builder for a PTY-backed terminal control.
	/// Supported platforms: Linux (openpty), Windows 10 1809+ (ConPTY).
	/// The default shell is <c>bash</c> on Linux and <c>cmd.exe</c> on Windows.
	/// Pass <paramref name="exe"/> to launch a different program (e.g. <c>"pwsh"</c>).
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform("linux")]
	[System.Runtime.Versioning.SupportedOSPlatform("windows")]
	public static TerminalBuilder Terminal(string? exe = null)
		=> exe is null ? new TerminalBuilder() : new TerminalBuilder().WithExe(exe);

	/// <summary>
	/// Creates an ImageControl displaying the specified pixel buffer.
	/// </summary>
	/// <param name="source">The pixel buffer to display.</param>
	/// <returns>A configured image control.</returns>
	public static ImageControl Image(PixelBuffer source) => new ImageControl { Source = source };

	/// <summary>
	/// Creates a new image control builder.
	/// </summary>
	/// <returns>A new image control builder.</returns>
	public static ImageControlBuilder Image() => new ImageControlBuilder();

	/// <summary>
	/// Creates a new canvas control builder.
	/// </summary>
	/// <param name="width">Optional canvas width in characters.</param>
	/// <param name="height">Optional canvas height in characters.</param>
	/// <returns>A new canvas control builder.</returns>
	public static CanvasControlBuilder Canvas(int? width = null, int? height = null)
	{
		var builder = new CanvasControlBuilder();
		if (width.HasValue && height.HasValue)
			builder.WithSize(width.Value, height.Value);
		return builder;
	}

	/// <summary>
	/// Creates a VideoControl builder for terminal video playback.
	/// </summary>
	/// <returns>A new video control builder.</returns>
	public static VideoControlBuilder Video() => new VideoControlBuilder();

	/// <summary>
	/// Creates a VideoControl builder with a source pre-set.
	/// Accepts file paths, HTTP/HTTPS URLs, RTSP, HLS, RTMP, FTP — anything FFmpeg supports.
	/// </summary>
	/// <param name="source">File path or URL.</param>
	/// <returns>A new video control builder.</returns>
	public static VideoControlBuilder Video(string source) => new VideoControlBuilder().WithSource(source);

	/// <summary>
	/// Creates a new navigation view builder.
	/// </summary>
	/// <returns>A new navigation view builder.</returns>
	public static NavigationViewBuilder NavigationView() => new NavigationViewBuilder();

	/// <summary>
	/// Creates a new splitter control builder.
	/// </summary>
	/// <returns>A new splitter control builder.</returns>
	public static SplitterControlBuilder Splitter() => new SplitterControlBuilder();

	/// <summary>
	/// Creates a new horizontal splitter control builder.
	/// </summary>
	/// <returns>A new horizontal splitter control builder.</returns>
	public static HorizontalSplitterBuilder HorizontalSplitter() => new HorizontalSplitterBuilder();

	/// <summary>
	/// Creates a new status bar builder.
	/// </summary>
	/// <returns>A new status bar builder.</returns>
	public static StatusBarBuilder StatusBar() => new StatusBarBuilder();

	/// <summary>
	/// Creates a new time picker builder.
	/// </summary>
	/// <param name="prompt">Optional prompt text.</param>
	/// <returns>A new time picker builder.</returns>
	public static TimePickerBuilder TimePicker(string? prompt = null)
	{
		var builder = new TimePickerBuilder();
		if (prompt != null)
			builder.WithPrompt(prompt);
		return builder;
	}

	/// <summary>
	/// Creates a new date picker builder.
	/// </summary>
	/// <param name="prompt">Optional prompt text.</param>
	/// <returns>A new date picker builder.</returns>
	public static DatePickerBuilder DatePicker(string? prompt = null)
	{
		var builder = new DatePickerBuilder();
		if (prompt != null)
			builder.WithPrompt(prompt);
		return builder;
	}

	/// <summary>
	/// Creates a new slider builder.
	/// </summary>
	/// <returns>A new slider builder.</returns>
	public static SliderBuilder Slider() => new SliderBuilder();

	/// <summary>
	/// Creates a new range slider builder.
	/// </summary>
	/// <returns>A new range slider builder.</returns>
	public static RangeSliderBuilder RangeSlider() => new RangeSliderBuilder();

	/// <summary>
	/// Creates a new <see cref="RadioGroupBuilder{T}"/> for coordinating a set of radio controls.
	/// </summary>
	/// <typeparam name="T">The value type each radio in the group represents.</typeparam>
	/// <returns>A new radio group builder.</returns>
	public static RadioGroupBuilder<T> RadioGroup<T>() => new RadioGroupBuilder<T>();

	/// <summary>
	/// Creates a new <see cref="RadioBuilder{T}"/> for a radio control with the given group, value, and label.
	/// </summary>
	/// <typeparam name="T">The value type this radio represents.</typeparam>
	/// <param name="group">The coordinating group.</param>
	/// <param name="value">The value this radio represents.</param>
	/// <param name="label">The text label displayed next to the radio.</param>
	/// <returns>A new radio builder.</returns>
	public static RadioBuilder<T> Radio<T>(SharpConsoleUI.Controls.RadioGroup<T> group, T value, string label = "") =>
		new RadioBuilder<T>(group, value, label);

	/// <summary>
	/// Creates a new <see cref="RadioBuilder{T}"/> for string values where the label doubles as the value.
	/// Equivalent to <c>Radio&lt;string&gt;(group, label, label)</c>.
	/// </summary>
	/// <param name="group">The coordinating string group.</param>
	/// <param name="label">The label text, which is also the option value.</param>
	/// <returns>A new radio builder.</returns>
	public static RadioBuilder<string> Radio(SharpConsoleUI.Controls.RadioGroup<string> group, string label) =>
		new RadioBuilder<string>(group, label, label);

	/// <summary>
	/// Creates a fluent builder for a <see cref="FormControl"/> — a labeled-input form composed
	/// from real input controls in a two-column grid.
	/// </summary>
	/// <returns>A new <see cref="FormBuilder"/>.</returns>
	public static FormBuilder Form() => new FormBuilder();
}
