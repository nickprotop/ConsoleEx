// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that displays rich text content using Spectre.Console markup syntax.
	/// Supports text alignment, margins, word wrapping, and sticky positioning.
	/// </summary>
	public partial class MarkupControl : BaseControl, IMouseAwareControl, ISelectableControl, ICopyableControl, IFocusableControl, IInteractiveControl, IDragAutoScrollTarget, IColorRoleableControl
	{

		#region ColorRole

		private ColorRole _role = ColorRole.Default;
		private ThemeMode? _colorRoleMode;
		private bool _outline;

		/// <inheritdoc/>
		public ColorRole ColorRole
		{
			get => _role;
			set => SetProperty(ref _role, value);
		}

		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode
		{
			get => _colorRoleMode;
			set => SetProperty(ref _colorRoleMode, value);
		}

		/// <inheritdoc/>
		public bool Outline
		{
			get => _outline;
			set => SetProperty(ref _outline, value);
		}

		#endregion

		private List<string> _content;
		private readonly object _contentLock = new();
		private bool _wrap = true;
		private Color? _backgroundColor = null;
		private Color? _foregroundColor = null;
		private Configuration.MarkdownStyle? _markdownStyle = null;

		// Memoize the theme-derived MarkdownStyle so ResolveMarkdownStyle returns a STABLE reference for an
		// unchanged theme. MarkdownStyle is a record but carries a Dictionary member (CodeHighlighters) that
		// has reference equality, so each fresh FromTheme() is record-unequal to the last — which would make
		// the parse-cache ParseKey (which includes MdStyle) miss on every frame. Caching the instance keeps
		// measure and paint keyed on the same reference. Invalidated when the source theme changes.
		private Themes.ITheme? _mdStyleThemeKey;
		private Configuration.MarkdownStyle? _mdStyleFromTheme;
		private BorderStyle _border = BorderStyle.None;
		private Color? _borderColor;
		private string? _header;
		private TextJustification _headerAlignment = TextJustification.Left;
		private bool _useSafeBorder;
		private Padding _padding = new Padding(0, 0, 0, 0);

		// Double-click detection
		private DateTime _lastClickTime = DateTime.MinValue;
		private Point _lastClickPosition = Point.Empty;
		private int _doubleClickThresholdMs = Configuration.ControlDefaults.DefaultDoubleClickThresholdMs;
		private bool _doubleClickEnabled = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="MarkupControl"/> class with the specified lines of text.
		/// </summary>
		/// <param name="lines">The lines of text to display, supporting Spectre.Console markup syntax.</param>
		public MarkupControl(List<string> lines)
		{
			lock (_contentLock) { _content = lines; }
			InitKeyboardNav();
		}

		/// <summary>
		/// Creates a fluent builder for constructing a MarkupControl.
		/// </summary>
		/// <returns>A new MarkupBuilder instance.</returns>
		public static Builders.MarkupBuilder Create()
		{
			return new Builders.MarkupBuilder();
		}

		/// <summary>
		/// Gets the actual rendered width of the control based on content.
		/// </summary>
		/// <returns>The maximum line width in characters.</returns>
		public override int? ContentWidth
		{
			get
			{
				// Measure the width the content actually RENDERS to (matching the paint path), not
				// StripLength — StripLength strips any [..] as a tag, but Parse renders unknown/literal
				// brackets (JSON, [INFO] logs, array[0]) as literal text, so StripLength under-measured and
				// the label was allotted a too-small width and wrapped to a tiny column count (#63). Parse
				// UNCONSTRAINED (int.MaxValue = one row per hard line, no soft-wrap) for the natural content
				// width; EnsureParsed is LRU-cached so repeated reads of this hot getter don't re-parse.
				int maxLength = NaturalContentWidth();

				// Include the optional border + inner padding so auto-sizing (TabLayout / column
				// autofit) reserves room for the frame. Zero when borderless with no padding, so the
				// returned width is unchanged from the plain-text path.
				int borderInset = _border == BorderStyle.None ? 0 : 1;
				int chromeWidth = borderInset * 2 + _padding.Left + _padding.Right;
				return maxLength + Margin.Left + Margin.Right + chromeWidth;
			}
		}

		/// <summary>
		/// Gets or sets the text content as a single string with newline separators.
		/// </summary>
		public string Text
		{
			get { lock (_contentLock) { return string.Join("\n", _content); } }
			set
			{
				lock (_contentLock) { _content = SplitContentPreservingTagRegions(value); }
				BumpContentVersion();
				InvalidateLinkCount();
				OnPropertyChanged();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Gets or sets whether text should wrap to multiple lines when exceeding available width.
		/// </summary>
		public bool Wrap
		{
			get => _wrap;
			set => SetProperty(ref _wrap, value);
		}

		/// <summary>Optional border drawn around the markup. <see cref="BorderStyle.None"/> (default) = no border.</summary>
		public BorderStyle Border { get => _border; set => SetProperty(ref _border, value); }

		/// <summary>Border color. Null falls back to the ColorRole border / foreground.</summary>
		public Color? BorderColor { get => _borderColor; set => SetProperty(ref _borderColor, value); }

		/// <summary>Optional header text rendered in the top border (only when bordered).</summary>
		public string? Header { get => _header; set => SetProperty(ref _header, value); }

		/// <summary>Horizontal alignment of the header text within the top border.</summary>
		public TextJustification HeaderAlignment { get => _headerAlignment; set => SetProperty(ref _headerAlignment, value); }

		/// <summary>Use ASCII-safe border characters for better terminal compatibility.</summary>
		public bool UseSafeBorder { get => _useSafeBorder; set => SetProperty(ref _useSafeBorder, value); }

		/// <summary>Inner padding between the border (or edge) and the markup content.</summary>
		public Padding Padding { get => _padding; set => SetProperty(ref _padding, value); }

		/// <summary>
		/// Gets or sets whether double-click events are enabled.
		/// Default: true.
		/// </summary>
		public bool DoubleClickEnabled
		{
			get => _doubleClickEnabled;
			set { _doubleClickEnabled = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets the double-click threshold in milliseconds.
		/// Default: 500ms, minimum: 100ms.
		/// </summary>
		public int DoubleClickThresholdMs
		{
			get => _doubleClickThresholdMs;
			set { _doubleClickThresholdMs = Math.Max(100, value); OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets or sets the background color for the control. If null, uses container's background color.
		/// When set with HorizontalAlignment.Stretch, this color will fill the entire width.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColor;
			set => SetProperty(ref _backgroundColor, value);
		}

		/// <summary>
		/// Gets or sets the foreground (text) color for the control. If null, uses container's foreground color.
		/// </summary>
		public Color? ForegroundColor
		{
			get => _foregroundColor;
			set => SetProperty(ref _foregroundColor, value);
		}

		/// <summary>
		/// Optional per-control Markdown style for content set via <see cref="SetMarkdown"/> or
		/// the <c>[markdown]</c> tag. When null, the global <see cref="Configuration.MarkdownStyle.Default"/> is used.
		/// </summary>
		public Configuration.MarkdownStyle? MarkdownStyle
		{
			get => _markdownStyle;
			set => SetProperty(ref _markdownStyle, value);
		}

		/// <summary>
		/// Resolves the Markdown style for this control's content: explicit per-control style →
		/// explicit global <see cref="Configuration.MarkdownStyle.Default"/> → theme-derived
		/// (<see cref="Configuration.MarkdownStyle.FromTheme"/>) → built-in default. So Markdown
		/// follows the active theme by default, while any explicitly-set style remains the user's choice.
		/// </summary>
		private Configuration.MarkdownStyle? ResolveMarkdownStyle()
		{
			if (_markdownStyle != null)
				return _markdownStyle;
			if (Configuration.MarkdownStyle.DefaultExplicitlySet)
				return Configuration.MarkdownStyle.Default;
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			if (theme == null)
				return null;

			// Return a STABLE instance for an unchanged theme (see _mdStyleThemeKey field comment) so the
			// parse-cache key doesn't churn on the record's reference-equality Dictionary member.
			if (!ReferenceEquals(_mdStyleThemeKey, theme))
			{
				_mdStyleThemeKey = theme;
				_mdStyleFromTheme = Configuration.MarkdownStyle.FromTheme(theme);
			}
			return _mdStyleFromTheme;
		}

		#region IMouseAwareControl Implementation

		/// <summary>
		/// Gets whether this control wants to receive mouse events.
		/// </summary>
		public bool WantsMouseEvents => true;

		/// <summary>
		/// Gets whether a mouse click on this control grants it focus. True only when the control is
		/// focusable for the same reason it is Tab-focusable — visible, enabled, and containing at
		/// least one link (see <see cref="CanReceiveFocus"/>). A plain MarkupControl with no links is
		/// not a focus target and clicking it does not steal focus.
		/// </summary>
		public bool CanFocusWithMouse => CanReceiveFocus;

		/// <summary>
		/// Occurs when the control is clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Occurs when the control is double-clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Occurs when the control is right-clicked.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <summary>
		/// Occurs when the mouse enters the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>
		/// Occurs when the mouse leaves the control area.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>
		/// Occurs when the mouse moves over the control.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			// Reuse ContentWidth for width calculation (already includes border + padding chrome).
			int width = ContentWidth ?? 0;

			// Calculate total lines (including splits)
			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }
			int totalLines = 0;
			foreach (var line in snapshot)
			{
				var subLines = line.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);  // fix: handle windows newline char.
				totalLines += subLines.Length;
			}

			// Include the optional border + inner padding in the logical height so a bordered markup
			// inside a ScrollablePanel scrolls to its true extent. Zero when borderless with no
			// padding, keeping the no-border path identical.
			int borderInset = _border == BorderStyle.None ? 0 : 1;
			int chromeHeight = borderInset * 2 + _padding.Top + _padding.Bottom;
			return new System.Drawing.Size(width, totalLines + chromeHeight);
		}

		/// <summary>
		/// Sets the content of the control to the specified lines of text.
		/// </summary>
		/// <param name="lines">The lines of text to display, supporting Spectre.Console markup syntax.</param>
		public void SetContent(List<string> lines)
		{
			lock (_contentLock) { _content = lines; }
			BumpContentVersion();
			InvalidateLinkCount();
			OnPropertyChanged(nameof(Text));
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Sets the control's content from Markdown. The content is wrapped in a <c>[markdown]</c>
		/// region and rendered through the markup pipeline; copied text remains plain.
		/// </summary>
		/// <param name="markdown">The Markdown source to render.</param>
		public void SetMarkdown(string markdown)
		{
			SetContent(new List<string> { $"[markdown]{markdown ?? string.Empty}[/]" });
		}

		/// <summary>
		/// Sets the control's content from Markdown. Discoverable alias for <see cref="SetMarkdown"/>:
		/// the source is wrapped in a <c>[markdown]</c> region and rendered through the markup pipeline.
		/// </summary>
		/// <param name="markdown">The Markdown source to render.</param>
		public void Markdown(string markdown) => SetMarkdown(markdown);

		/// <summary>
		/// Splits an incoming <see cref="Text"/> value into content lines on newlines, but keeps each
		/// balanced <c>[markdown]…[/]</c> region (including its embedded newlines) as a SINGLE content
		/// entry — exactly as <see cref="SetMarkdown"/> stores it. Without this, a multi-line
		/// <c>[markdown]</c> region would be torn across lines, so the per-line parse would never see a
		/// complete region and would render the Markdown literally (issue #59). Text OUTSIDE markdown
		/// regions still splits per line. The split is reversible by <see cref="Text"/>'s
		/// <c>string.Join("\n", _content)</c> getter, so set-then-get round-trips.
		/// <para>
		/// Scope: only the <c>[markdown]</c> tag is treated atomically. A multi-line NON-markdown tag
		/// (e.g. <c>[yellow]A\nB[/]</c>) is NOT made atomic here on purpose: even if its source were kept
		/// as one entry, the render path (<c>MarkupParser.ParseLines</c>) splits each
		/// logical line on <c>\n</c> and parses every sub-line independently, so the open tag's style does
		/// not carry to the next line — the colour would still be lost. <c>[markdown]</c> is the exception
		/// because <c>PreProcessMarkdownTags</c> expands it into per-line native markup BEFORE that split.
		/// Truly fixing multi-line non-markdown tags requires threading the open-style stack across
		/// newlines inside <c>ParseLines</c> (the frozen render path) — a separate, maintainer-gated
		/// change. For multi-line styled blocks today, wrap in <c>[markdown]</c> or set self-closed lines.
		/// </para>
		/// </summary>
		internal static List<string> SplitContentPreservingTagRegions(string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('[') < 0)
			{
				// Fast path: no tag at all — split per line exactly as before.
				return value.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).ToList();
			}

			// Walk the string, accumulating the current line. A balanced [tag]…[/] region (markdown,
			// yellow, bold, link=…, etc.) is swallowed WHOLE — its embedded newlines do NOT end the
			// current line — so the whole region lands in ONE content entry. The render path then parses
			// that entry as a unit, carrying the open tag's style across the embedded newlines (a region
			// torn across two entries would parse each half separately and drop the style on line 2).
			// Newlines OUTSIDE any region end the current line as usual. Because every kept newline becomes
			// exactly one entry boundary, string.Join("\n", result) reverses this split (it round-trips);
			// \r\n and lone \r normalize to a single boundary, matching the previous Split behavior.
			var result = new List<string>();
			var line = new System.Text.StringBuilder();
			int i = 0;
			int len = value.Length;

			while (i < len)
			{
				char c = value[i];

				if (c == '\r' || c == '\n')
				{
					result.Add(line.ToString());
					line.Clear();
					i += (c == '\r' && i + 1 < len && value[i + 1] == '\n') ? 2 : 1; // \r\n is one newline
					continue;
				}

				if (c == '[')
				{
					// Escaped [[ — a literal '[', not a tag. Emit both chars and move on (the embedded
					// newline after a literal [[tag]] is a normal depth-0 split point).
					if (i + 1 < len && value[i + 1] == '[')
					{
						line.Append("[[");
						i += 2;
						continue;
					}

					int tagEnd = value.IndexOf(']', i + 1);
					if (tagEnd > i)
					{
						string tag = value.Substring(i + 1, tagEnd - i - 1);
						// A real OPENING tag (non-empty, not the close "/"): swallow the whole balanced
						// region through its matching [/] (depth-aware, mirrors how the parser reads it).
						// Reuse MarkupParser.FindMatchingCloseTag so the escape/nesting semantics match
						// exactly. Unclosed (no matching [/]) → keep the rest as one entry (do not tear).
						if (!string.IsNullOrEmpty(tag) && tag != "/")
						{
							int closeAt = Parsing.MarkupParser.FindMatchingCloseTag(value, tagEnd + 1);
							int regionEnd = closeAt < 0 ? len : closeAt + "[/]".Length;
							line.Append(value, i, regionEnd - i); // whole region, embedded newlines included
							i = regionEnd;
							continue;
						}
					}
				}

				line.Append(c);
				i++;
			}

			result.Add(line.ToString());
			return result;
		}

		/// <summary>
		/// Appends markup to the end of the content without starting a new line, in the style of
		/// <see cref="System.Text.StringBuilder.Append(string)"/> / <c>Console.Write</c>: the text is
		/// joined onto the current last line, and a new line begins only at each embedded <c>\n</c>.
		/// Pairs with <see cref="AppendLine(string)"/>.
		/// </summary>
		/// <param name="text">The text to append, supporting markup syntax. Embedded <c>\n</c> characters start new lines.</param>
		public void Append(string text) => AppendText(text, inline: true);

		/// <summary>
		/// Appends a single line of markup to the end of the content.
		/// </summary>
		/// <param name="line">The line to append, supporting markup syntax.</param>
		public void AppendLine(string line)
		{
			lock (_contentLock) { _content.Add(line ?? string.Empty); }
			OnContentAppended();
		}

		/// <summary>
		/// Appends multiple lines of markup to the end of the content.
		/// </summary>
		/// <param name="lines">The lines to append, supporting markup syntax.</param>
		public void AppendLines(IEnumerable<string> lines)
		{
			lock (_contentLock)
			{
				foreach (var line in lines)
					_content.Add(line ?? string.Empty);
			}
			OnContentAppended();
		}

		/// <summary>
		/// Appends text to the content, splitting on newlines into separate lines.
		/// </summary>
		/// <param name="text">The text to append. Embedded <c>\n</c> characters start new lines.</param>
		/// <param name="inline">
		/// When <c>false</c> (the default), the appended text starts on a new line — each segment between
		/// <c>\n</c> characters becomes its own content line. When <c>true</c>, the first segment is joined
		/// onto the current last line (<c>Console.Write</c>-style), and a new line begins only at each
		/// embedded <c>\n</c>.
		/// </param>
		public void AppendText(string text, bool inline = false)
		{
			if (string.IsNullOrEmpty(text)) return;

			var parts = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);  // fix: handle windows newline char.
			lock (_contentLock)
			{
				int startIndex = 0;
				if (inline && _content.Count > 0)
				{
					// Join the first segment onto the current last line; remaining segments are new lines.
					_content[^1] += parts[0];
					startIndex = 1;
				}

				for (int i = startIndex; i < parts.Length; i++)
					_content.Add(parts[i]);
			}
			OnContentAppended();
		}

		/// <summary>
		/// Appends text to the current last line (<c>Console.Write</c>-style), starting new lines only at
		/// embedded <c>\n</c> characters. Equivalent to <see cref="AppendText(string, bool)"/> with
		/// <c>inline: true</c>.
		/// </summary>
		/// <param name="text">The text to append. Embedded <c>\n</c> characters start new lines.</param>
		public void AppendInline(string text) => AppendText(text, inline: true);


		/// <summary>Shared post-append bookkeeping: any active selection is now stale, so clear it.</summary>
		private void OnContentAppended()
		{
			BumpContentVersion();
			if (_enableSelection && _hasSelection)
				ClearSelection();
			InvalidateLinkCount();
			OnPropertyChanged(nameof(Text));
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Processes mouse events for this control.
		/// </summary>
		/// <param name="args">The mouse event arguments.</param>
		/// <returns>True if the event was handled; otherwise, false.</returns>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!WantsMouseEvents || args.Handled)
				return false;

			// Opt-in text selection. When disabled (the default), behavior is unchanged for
			// existing users — this branch is skipped entirely.
			if (_enableSelection && TryProcessSelectionMouse(args, out bool selectionHandled))
				return selectionHandled;

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Handle double-click (driver-level detection - preferred method). Only CONSUME the event
			// when there is actually a subscriber — a passive MarkupControl (no MouseDoubleClick handler)
			// must leave the event unconsumed so it bubbles to a hosting container (e.g. a PanelControl
			// facade whose body double-click should select/activate the widget). This mirrors the plain
			// MouseClick path below, which only returns true when MouseClick != null.
			if (args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickEnabled && MouseDoubleClick != null)
			{
				// Reset tracking state since driver handled the gesture
				_lastClickTime = DateTime.MinValue;
				_lastClickPosition = Point.Empty;

				MouseDoubleClick.Invoke(this, args);
				return true;
			}

			// Handle click with manual double-click detection (fallback)
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				// Focus on click when this control is a focus target (i.e. it has links). This applies
				// to clicks ANYWHERE on the control, not just on a link, so a user can click the body
				// and then arrow between links. A linkless MarkupControl has CanFocusWithMouse == false
				// and is left unfocused.
				if (!HasFocus && CanFocusWithMouse)
					((IWindowControl)this).GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);

				// Link click takes priority over plain/double click when a link is under the cursor.
				if (TryRaiseLinkClick(args))
					return true;

				// Detect double-click
				var now = DateTime.UtcNow;
				var timeSince = (now - _lastClickTime).TotalMilliseconds;
				bool isDoubleClick = _doubleClickEnabled &&
									 args.Position == _lastClickPosition &&
									 timeSince <= _doubleClickThresholdMs;

				// Always update tracking state
				_lastClickTime = now;
				_lastClickPosition = args.Position;

				// Mutually exclusive: Fire either MouseDoubleClick OR MouseClick
				// Only consider handled if there are subscribers
				if (isDoubleClick && MouseDoubleClick != null)
				{
					MouseDoubleClick.Invoke(this, args);
					return true;
				}
				else if (!isDoubleClick && MouseClick != null)
				{
					MouseClick.Invoke(this, args);
					return true;
				}

				// No subscribers - let the event propagate (e.g., to UnhandledMouseClick)
				return false;
			}

			// Handle mouse enter
			if (args.HasFlag(MouseFlags.MouseEnter))
			{
				MouseEnter?.Invoke(this, args);
				return false;  // Don't mark as handled, allow propagation
			}

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				MouseLeave?.Invoke(this, args);
				return false;  // Don't mark as handled, allow propagation
			}

			// Handle mouse move
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
				return false;  // Don't mark as handled, allow propagation
			}

			return false;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Chrome added by an optional border + inner padding. Zero when borderless with no padding,
			// so the measured size is unchanged from the plain-text path (regression guard).
			int borderInset = _border == BorderStyle.None ? 0 : 1;
			int chromeWidth = borderInset * 2 + _padding.Left + _padding.Right;
			int chromeHeight = borderInset * 2 + _padding.Top + _padding.Bottom;

			int targetWidth = Width ?? constraints.MaxWidth;
			int contentWidth = Math.Max(0, targetWidth - Margin.Left - Margin.Right - chromeWidth);

			// Parse (or reuse cached parse) to get rows + per-line counts without re-parsing on a hit.
			var mdStyle = ResolveMarkdownStyle();
			var (measureFg, measureBg) = ResolveParseColors();
			int parseWidth = ComputeParseWidth(contentWidth);
			var parsed = EnsureParsed(parseWidth, measureFg, measureBg, mdStyle, _wrap);

			int maxContentWidth = 0;
			for (int i = 0; i < parsed.Rows.Count; i++)
				maxContentWidth = Math.Max(maxContentWidth, parsed.Rows[i].Count);
			int totalLines = parsed.TotalRows;

			// Account for margins
			// For Stretch alignment, request full available width
			// For other alignments, request only what content needs
			int contentBasedWidth = maxContentWidth + Margin.Left + Margin.Right + chromeWidth;
			int width = HorizontalAlignment == HorizontalAlignment.Stretch
				? targetWidth + Margin.Left + Margin.Right
				: Math.Min(targetWidth, contentBasedWidth);
			int height = totalLines + Margin.Top + Margin.Bottom + chromeHeight;

			// Guard against invalid constraints (e.g. when container is resized very small)
			int clampedMinW = Math.Min(constraints.MinWidth, constraints.MaxWidth);
			int clampedMinH = Math.Min(constraints.MinHeight, constraints.MaxHeight);

			return new LayoutSize(
				Math.Clamp(width, clampedMinW, constraints.MaxWidth),
				Math.Clamp(height, clampedMinH, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			Color bgColor = Container?.BackgroundColor ?? defaultBg;
			Color fgColor = Container?.ForegroundColor ?? defaultFg;
			var marginBg = Color.Transparent;

			// Border + padding inset. When _border == None and padding is all-zero, these insets
			// EQUAL the margins, so the paint path is byte-identical to the borderless behavior
			// (the regression guard). The content area shrinks by the border (1 cell each side when
			// present) plus padding; everything that delimits the CONTENT AREA uses the *Inset vars,
			// while the outer-margin fills stay on Margin.*.
			int borderInset = _border == BorderStyle.None ? 0 : 1;
			int leftInset = Margin.Left + borderInset + _padding.Left;
			int topInset = Margin.Top + borderInset + _padding.Top;
			int rightInset = Margin.Right + borderInset + _padding.Right;
			int bottomInset = Margin.Bottom + borderInset + _padding.Bottom;

			int targetWidth = bounds.Width - leftInset - rightInset;
			if (targetWidth <= 0) return;

			// Resolve the parse colors via the SHARED resolver so the ParseKey computed here matches the
			// one MeasureDOM computes — that lets measure + paint share ONE cache entry (no per-frame
			// re-parse). The role sets the DEFAULT foreground passed to the markup parser; inline [color]
			// tags in the content still override it (applied during parsing, after the default).
			var mdStyle = ResolveMarkdownStyle();
			var (effectiveFg, effectiveBg) = ResolveParseColors();

			// Consume the parse cache instead of re-parsing. EnsureParsed returns one cell-row per display
			// row, its source (logical) line index, and the link spans per row — the same three lists the
			// old per-line loop built. CountParse now lives inside EnsureParsed, so a repeated paint with an
			// unchanged key performs zero new parses (the issue #42 fix).
			int parseWidth = ComputeParseWidth(targetWidth);
			var parsed = EnsureParsed(parseWidth, effectiveFg, effectiveBg, mdStyle, _wrap);
			var renderedCellLines = parsed.Rows;
			var renderedLinkLines = parsed.RowLinks;
			var rowSourceLineIndex = parsed.RowSourceLine;
			var rowSoftWrapFlags = parsed.RowIsSoftWrapContinuation;

			// Calculate content width for alignment (Center/Right). Derived from the parsed rows (already in
			// display columns) so it stays consistent with what is painted.
			int maxContentWidth = 0;
			for (int i = 0; i < renderedCellLines.Count; i++)
				maxContentWidth = Math.Max(maxContentWidth, renderedCellLines[i].Count);

			// Paint with margins + border + padding inset
			int startY = bounds.Y + topInset;
			int startX = bounds.X + leftInset;

			// Cache the laid-out grid + paint origin so mouse hit-testing maps screen coords
			// to (displayRow, cellIndex) over the exact cells that were painted.
			// NOTE: mouse coordinates delivered to ProcessMouseEvent are CONTROL-RELATIVE
			// (content top-left = (0,0)), so the cache stores origins relative to the control
			// (Margin.Top / Margin.Left + alignOffset), NOT the absolute buffer bounds.
			UpdateSelectionLayoutCache(renderedCellLines, rowSourceLineIndex, rowSoftWrapFlags, leftInset, topInset, targetWidth);
			UpdateLinkLayoutCache(renderedLinkLines);
			UpdateLinkVisibilityCache(renderedCellLines.Count, startY, clipRect.Y, clipRect.Bottom);

			// Resolve the focused link's (row, span) once for this paint so the loop can invert its cells.
			bool highlightFocus = ComputeHasFocus() && _focusedLinkIndex >= 0;
			int focusedRow = -1;
			Parsing.LinkSpan focusedSpan = default;
			if (highlightFocus)
			{
				var flat = FlattenLinks();
				if (_focusedLinkIndex < flat.Count)
				{
					focusedRow = flat[_focusedLinkIndex].row;
					focusedSpan = flat[_focusedLinkIndex].span;
				}
				else
				{
					highlightFocus = false;
				}
			}

			// Fill top margin (outer margin rows above the border/content)
			int topMarginEnd = bounds.Y + Margin.Top;
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, topMarginEnd, fgColor, marginBg);

			// ColorRole anchor for a bordered control is the frame: explicit border → role border → foreground.
			Color borderColor = _borderColor
				?? ColorResolver.ColorRoleBorder(ColorRole, Container, Outline, Themes.ColorRoleState.Normal, mode: ColorRoleMode)
				?? effectiveFg;

			// Paint content lines (leave room for the bottom border + bottom padding + bottom margin)
			for (int i = 0; i < renderedCellLines.Count && startY + i < bounds.Bottom - bottomInset; i++)
			{
				int y = startY + i;
				if (y < clipRect.Y || y >= clipRect.Bottom)
					continue;

				var cellLine = renderedCellLines[i];
				int lineWidth = cellLine.Count;

				// Calculate alignment offset
				int alignOffset = 0;
				if (lineWidth < targetWidth)
				{
					switch (HorizontalAlignment)
					{
						case HorizontalAlignment.Center:
							alignOffset = (targetWidth - lineWidth) / 2;
							break;
						case HorizontalAlignment.Right:
							alignOffset = targetWidth - lineWidth;
							break;
					}
				}

				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, marginBg);
				}

				// Fill alignment padding (left side)
				if (alignOffset > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, y, alignOffset, 1), fgColor, marginBg);
				}

				// Record this row's horizontal paint offset (control-relative) for mouse hit-testing.
				// Uses the full inset (margin + border + padding) so a click inside a bordered markup
				// maps to the correct cell.
				SetRowPaintOffset(i, leftInset + alignOffset);

				// Apply selection highlight (only when selection is enabled and this row is selected).
				var paintLine = ApplySelectionHighlight(i, cellLine);

				// Apply focus highlight (keyboard nav): invert the focused link's cells on its row.
				if (highlightFocus && i == focusedRow)
					paintLine = ApplyFocusHighlight(paintLine, focusedSpan);

				// Paint the line content
				buffer.WriteCellsClipped(startX + alignOffset, y, paintLine, clipRect);

				// Fill remaining space (right side) up to the right inset (leaves the right border /
				// right padding / right margin columns untouched).
				int rightPadStart = startX + alignOffset + lineWidth;
				int rightPadWidth = bounds.Right - rightPadStart - rightInset;
				if (rightPadWidth > 0)
				{
					// Use the control's background color if set, otherwise container's
					var rightFillBg = _backgroundColor == null ? Color.Transparent : _backgroundColor.Value;

					// If this line's last cell requests fill-to-width (via the [fillwidth] marker),
					// extend that cell's background instead — e.g. a shaded code-block line whose
					// trailing pad carries the code background should fill solid to the right edge.
					if (cellLine.Count > 0 && cellLine[^1].FillToWidth)
					{
						rightFillBg = cellLine[^1].Background;
					}

					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, y, rightPadWidth, 1), fgColor, rightFillBg);
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, marginBg);
				}
			}

			// Fill bottom margin and remaining space below the content.
			int contentEndY = startY + renderedCellLines.Count;
			for (int y = contentEndY; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, marginBg);
				}
			}

			// Draw the border frame LAST so its top/bottom borders and left/right vertical edges
			// overwrite any margin/content spill. The frame helper only lays down chrome (border
			// glyphs + interior fill on non-content rows / edge cells on content rows); the painted
			// content above survives because the inset guarantees it landed strictly inside the frame.
			if (_border != BorderStyle.None)
			{
				PaintBorderFrame(buffer, bounds, clipRect, borderColor, effectiveBg, renderedCellLines.Count, topInset, bottomInset);
			}
		}

		#endregion
	}
}
