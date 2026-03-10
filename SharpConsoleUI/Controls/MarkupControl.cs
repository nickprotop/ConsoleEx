// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that displays rich text content using Spectre.Console markup syntax.
	/// Supports text alignment, margins, word wrapping, and sticky positioning.
	/// </summary>
	public class MarkupControl : BaseControl, IMouseAwareControl
	{
		private List<string> _content;
		private readonly object _contentLock = new();
		private bool _wrap = true;
		private Color? _backgroundColor = null;
		private Color? _foregroundColor = null;

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
		/// Processes mouse events for this control.
		/// </summary>
		/// <param name="args">The mouse event arguments.</param>
		/// <returns>True if the event was handled; otherwise, false.</returns>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
		if (!WantsMouseEvents || args.Handled)
		return false;

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

			// Calculate content dimensions
			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }
			int maxContentWidth = 0;
			int totalLines = 0;

			foreach (var line in snapshot)
			{
				// Split by embedded newlines to count actual rendered lines
				var subLines = line.Split('\n');
				foreach (var subLine in subLines)
				{
					int lineWidth = Parsing.MarkupParser.StripLength(subLine);
					maxContentWidth = Math.Max(maxContentWidth, lineWidth);

					if (_wrap && lineWidth > targetWidth && targetWidth > 0)
					{
						// Estimate wrapped lines
						totalLines += (int)Math.Ceiling((double)lineWidth / targetWidth);
					}
					else
					{
						totalLines++;
					}
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

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			Color bgColor = Container?.BackgroundColor ?? defaultBg;
			Color fgColor = Container?.ForegroundColor ?? defaultFg;
			bool preserveBg = Container?.HasGradientBackground ?? false;

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

			// Render content lines
			Color effectiveFg = _foregroundColor ?? fgColor;
			Color effectiveBg = _backgroundColor ?? bgColor;
			var renderedCellLines = new List<List<Cell>>();
			foreach (var line in snapshot)
			{
				int renderWidth = (HorizontalAlignment == HorizontalAlignment.Center || HorizontalAlignment == HorizontalAlignment.Right)
					? Math.Min(maxContentWidth, targetWidth)
					: targetWidth;

				if (_wrap)
				{
					var wrappedLines = Parsing.MarkupParser.ParseLines(line, renderWidth, effectiveFg, effectiveBg);
					renderedCellLines.AddRange(wrappedLines);
				}
				else
				{
					var cells = Parsing.MarkupParser.Parse(line, effectiveFg, effectiveBg);
					renderedCellLines.Add(cells);
				}
			}

			// Paint with margins
			int startY = bounds.Y + Margin.Top;
			int startX = bounds.X + Margin.Left;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor, preserveBg);

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
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, bgColor, preserveBg);
				}

				// Fill alignment padding (left side)
				if (alignOffset > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, y, alignOffset, 1), fgColor, bgColor, preserveBg);
				}

				// Paint the line content
				if (preserveBg)
					buffer.WriteCellsClippedPreservingBackground(startX + alignOffset, y, cellLine, clipRect, effectiveBg);
				else
					buffer.WriteCellsClipped(startX + alignOffset, y, cellLine, clipRect);

				// Fill remaining space (right side)
				int rightPadStart = startX + alignOffset + lineWidth;
				int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
				if (rightPadWidth > 0)
				{
					// Use the control's background color if set, otherwise container's
					Color fillBg = _backgroundColor ?? bgColor;
					bool preserveRightFill = _backgroundColor == null && preserveBg;
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightPadStart, y, rightPadWidth, 1), fgColor, fillBg, preserveRightFill);
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, y, Margin.Right, 1), fgColor, bgColor, preserveBg);
				}
			}

			// Fill bottom margin and remaining space
			int contentEndY = startY + renderedCellLines.Count;
			for (int y = contentEndY; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, bgColor, preserveBg);
				}
			}
		}

		#endregion
	}
}
