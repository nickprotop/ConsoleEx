// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Timers;
using Size = System.Drawing.Size;
using Timer = System.Timers.Timer;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A progress bar control with both determinate (percentage) and indeterminate (pulsing) modes.
	/// Uses box-drawing character ━ (U+2501) for clean terminal rendering.
	/// </summary>
	public class ProgressBarControl : BaseControl
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

		// Container backing field for custom override
		private IContainer? _container;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgressBarControl"/> class.
		/// </summary>
		public ProgressBarControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
		}

		#region Value Properties

		/// <summary>
		/// Gets or sets the current progress value (0 to MaxValue).
		/// </summary>
		public double Value
		{
			get => _value;
			set => SetProperty(ref _value, value, v => Math.Max(0, Math.Min(v, _maxValue)));
		}

		/// <summary>
		/// Gets or sets the maximum value for the progress bar (default: 100).
		/// </summary>
		public double MaxValue
		{
			get => _maxValue;
			set => SetProperty(ref _maxValue, value, v => Math.Max(0.01, v));
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
				OnPropertyChanged();

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
				OnPropertyChanged();
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
			set => SetProperty(ref _pulseWidth, value, v => Math.Max(1, v));
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
			set => SetProperty(ref _barWidth, value, v => v.HasValue ? Math.Max(1, v.Value) : null);
		}

		/// <summary>
		/// Gets or sets whether to show the percentage text after the bar.
		/// Only displayed in determinate mode.
		/// </summary>
		public bool ShowPercentage
		{
			get => _showPercentage;
			set => SetProperty(ref _showPercentage, value);
		}

		/// <summary>
		/// Gets or sets whether to show the header text above the bar.
		/// </summary>
		public bool ShowHeader
		{
			get => _showHeader;
			set => SetProperty(ref _showHeader, value);
		}

		/// <summary>
		/// Gets or sets the header text displayed above the bar.
		/// </summary>
		public string? Header
		{
			get => _header;
			set => SetProperty(ref _header, value);
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
			set => SetProperty(ref _filledColorValue, value);
		}

		/// <summary>
		/// Gets or sets the color for the unfilled portion of the bar.
		/// When null, uses theme color or default (Grey35).
		/// </summary>
		public Color? UnfilledColor
		{
			get => _unfilledColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ProgressBarUnfilledColor ?? Color.Grey35;
			set => SetProperty(ref _unfilledColorValue, value);
		}

		/// <summary>
		/// Gets or sets the color for the percentage text.
		/// When null, uses theme color or default (White).
		/// </summary>
		public Color? PercentageColor
		{
			get => _percentageColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ProgressBarPercentageColor ?? Color.White;
			set => SetProperty(ref _percentageColorValue, value);
		}

		/// <summary>
		/// Gets or sets the background color.
		/// When null, inherits from the container.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set => SetProperty(ref _backgroundColorValue, value);
		}

		#endregion

		#region BaseControl Overrides

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				// Stop animation when removed from container
				if (value == null && _container != null)
					StopAnimation();

				_container = value;
				OnPropertyChanged();
				_container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public override int? Width
		{
			get => base.Width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : (int?)null;
				if (base.Width != validatedValue)
				{
					base.Width = validatedValue;
				}
			}
		}

		/// <inheritdoc/>
		public override Size GetLogicalContentSize()
		{
			int contentHeight = _showHeader && !string.IsNullOrEmpty(_header) ? 2 : 1;
			int contentWidth = CalculateContentWidth(50); // Use default width for logical size
			return new Size(contentWidth + Margin.Left + Margin.Right, contentHeight + Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			StopAnimation();
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int contentHeight = _showHeader && !string.IsNullOrEmpty(_header) ? 2 : 1;
			int height = contentHeight + Margin.Top + Margin.Bottom;

			int contentWidth;
			if (_barWidth.HasValue)
			{
				contentWidth = CalculateContentWidth(_barWidth.Value);
			}
			else
			{
				// Stretch mode: use max available width
				contentWidth = constraints.MaxWidth - Margin.Left - Margin.Right;
			}

			int width = contentWidth + Margin.Left + Margin.Right;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Resolve colors
			Color bgColor = _backgroundColorValue ?? Container?.BackgroundColor ?? defaultBg;
			Color filledColor = FilledColor ?? Color.Cyan1;
			Color unfilledColor = UnfilledColor ?? Color.Grey35;
			Color percentageColor = PercentageColor ?? Color.White;
			bool preserveBg = Container?.HasGradientBackground ?? false;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int currentY = startY;

			// Fill entire bounds with background first
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, defaultFg, bgColor, preserveBg);

			// Paint header if visible
			if (_showHeader && !string.IsNullOrEmpty(_header))
			{
				if (currentY >= clipRect.Y && currentY < clipRect.Bottom && currentY < bounds.Bottom)
				{
					// Fill the header line
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor, preserveBg);

					// Paint header text
					int headerX = startX;
					Cell? pendingHeaderCell = null;
					int pendingHeaderX = 0;
					foreach (var rune in _header.EnumerateRunes())
					{
						int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
						// VS16 widens certain emoji from 1→2 columns
						if (rw == 0 && Helpers.UnicodeWidth.IsVS16(rune) && pendingHeaderCell != null
							&& Helpers.UnicodeWidth.IsVs16Widened(pendingHeaderCell.Value.Character)
							&& !Helpers.UnicodeWidth.IsWideRune(pendingHeaderCell.Value.Character))
						{
							// Widen: emit base cell + continuation
							var widened = pendingHeaderCell.Value;
							widened.AppendCombiner(rune);
							if (pendingHeaderX >= clipRect.X && pendingHeaderX < clipRect.Right && pendingHeaderX < bounds.Right)
								buffer.SetCell(pendingHeaderX, currentY, widened);
							if (headerX >= clipRect.X && headerX < clipRect.Right && headerX < bounds.Right)
							{
								Color cellBg = preserveBg ? buffer.GetCell(headerX, currentY).Background : widened.Background;
								buffer.SetCell(headerX, currentY, new Cell(' ', defaultFg, cellBg) { IsWideContinuation = true });
							}
							headerX++;
							pendingHeaderCell = null;
							continue;
						}
						// Flush pending cell
						if (pendingHeaderCell != null)
						{
							if (pendingHeaderX >= clipRect.X && pendingHeaderX < clipRect.Right && pendingHeaderX < bounds.Right)
								buffer.SetCell(pendingHeaderX, currentY, pendingHeaderCell.Value);
							pendingHeaderCell = null;
						}
						if (rw == 0)
						{
							// Other zero-width: attach as combiner to last painted cell
							continue;
						}
						Color hCellBg = preserveBg ? buffer.GetCell(headerX, currentY).Background : bgColor;
						if (rw == 2)
						{
							if (headerX >= clipRect.X && headerX < clipRect.Right && headerX < bounds.Right)
								buffer.SetCell(headerX, currentY, new Cell(rune, defaultFg, hCellBg));
							if (headerX + 1 < bounds.Right)
								buffer.SetCell(headerX + 1, currentY, new Cell(' ', defaultFg, hCellBg) { IsWideContinuation = true });
							headerX += 2;
						}
						else
						{
							// Hold width-1 cell as pending for potential VS16 widening
							pendingHeaderCell = new Cell(rune, defaultFg, hCellBg);
							pendingHeaderX = headerX;
							headerX += 1;
						}
					}
					// Flush last pending header cell
					if (pendingHeaderCell != null)
					{
						if (pendingHeaderX >= clipRect.X && pendingHeaderX < clipRect.Right && pendingHeaderX < bounds.Right)
							buffer.SetCell(pendingHeaderX, currentY, pendingHeaderCell.Value);
					}
				}
				currentY++;
			}

			// Calculate bar width
			int availableWidth = bounds.Width - Margin.Left - Margin.Right;
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
				ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, currentY, bounds.Width, 1), defaultFg, bgColor, preserveBg);

				int currentX = startX;

				if (_isIndeterminate)
				{
					// Indeterminate mode: paint pulsing bar
					PaintIndeterminateBar(buffer, clipRect, bounds, currentX, currentY, barWidth, filledColor, unfilledColor, bgColor, preserveBg);
				}
				else
				{
					// Determinate mode: paint filled/unfilled portions
					PaintDeterminateBar(buffer, clipRect, bounds, currentX, currentY, barWidth, filledColor, unfilledColor, bgColor, preserveBg);
					currentX = startX + barWidth;

					// Paint percentage if showing
					if (_showPercentage)
					{
						currentX++; // Space before percentage
						string percentText = GetPercentageText();
						// Percentage text is always ASCII, no VS16 handling needed
						foreach (var rune in percentText.EnumerateRunes())
						{
							int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
							if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
							{
								Color cellBg = preserveBg ? buffer.GetCell(currentX, currentY).Background : bgColor;
								buffer.SetCell(currentX, currentY, new Cell(rune, percentageColor, cellBg));
								if (rw == 2 && currentX + 1 < bounds.Right)
									buffer.SetCell(currentX + 1, currentY, new Cell(' ', percentageColor, cellBg) { IsWideContinuation = true });
							}
							currentX += rw;
						}
					}
				}
			}
			currentY++;

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, currentY, defaultFg, bgColor, preserveBg);
		}

		#endregion

		#region Private Methods

		private void PaintDeterminateBar(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int startX, int y, int barWidth, Color filledColor, Color unfilledColor, Color bgColor, bool preserveBg)
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
					Color cellBg = preserveBg ? buffer.GetCell(currentX, y).Background : bgColor;
					buffer.SetCell(currentX, y, BAR_CHAR, filledColor, cellBg);
				}
				currentX++;
			}

			// Paint unfilled portion
			for (int i = 0; i < unfilledChars; i++)
			{
				if (currentX >= clipRect.X && currentX < clipRect.Right && currentX < bounds.Right)
				{
					Color cellBg = preserveBg ? buffer.GetCell(currentX, y).Background : bgColor;
					buffer.SetCell(currentX, y, BAR_CHAR, unfilledColor, cellBg);
				}
				currentX++;
			}
		}

		private void PaintIndeterminateBar(CharacterBuffer buffer, LayoutRect clipRect, LayoutRect bounds,
			int startX, int y, int barWidth, Color filledColor, Color unfilledColor, Color bgColor, bool preserveBg)
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
					Color cellBg = preserveBg ? buffer.GetCell(currentX, y).Background : bgColor;
					buffer.SetCell(currentX, y, BAR_CHAR, charColor, cellBg);
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
