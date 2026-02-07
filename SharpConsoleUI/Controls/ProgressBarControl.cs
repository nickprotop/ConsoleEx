// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Timers;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;
using Timer = System.Timers.Timer;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A progress bar control with both determinate (percentage) and indeterminate (pulsing) modes.
	/// Uses box-drawing character ━ (U+2501) for clean terminal rendering.
	/// </summary>
	public class ProgressBarControl : IWindowControl, IDOMPaintable, IDisposable
	{
		private const char BAR_CHAR = '━'; // U+2501 - Box Drawings Heavy Horizontal

		// Animation
		private Timer? _animationTimer;
		private int _animationInterval = 100;
		private int _pulsePosition;
		private int _pulseWidth = 5;
		private bool _isIndeterminate;

		// Colors (nullable for theme resolution)
		private Color? _filledColorValue;
		private Color? _unfilledColorValue;
		private Color? _percentageColorValue;
		private Color? _backgroundColorValue;

		// Value properties
		private double _value;
		private double _maxValue = 100.0;

		// Display options
		private int? _barWidth;
		private bool _showPercentage;
		private bool _showHeader;
		private string? _header;

		// Standard control properties
		private IContainer? _container;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgressBarControl"/> class.
		/// </summary>
		public ProgressBarControl()
		{
		}

		#region Value Properties

		/// <summary>
		/// Gets or sets the current progress value (0 to MaxValue).
		/// </summary>
		public double Value
		{
			get => _value;
			set
			{
				_value = Math.Max(0, Math.Min(value, _maxValue));
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the maximum value for the progress bar (default: 100).
		/// </summary>
		public double MaxValue
		{
			get => _maxValue;
			set
			{
				_maxValue = Math.Max(0.01, value);
				Container?.Invalidate(true);
			}
		}

		#endregion

		#region Indeterminate Mode Properties

		/// <summary>
		/// Gets or sets whether the progress bar is in indeterminate mode (pulsing animation).
		/// Setting this to true starts the internal animation timer; false stops it.
		/// </summary>
		public bool IsIndeterminate
		{
			get => _isIndeterminate;
			set
			{
				if (_isIndeterminate == value) return;
				_isIndeterminate = value;

				if (value)
					StartAnimation();
				else
					StopAnimation();

				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the animation interval in milliseconds (default: 100).
		/// Only affects indeterminate mode.
		/// </summary>
		public int AnimationInterval
		{
			get => _animationInterval;
			set
			{
				_animationInterval = Math.Max(10, value);
				if (_animationTimer != null)
				{
					_animationTimer.Interval = _animationInterval;
				}
			}
		}

		/// <summary>
		/// Gets or sets the width of the pulse segment in indeterminate mode (default: 5).
		/// </summary>
		public int PulseWidth
		{
			get => _pulseWidth;
			set
			{
				_pulseWidth = Math.Max(1, value);
				Container?.Invalidate(true);
			}
		}

		#endregion

		#region Display Properties

		/// <summary>
		/// Gets or sets the fixed width of the bar in characters.
		/// When null, the bar stretches to fill available width.
		/// </summary>
		public int? BarWidth
		{
			get => _barWidth;
			set
			{
				_barWidth = value.HasValue ? Math.Max(1, value.Value) : null;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether to show the percentage text after the bar.
		/// Only displayed in determinate mode.
		/// </summary>
		public bool ShowPercentage
		{
			get => _showPercentage;
			set => PropertySetterHelper.SetBoolProperty(ref _showPercentage, value, Container);
		}

		/// <summary>
		/// Gets or sets whether to show the header text above the bar.
		/// </summary>
		public bool ShowHeader
		{
			get => _showHeader;
			set => PropertySetterHelper.SetBoolProperty(ref _showHeader, value, Container);
		}

		/// <summary>
		/// Gets or sets the header text displayed above the bar.
		/// </summary>
		public string? Header
		{
			get => _header;
			set
			{
				_header = value;
				Container?.Invalidate(true);
			}
		}

		#endregion

		#region Color Properties

		/// <summary>
		/// Gets or sets the color for the filled portion of the bar.
		/// When null, uses theme color or default (Cyan1).
		/// </summary>
		public Color? FilledColor
		{
			get => _filledColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ProgressBarFilledColor ?? Color.Cyan1;
			set => PropertySetterHelper.SetColorProperty(ref _filledColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the color for the unfilled portion of the bar.
		/// When null, uses theme color or default (Grey35).
		/// </summary>
		public Color? UnfilledColor
		{
			get => _unfilledColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ProgressBarUnfilledColor ?? Color.Grey35;
			set => PropertySetterHelper.SetColorProperty(ref _unfilledColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the color for the percentage text.
		/// When null, uses theme color or default (White).
		/// </summary>
		public Color? PercentageColor
		{
			get => _percentageColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ProgressBarPercentageColor ?? Color.White;
			set => PropertySetterHelper.SetColorProperty(ref _percentageColorValue, value, Container);
		}

		/// <summary>
		/// Gets or sets the background color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => PropertySetterHelper.SetColorProperty(ref _backgroundColorValue, value, Container);
		}

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ContentWidth => _width;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				// Stop animation when removed from container
				if (value == null && _container != null)
					StopAnimation();

				_container = value;
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Container?.Invalidate(true);
			}
		}

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
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
				if (_width != validatedValue)
				{
					_width = validatedValue;
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public Size GetLogicalContentSize()
		{
			int contentHeight = _showHeader && !string.IsNullOrEmpty(_header) ? 2 : 1;
			int contentWidth = CalculateContentWidth(50); // Use default width for logical size
			return new Size(contentWidth + _margin.Left + _margin.Right, contentHeight + _margin.Top + _margin.Bottom);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(false);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			StopAnimation();
			Container = null;
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int contentHeight = _showHeader && !string.IsNullOrEmpty(_header) ? 2 : 1;
			int height = contentHeight + _margin.Top + _margin.Bottom;

			int contentWidth;
			if (_barWidth.HasValue)
			{
				contentWidth = CalculateContentWidth(_barWidth.Value);
			}
			else
			{
				// Stretch mode: use max available width
				contentWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
			}

			int width = contentWidth + _margin.Left + _margin.Right;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			// Resolve colors
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color filledColor = FilledColor ?? Color.Cyan1;
			Color unfilledColor = UnfilledColor ?? Color.Grey35;
			Color percentageColor = PercentageColor ?? Color.White;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;
			int currentY = startY;

			// Fill entire bounds with background first
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, defaultFg, bgColor);

			// Paint header if visible
			if (_showHeader && !string.IsNullOrEmpty(_header))
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					// Fill the header line
					buffer.FillRect(new LayoutRect(bounds.X, currentY, bounds.Width, 1), ' ', defaultFg, bgColor);

					// Paint header text
					int headerX = startX;
					foreach (char c in _header)
					{
						if (headerX >= clipRect.X && headerX < clipRect.Right && headerX < bounds.Right)
						{
							buffer.SetCell(headerX, currentY, c, defaultFg, bgColor);
						}
						headerX++;
					}
				}
				currentY++;
			}

			// Calculate bar width
			int availableWidth = bounds.Width - _margin.Left - _margin.Right;
			int barWidth = _barWidth ?? availableWidth;

			// Subtract percentage text width if showing percentage
			if (_showPercentage && !_isIndeterminate)
			{
				string percentText = GetPercentageText();
				barWidth = Math.Max(1, barWidth - percentText.Length - 1); // -1 for space
			}

			barWidth = Math.Max(1, Math.Min(barWidth, availableWidth));

			// Paint bar line
			if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
			{
				// Fill the bar line background
				buffer.FillRect(new LayoutRect(bounds.X, currentY, bounds.Width, 1), ' ', defaultFg, bgColor);

				int currentX = startX;

				if (_isIndeterminate)
				{
					// Indeterminate mode: paint pulsing bar
					PaintIndeterminateBar(buffer, clipRect, bounds, currentX, currentY, barWidth, filledColor, unfilledColor, bgColor);
				}
				else
				{
					// Determinate mode: paint filled/unfilled portions
					PaintDeterminateBar(buffer, clipRect, bounds, currentX, currentY, barWidth, filledColor, unfilledColor, bgColor);
					currentX = startX + barWidth;

					// Paint percentage if showing
					if (_showPercentage)
					{
						currentX++; // Space before percentage
						string percentText = GetPercentageText();
						foreach (char c in percentText)
						{
							if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
							{
								buffer.SetCell(currentX, currentY, c, percentageColor, bgColor);
							}
							currentX++;
						}
					}
				}
			}
			currentY++;

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, currentY, defaultFg, bgColor);
		}

		#endregion

		#region Private Methods

		private void PaintDeterminateBar(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int startX, int y, int barWidth, Color filledColor, Color unfilledColor, Color bgColor)
		{
			double percent = Math.Clamp(_value / _maxValue, 0.0, 1.0);
			int filledChars = (int)Math.Round(percent * barWidth);
			int unfilledChars = barWidth - filledChars;

			int currentX = startX;

			// Paint filled portion
			for (int i = 0; i < filledChars; i++)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					buffer.SetCell(currentX, y, BAR_CHAR, filledColor, bgColor);
				}
				currentX++;
			}

			// Paint unfilled portion
			for (int i = 0; i < unfilledChars; i++)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					buffer.SetCell(currentX, y, BAR_CHAR, unfilledColor, bgColor);
				}
				currentX++;
			}
		}

		private void PaintIndeterminateBar(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int startX, int y, int barWidth, Color filledColor, Color unfilledColor, Color bgColor)
		{
			int pulseStart = _pulsePosition % barWidth;

			for (int i = 0; i < barWidth; i++)
			{
				int currentX = startX + i;
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					// Check if this position is within the pulse
					bool inPulse = false;
					int pulseEnd = pulseStart + _pulseWidth;

					if (pulseEnd <= barWidth)
					{
						// Pulse doesn't wrap
						inPulse = i >= pulseStart && i < pulseEnd;
					}
					else
					{
						// Pulse wraps around
						inPulse = i >= pulseStart || i < (pulseEnd % barWidth);
					}

					Color charColor = inPulse ? filledColor : unfilledColor;
					buffer.SetCell(currentX, y, BAR_CHAR, charColor, bgColor);
				}
			}
		}

		private string GetPercentageText()
		{
			double percent = Math.Clamp((_value / _maxValue) * 100, 0.0, 100.0);
			return $"{percent:F0}%";
		}

		private int CalculateContentWidth(int barWidth)
		{
			int width = barWidth;

			if (_showPercentage && !_isIndeterminate)
			{
				width += 1 + GetPercentageText().Length; // Space + percentage
			}

			return width;
		}

		private void StartAnimation()
		{
			StopAnimation(); // Ensure no duplicate timers

			_animationTimer = new Timer(_animationInterval);
			_animationTimer.Elapsed += OnAnimationTick;
			_animationTimer.AutoReset = true;
			_animationTimer.Start();
		}

		private void StopAnimation()
		{
			if (_animationTimer != null)
			{
				_animationTimer.Stop();
				_animationTimer.Elapsed -= OnAnimationTick;
				_animationTimer.Dispose();
				_animationTimer = null;
			}
			_pulsePosition = 0;
		}

		private void OnAnimationTick(object? sender, ElapsedEventArgs e)
		{
			// Guard: don't update if disposed or no container
			if (_animationTimer == null || Container == null) return;

			_pulsePosition++;
			Container?.Invalidate(true);
		}

		#endregion
	}
}
