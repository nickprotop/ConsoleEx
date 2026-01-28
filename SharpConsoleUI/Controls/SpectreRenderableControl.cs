// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that wraps any Spectre.Console IRenderable for display within the window system.
	/// Provides a bridge between Spectre.Console's rich rendering and the SharpConsoleUI framework.
	/// </summary>
	public class SpectreRenderableControl : IWindowControl, IDOMPaintable, IMouseAwareControl
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private IRenderable? _renderable;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Mouse interaction state
		private bool _wantsMouseEvents = true;
		private bool _canFocusWithMouse = false;
		private bool _isMouseInside = false;
		private DateTime _lastClickTime = DateTime.MinValue;
		private int _clickCount = 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpectreRenderableControl"/> class.
		/// </summary>
		public SpectreRenderableControl()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SpectreRenderableControl"/> class with a renderable.
		/// </summary>
		/// <param name="renderable">The Spectre.Console renderable to display.</param>
		public SpectreRenderableControl(IRenderable renderable)
		{
			_renderable = renderable;
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				if (_renderable == null) return _margin.Left + _margin.Right;

				var bgColor = BackgroundColor;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, _width ?? 80, null, bgColor);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the background color for rendering.
		/// Falls back to container or theme colors if not explicitly set.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <summary>
		/// Gets or sets the foreground color for rendering.
		/// Falls back to theme colors if not explicitly set.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <summary>
		/// Gets or sets the Spectre.Console renderable to display.
		/// </summary>
		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <inheritdoc/>
		public bool WantsMouseEvents
		{
			get => _wantsMouseEvents;
			set
			{
				if (_wantsMouseEvents == value) return;
				_wantsMouseEvents = value;
			}
		}

		/// <inheritdoc/>
		public bool CanFocusWithMouse
		{
			get => _canFocusWithMouse;
			set
			{
				if (_canFocusWithMouse == value) return;
				_canFocusWithMouse = value;
			}
		}

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!WantsMouseEvents)
				return false;

			if (args.Handled)
				return false;

			// Handle mouse leave
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_isMouseInside)
				{
					_isMouseInside = false;
					MouseLeave?.Invoke(this, args);
					Container?.Invalidate(true);
				}
				return true;
			}

			// Handle mouse enter
			if (!_isMouseInside && args.HasFlag(MouseFlags.ReportMousePosition))
			{
				_isMouseInside = true;
				MouseEnter?.Invoke(this, args);
				Container?.Invalidate(true);
			}

			// Scroll events - ALWAYS bubble up (don't consume)
			if (args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown) ||
				args.HasFlag(MouseFlags.WheeledLeft) || args.HasFlag(MouseFlags.WheeledRight))
			{
				return false;  // Let scroll events bubble to parent
			}

			// Handle driver-provided double-click
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle clicks with manual double-click detection
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				var now = DateTime.UtcNow;
				var timeSince = (now - _lastClickTime).TotalMilliseconds;
				bool isDoubleClick = timeSince <= ControlDefaults.DefaultDoubleClickThresholdMs &&
									_clickCount == 1;

				if (isDoubleClick)
				{
					_clickCount = 0;
					_lastClickTime = DateTime.MinValue;
					MouseDoubleClick?.Invoke(this, args);
				}
				else
				{
					_clickCount = 1;
					_lastClickTime = now;
					MouseClick?.Invoke(this, args);
				}

				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle mouse movement
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}

			return false;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// Clear mouse event handlers to prevent memory leaks
			MouseClick = null;
			MouseDoubleClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;

			Container = null;
		}

		/// <summary>
		/// Creates a new builder for configuring a SpectreRenderableControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.SpectreRenderableBuilder Create()
		{
			return new Builders.SpectreRenderableBuilder();
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			if (_renderable == null)
				return new System.Drawing.Size(_margin.Left + _margin.Right, _margin.Top + _margin.Bottom);

			var bgColor = BackgroundColor;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, _width ?? 80, null, bgColor);

			int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
			return new System.Drawing.Size(
				maxWidth + _margin.Left + _margin.Right,
				content.Count + _margin.Top + _margin.Bottom
			);
		}

		/// <summary>
		/// Sets the Spectre.Console renderable to display.
		/// </summary>
		/// <param name="renderable">The renderable to display.</param>
		public void SetRenderable(IRenderable renderable)
		{
			_renderable = renderable;
			Container?.Invalidate(true);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
        public LayoutSize MeasureDOM(LayoutConstraints constraints)
        {
            if (_renderable == null)
            {
                return new LayoutSize(
                    Math.Clamp(_margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
                    Math.Clamp(_margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
                );
            }

            var bgColor = BackgroundColor;
            int targetWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;

            var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, targetWidth, null, bgColor);

            int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
            int width = maxWidth + _margin.Left + _margin.Right;
            int height = content.Count + _margin.Top + _margin.Bottom;

            return new LayoutSize(
                Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
                Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
            );
        }


		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			int targetWidth = bounds.Width - _margin.Left - _margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			if (_renderable != null)
			{
				int renderWidth = _width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, renderWidth, null, bgColor);

				int contentHeight = renderedContent.Count;
				int availableHeight = bounds.Height - _margin.Top - _margin.Bottom;

				for (int i = 0; i < Math.Min(contentHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Calculate alignment
						int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
						int alignOffset = 0;
						if (lineWidth < targetWidth)
						{
							switch (_horizontalAlignment)
							{
								case HorizontalAlignment.Center:
									alignOffset = (targetWidth - lineWidth) / 2;
									break;
								case HorizontalAlignment.Right:
									alignOffset = targetWidth - lineWidth;
									break;
							}
						}

						// Fill left alignment padding
						if (alignOffset > 0)
						{
							buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', fgColor, bgColor);
						}

						// Parse and write the content line
						var cells = AnsiParser.Parse(renderedContent[i], fgColor, bgColor);
						buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

						// Fill right padding
						int rightPadStart = startX + alignOffset + lineWidth;
						int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
						if (rightPadWidth > 0)
						{
							buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', fgColor, bgColor);
						}

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after content
				for (int y = startY + contentHeight; y < bounds.Bottom - _margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}
}
