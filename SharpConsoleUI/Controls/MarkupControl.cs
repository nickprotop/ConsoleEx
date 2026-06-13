// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that displays rich text content using Spectre.Console markup syntax.
	/// Supports text alignment, margins, word wrapping, and sticky positioning.
	/// </summary>
	public partial class MarkupControl : BaseControl, IMouseAwareControl, ISelectableControl, ICopyableControl
	{
		private List<string> _content;
		private readonly object _contentLock = new();
		private bool _wrap = true;
		private Color? _backgroundColor = null;
		private Color? _foregroundColor = null;
		private Configuration.MarkdownStyle? _markdownStyle = null;

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
				List<string> snapshot;
				lock (_contentLock) { snapshot = _content.ToList(); }
				int maxLength = 0;
				foreach (var line in snapshot)
				{
					int length = Parsing.MarkupParser.StripLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + Margin.Left + Margin.Right;
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
				lock (_contentLock) { _content = value.Split('\n').ToList(); }
				OnPropertyChanged();
				Container?.Invalidate(true);
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

		#region IMouseAwareControl Implementation

		/// <summary>
		/// Gets whether this control wants to receive mouse events.
		/// </summary>
		public bool WantsMouseEvents => true;

		/// <summary>
		/// Gets whether this control can receive focus via mouse click.
		/// MarkupControl is display-only, so it doesn't take keyboard focus.
		/// </summary>
		public bool CanFocusWithMouse => false;

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
			// Reuse ContentWidth for width calculation
			int width = ContentWidth ?? 0;

			// Calculate total lines (including splits)
			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }
			int totalLines = 0;
			foreach (var line in snapshot)
			{
				var subLines = line.Split('\n');
				totalLines += subLines.Length;
			}

			return new System.Drawing.Size(width, totalLines);
		}

		/// <summary>
		/// Sets the content of the control to the specified lines of text.
		/// </summary>
		/// <param name="lines">The lines of text to display, supporting Spectre.Console markup syntax.</param>
		public void SetContent(List<string> lines)
		{
			lock (_contentLock) { _content = lines; }
			OnPropertyChanged(nameof(Text));
			Container?.Invalidate(true);
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
		public void AppendText(string text)
		{
			var parts = (text ?? string.Empty).Split('\n');
			lock (_contentLock)
			{
				foreach (var part in parts)
					_content.Add(part);
			}
			OnContentAppended();
		}

		/// <summary>Shared post-append bookkeeping: any active selection is now stale, so clear it.</summary>
		private void OnContentAppended()
		{
			if (_enableSelection && _hasSelection)
				ClearSelection();
			OnPropertyChanged(nameof(Text));
			Container?.Invalidate(true);
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

			// Handle double-click (driver-level detection - preferred method)
			if (args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickEnabled)
			{
				// Reset tracking state since driver handled the gesture
				_lastClickTime = DateTime.MinValue;
				_lastClickPosition = Point.Empty;

				MouseDoubleClick?.Invoke(this, args);
				return true;
			}

			// Handle click with manual double-click detection (fallback)
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
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
			int targetWidth = Width ?? constraints.MaxWidth;
			int contentWidth = Math.Max(0, targetWidth - Margin.Left - Margin.Right);

			// Calculate content dimensions
			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }
			int maxContentWidth = 0;
			int totalLines = 0;

			foreach (var line in snapshot)
			{
				int lineWidth = Parsing.MarkupParser.StripLength(line);
				maxContentWidth = Math.Max(maxContentWidth, lineWidth);

				if (_wrap && contentWidth > 0)
				{
					// Use actual word-wrap logic for accurate line count
					var wrappedLines = Parsing.MarkupParser.ParseLines(line, contentWidth, Color.White, Color.Transparent);
					totalLines += wrappedLines.Count;
				}
				else
				{
					// Count explicit newlines
					var subLines = line.Split('\n');
					totalLines += subLines.Length;
				}
			}

			// Account for margins
			// For Stretch alignment, request full available width
			// For other alignments, request only what content needs
			int contentBasedWidth = maxContentWidth + Margin.Left + Margin.Right;
			int width = HorizontalAlignment == HorizontalAlignment.Stretch
				? targetWidth + Margin.Left + Margin.Right
				: Math.Min(targetWidth, contentBasedWidth);
			int height = totalLines + Margin.Top + Margin.Bottom;

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

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }

			// Calculate content width for alignment
			int maxContentWidth = 0;
			foreach (var line in snapshot)
			{
				int length = Parsing.MarkupParser.StripLength(line);
				maxContentWidth = Math.Max(maxContentWidth, length);
			}

			// Render content lines.
			// Track each display row's source (logical) line index so selection copy can suppress
			// soft-wrap newlines (rows from the same logical line are joined without a line break).
			Color effectiveFg = _foregroundColor ?? fgColor;
			Color effectiveBg = _backgroundColor ?? Color.Transparent;
			var renderedCellLines = new List<List<Cell>>();
			var renderedLinkLines = new List<List<Parsing.LinkSpan>>();
			var rowSourceLineIndex = new List<int>();
			for (int sourceIndex = 0; sourceIndex < snapshot.Count; sourceIndex++)
			{
				var line = snapshot[sourceIndex];
				int renderWidth = (HorizontalAlignment == HorizontalAlignment.Center || HorizontalAlignment == HorizontalAlignment.Right)
					? Math.Min(maxContentWidth, targetWidth)
					: targetWidth;

				if (_wrap)
				{
					var wrappedLines = Parsing.MarkupParser.ParseLines(line, renderWidth, effectiveFg, effectiveBg, out var wrappedLinks);
					for (int w = 0; w < wrappedLines.Count; w++)
					{
						renderedCellLines.Add(wrappedLines[w]);
						renderedLinkLines.Add(w < wrappedLinks.Count ? wrappedLinks[w] : new List<Parsing.LinkSpan>());
						rowSourceLineIndex.Add(sourceIndex);
					}
				}
				else
				{
					var cells = Parsing.MarkupParser.Parse(line, effectiveFg, effectiveBg, out var lineLinks);
					renderedCellLines.Add(cells);
					renderedLinkLines.Add(lineLinks);
					rowSourceLineIndex.Add(sourceIndex);
				}
			}

			// Paint with margins
			int startY = bounds.Y + Margin.Top;
			int startX = bounds.X + Margin.Left;

			// Cache the laid-out grid + paint origin so mouse hit-testing maps screen coords
			// to (displayRow, cellIndex) over the exact cells that were painted.
			// NOTE: mouse coordinates delivered to ProcessMouseEvent are CONTROL-RELATIVE
			// (content top-left = (0,0)), so the cache stores origins relative to the control
			// (Margin.Top / Margin.Left + alignOffset), NOT the absolute buffer bounds.
			UpdateSelectionLayoutCache(renderedCellLines, rowSourceLineIndex, Margin.Left, Margin.Top, targetWidth);
			UpdateLinkLayoutCache(renderedLinkLines);

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, marginBg);

			// Paint content lines
			for (int i = 0; i < renderedCellLines.Count && startY + i < bounds.Bottom; i++)
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
				SetRowPaintOffset(i, Margin.Left + alignOffset);

				// Apply selection highlight (only when selection is enabled and this row is selected).
				var paintLine = ApplySelectionHighlight(i, cellLine);

				// Paint the line content
				buffer.WriteCellsClipped(startX + alignOffset, y, paintLine, clipRect);

				// Fill remaining space (right side)
				int rightPadStart = startX + alignOffset + lineWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
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

			// Fill bottom margin and remaining space
			int contentEndY = startY + renderedCellLines.Count;
			for (int y = contentEndY; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, marginBg);
				}
			}
		}

		#endregion
	}
}
