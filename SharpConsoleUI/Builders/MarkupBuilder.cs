// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for markup controls
/// </summary>
public sealed class MarkupBuilder : IControlBuilder<MarkupControl>
{
	private readonly List<string> _lines = new();
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private bool _enableSelection;
	private Color? _selectionForegroundColor;
	private Color? _selectionBackgroundColor;
	private bool _copyEnabled = true;
	private ConsoleKey _copyKey = ConsoleKey.C;
	private ConsoleModifiers _copyModifiers = ConsoleModifiers.Control;
	private Func<Configuration.MarkdownStyle, Configuration.MarkdownStyle>? _markdownStyleConfig;
	private EventHandler<LinkClickedEventArgs>? _linkClickedHandler;
	private Color? _focusedLinkForegroundColor;
	private Color? _focusedLinkBackgroundColor;
	private Themes.ColorRole _role = Themes.ColorRole.Default;
	private Themes.ThemeMode? _colorRoleMode;
	private bool _outline = false;

	/// <summary>
	/// Sets the control's semantic colour role (drives the default text colour;
	/// inline [color] tags still override it).
	/// </summary>
	/// <param name="role">The semantic role determining the default text colour.</param>
	/// <param name="mode">Optional <see cref="Themes.ThemeMode"/> override for dark/light role-colour derivation. When null, the active theme's mode is used.</param>
	/// <returns>The builder for chaining.</returns>
	public MarkupBuilder WithColorRole(Themes.ColorRole role, Themes.ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return this;
	}

	/// <summary>
	/// Renders the role accent in outline style.
	/// </summary>
	/// <param name="outline">Whether to use outline style.</param>
	/// <returns>The builder for chaining.</returns>
	public MarkupBuilder Outline(bool outline = true)
	{
		_outline = outline;
		return this;
	}

	/// <summary>
	/// Adds a line of markup text
	/// </summary>
	/// <param name="markup">The markup text</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AddLine(string markup)
	{
		// Store the markup verbatim as ONE logical line. Do NOT split on embedded newlines here:
		// the paint path (MarkupControl.PaintDOM / ParseLines) already splits each logical line on
		// "\r\n"/"\r"/"\n" at render time, so CR/CRLF is handled without pre-splitting (issue #45).
		// Splitting here would also shatter a multi-line region whose open/close tags span newlines —
		// e.g. AddMarkdown wraps content as "[markdown]…[/]", and splitting separated the "[markdown]"
		// open tag from its "[/]" close, breaking code-block backgrounds (regression after PR #55).
		_lines.Add(markup ?? string.Empty);
		return this;
	}

	/// <summary>
	/// Appends markup to the current last line without starting a new line, in the style of
	/// <see cref="System.Text.StringBuilder.Append(string)"/> / <c>Console.Write</c>: the first segment is
	/// joined onto the line added so far, and a new line begins only at each embedded <c>\n</c>. Use
	/// <see cref="AddLine"/> when you want each call to start on its own line.
	/// </summary>
	/// <param name="markup">The markup text</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder Append(string markup)
	{
		if (string.IsNullOrEmpty(markup)) return this;

		var parts = markup.Split(["\r\n", "\r", "\n"], StringSplitOptions.None); // fix: handle windows newline char.

		// Ensure at least one line exists, then join the first segment onto the current last line.
		if (_lines.Count == 0)
			_lines.Add(string.Empty);
		_lines[^1] += parts[0];

		// Remaining segments each start a new line.
		for (int i = 1; i < parts.Length; i++)
			_lines.Add(parts[i]);

		return this;
	}

	/// <summary>
	/// Alias for <see cref="Append(string)"/>, retained for compatibility. Prefer <see cref="Append"/>
	/// (matches the .NET <c>StringBuilder.Append</c> / <c>Console.Write</c> convention).
	/// </summary>
	/// <param name="markup">The markup text</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AddText(string markup) => Append(markup);

	/// <summary>Appends a Markdown block, wrapped in a [markdown] region.</summary>
	/// <param name="markdown">The Markdown content.</param>
	/// <returns>This builder for chaining.</returns>
	public MarkupBuilder AddMarkdown(string markdown)
	{
		return AddLine($"[markdown]{markdown ?? string.Empty}[/]");
	}

	/// <summary>Alias for <see cref="AddMarkdown"/>, for fluent readability.</summary>
	/// <param name="markdown">The Markdown content.</param>
	/// <returns>This builder for chaining.</returns>
	public MarkupBuilder WithMarkdown(string markdown) => AddMarkdown(markdown);

