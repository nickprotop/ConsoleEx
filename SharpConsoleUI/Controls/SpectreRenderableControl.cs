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
	public class SpectreRenderableControl : BaseControl, IMouseAwareControl
	{
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private IRenderable? _renderable;

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
		public override int? ContentWidth
		{
			get
			{
				if (_renderable == null) return Margin.Left + Margin.Right;

				var bgColor = BackgroundColor;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, Width ?? 80, null, bgColor);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + Margin.Left + Margin.Right;
			}
		}

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

		/// <summary>
		/// Gets or sets the Spectre.Console renderable to display.
		/// </summary>
		public IRenderable? Renderable
		{ get => _renderable; set { _renderable = value; Container?.Invalidate(true); } }

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
		public event EventHandler<MouseEventArgs>? MouseRightClick;

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

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			// Scroll events - ALWAYS bubble up (don't consume)
			if (args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown) ||
				args.HasFlag(MouseFlags.WheeledLeft) || args.HasFlag(MouseFlags.WheeledRight))
			{
				return false;  // Let scroll events bubble to parent
			}

			// Handle driver-provided double-click (preferred method)
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				// Reset tracking state since driver handled the gesture
				_clickCount = 0;
				_lastClickTime = DateTime.MinValue;

				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				Container?.Invalidate(true);
				return true;
			}

			// Handle clicks with manual double-click detection (fallback)
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				var now = DateTime.UtcNow;
				var timeSince = (now - _lastClickTime).TotalMilliseconds;

				// Check for double-click before updating state
				bool isDoubleClick = timeSince <= ControlDefaults.DefaultDoubleClickThresholdMs &&
									_clickCount == 1;

				// Update tracking state
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
		protected override void OnDisposing()
		{
			// Clear mouse event handlers to prevent memory leaks
			MouseClick = null;
			MouseDoubleClick = null;
			MouseRightClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
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
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (_renderable == null)
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			// Reuse ContentWidth for width
			int width = ContentWidth ?? 0;

			// Calculate height
			var bgColor = BackgroundColor;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, Width ?? 80, null, bgColor);
			int height = content.Count + Margin.Top + Margin.Bottom;

			return new System.Drawing.Size(width, height);
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
        public override LayoutSize MeasureDOM(LayoutConstraints constraints)
        {
            if (_renderable == null)
            {
                return new LayoutSize(
                    Math.Clamp(Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
                    Math.Clamp(Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
                );
            }

            var bgColor = BackgroundColor;
            int targetWidth = Width ?? constraints.MaxWidth - Margin.Left - Margin.Right;

            var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, targetWidth, null, bgColor);

            int maxWidth = content.Count > 0 ? content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line)) : 0;
            int width = maxWidth + Margin.Left + Margin.Right;
            int height = content.Count + Margin.Top + Margin.Bottom;

            return new LayoutSize(
                Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
                Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
            );
        }


		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			if (_renderable != null)
			{
				int renderWidth = Width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(_renderable, renderWidth, null, bgColor);

				int contentHeight = renderedContent.Count;
				int availableHeight = bounds.Height - Margin.Top - Margin.Bottom;

				for (int i = 0; i < Math.Min(contentHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (Margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, Margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Calculate alignment
						int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
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
						int rightPadWidth = bounds.Right - rightPadStart - Margin.Right;
						if (rightPadWidth > 0)
						{
							buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', fgColor, bgColor);
						}

						// Fill right margin
						if (Margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after content
				for (int y = startY + contentHeight; y < bounds.Bottom - Margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - Margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}
}