	/// <summary>Sets a per-control Markdown style override applied on build.</summary>
	/// <param name="configure">Receives the current default style; return a modified copy.</param>
	/// <returns>This builder for chaining.</returns>
	public MarkupBuilder WithMarkdownStyle(Func<Configuration.MarkdownStyle, Configuration.MarkdownStyle> configure)
	{
		_markdownStyleConfig = configure;
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
	/// Centers the content horizontally
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder Centered()
	{
		_alignment = HorizontalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment
	/// </summary>
	/// <param name="alignment">The vertical alignment</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Centers the content vertically
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder VerticallyCentered()
	{
		_verticalAlignment = VerticalAlignment.Center;
		return this;
	}

	/// <summary>
	/// Aligns content to the top
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AlignTop()
	{
		_verticalAlignment = VerticalAlignment.Top;
		return this;
	}

	/// <summary>
	/// Aligns content to the bottom
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder AlignBottom()
	{
		_verticalAlignment = VerticalAlignment.Bottom;
		return this;
	}

	/// <summary>
	/// Makes the content fill vertically
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder FillVertical()
	{
		_verticalAlignment = VerticalAlignment.Fill;
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
	/// Sets the control name for lookup
	/// </summary>
	/// <param name="name">The control name</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithName(string name)
	{
		_name = name;
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
	/// Sets the sticky position
	/// </summary>
	/// <param name="position">The sticky position</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window
	/// </summary>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Sets the background color for the control
	/// </summary>
	/// <param name="color">The background color</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the foreground (text) color for the control
	/// </summary>
	/// <param name="color">The foreground color</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors for the control
	/// </summary>
	/// <param name="foreground">The foreground (text) color</param>
	/// <param name="background">The background color</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Enables (or disables) mouse text selection and window-level Ctrl+C copy for the control.
	/// Selection is opt-in; when disabled (the default) the control is display-only.
	/// </summary>
	/// <param name="enabled">Whether selection should be enabled. Defaults to true.</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithSelectionEnabled(bool enabled = true)
	{
		_enableSelection = enabled;
		return this;
	}

	/// <summary>
	/// Sets the colors used to highlight selected text. Implies <see cref="WithSelectionEnabled"/>.
	/// </summary>
	/// <param name="foreground">The selected-text foreground color</param>
	/// <param name="background">The selected-text background color</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithSelectionColors(Color foreground, Color background)
	{
		_enableSelection = true;
		_selectionForegroundColor = foreground;
		_selectionBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the colors used to highlight the keyboard-focused link.
	/// </summary>
	/// <param name="foreground">The focused-link foreground color</param>
	/// <param name="background">The focused-link background color</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithFocusedLinkColors(Color foreground, Color background)
	{
		_focusedLinkForegroundColor = foreground;
		_focusedLinkBackgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the keyboard copy shortcut for selected text. Implies <see cref="WithSelectionEnabled"/>.
	/// </summary>
	/// <param name="key">The key that triggers a copy (default <see cref="ConsoleKey.C"/>).</param>
	/// <param name="modifiers">The required modifier keys (default <see cref="ConsoleModifiers.Control"/>).</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithCopyKey(ConsoleKey key, ConsoleModifiers modifiers = ConsoleModifiers.Control)
	{
		_enableSelection = true;
		_copyKey = key;
		_copyModifiers = modifiers;
		return this;
	}

	/// <summary>
	/// Enables or disables the keyboard copy shortcut. Programmatic copy is unaffected.
	/// </summary>
	/// <param name="enabled">Whether the copy shortcut is enabled. Defaults to true.</param>
	/// <returns>The builder for chaining</returns>
	public MarkupBuilder WithCopyEnabled(bool enabled = true)
	{
		_copyEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets the handler raised when a rendered link is clicked.
	/// </summary>
	/// <param name="handler">The LinkClicked event handler.</param>
	/// <returns>The builder for chaining.</returns>
	public MarkupBuilder OnLinkClicked(EventHandler<LinkClickedEventArgs> handler)
	{
		_linkClickedHandler = handler;
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
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Width = _width,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition,
			BackgroundColor = _backgroundColor,
			ForegroundColor = _foregroundColor,
			MarkdownStyle = _markdownStyleConfig != null
				? _markdownStyleConfig(Configuration.MarkdownStyle.Default)
				: null,
			EnableSelection = _enableSelection,
			SelectionForegroundColor = _selectionForegroundColor,
			SelectionBackgroundColor = _selectionBackgroundColor,
			CopyEnabled = _copyEnabled,
			CopyKey = _copyKey,
			CopyModifiers = _copyModifiers,
			FocusedLinkForegroundColor = _focusedLinkForegroundColor,
			FocusedLinkBackgroundColor = _focusedLinkBackgroundColor,
			ColorRole = _role,
			ColorRoleMode = _colorRoleMode,
			Outline = _outline
		};

		if (_linkClickedHandler != null)
			markup.LinkClicked += _linkClickedHandler;

		BindingHelper.ApplyDeferredBindings(this, markup);
		return markup;
	}

	/// <summary>
	/// Implicit conversion to MarkupControl
	/// </summary>
	/// <param name="builder">The builder</param>
	/// <returns>The built markup control</returns>
	public static implicit operator MarkupControl(MarkupBuilder builder) => builder.Build();
}
