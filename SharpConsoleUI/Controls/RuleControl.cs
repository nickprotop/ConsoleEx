// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders a horizontal rule (divider line) with optional title text.
	/// Renders directly to CharacterBuffer using BoxChars.
	/// </summary>
	public class RuleControl : BaseControl
	{
		private Color? _color;
		private string? _title;
		private TextJustification _titleAlignment = TextJustification.Left;
		private BorderStyle _borderStyle = BorderStyle.Single;

		// Progress fields
		private ColorGradient? _progressGradient;
		private float _progressRatio;
		private bool _isIndeterminate;
		private float _shimmerPosition = -0.15f;
		private IAnimation? _shimmerAnimation;
		private IAnimation? _clearAnimation;
		private AnimationManager? _testAnimationManager;

		/// <summary>
		/// Initializes a new instance of the <see cref="RuleControl"/> class.
		/// </summary>
		public RuleControl()
		{
		}

		/// <inheritdoc/>
		public override int? ContentWidth => (Width ?? 80) + Margin.Left + Margin.Right;

		/// <summary>
		/// Gets or sets the color of the rule line.
		/// </summary>
		public Color? Color
		{
			get => _color;
			set => SetProperty(ref _color, value);
		}

		/// <summary>
		/// Gets or sets the title text displayed within the rule.
		/// </summary>
		public string? Title
		{
			get => _title;
			set => SetProperty(ref _title, value);
		}

		/// <summary>
		/// Gets or sets the horizontal alignment of the title within the rule.
		/// </summary>
		public TextJustification TitleAlignment
		{
			get => _titleAlignment;
			set => SetProperty(ref _titleAlignment, value);
		}

		/// <summary>
		/// Gets or sets the border style for the rule line characters.
		/// </summary>
		public BorderStyle BorderStyle
		{
			get => _borderStyle;
			set => SetProperty(ref _borderStyle, value);
		}

		/// <summary>
		/// Gets the current progress ratio (0.0 to 1.0).
		/// </summary>
		public float ProgressRatio => _progressRatio;

		/// <summary>
		/// Gets whether progress or indeterminate mode is active.
		/// </summary>
		public bool IsProgressActive => _progressRatio > 0 || _isIndeterminate;

		/// <summary>
		/// Gets whether indeterminate shimmer mode is active.
		/// </summary>
		public bool IsIndeterminate => _isIndeterminate;

		/// <summary>
		/// Shows determinate progress. Fills the rule left-to-right with the gradient up to the ratio.
		/// Cancels any active shimmer or clear animation.
		/// </summary>
		/// <param name="ratio">Progress ratio, clamped to [0,1].</param>
		/// <param name="gradient">Color gradient to apply to the filled portion.</param>
		public void SetProgress(float ratio, ColorGradient gradient)
		{
			_shimmerAnimation?.Cancel();
			_shimmerAnimation = null;
			_clearAnimation?.Cancel();
			_clearAnimation = null;

			_isIndeterminate = false;
			_progressRatio = Math.Clamp(ratio, 0f, 1f);
			_progressGradient = gradient;
			_shimmerPosition = -0.15f;
			Invalidate();
		}

		/// <summary>
		/// Shows indeterminate shimmer. A gradient segment sweeps left to right in a loop.
		/// </summary>
		/// <param name="gradient">Color gradient for the shimmer segment.</param>
		/// <param name="cycleDuration">Duration of one sweep cycle. Default: 1500ms.</param>
		public void SetIndeterminate(ColorGradient gradient, TimeSpan? cycleDuration = null)
		{
			_shimmerAnimation?.Cancel();
			_shimmerAnimation = null;
			_clearAnimation?.Cancel();
			_clearAnimation = null;

			_isIndeterminate = true;
			_progressRatio = 0;
			_progressGradient = gradient;
			_shimmerPosition = -0.15f;

			var manager = GetAnimationManager();
			if (manager == null)
			{
				Invalidate();
				return;
			}

			var duration = cycleDuration ?? TimeSpan.FromMilliseconds(1500);
			StartShimmerCycle(manager, duration);
		}

		/// <summary>
		/// Clears progress. Cancels shimmer. Optionally fades the progress to zero.
		/// </summary>
		/// <param name="fadeDuration">Fade duration. Default: 300ms. Pass TimeSpan.Zero for immediate.</param>
		public void ClearProgress(TimeSpan? fadeDuration = null)
		{
			_shimmerAnimation?.Cancel();
			_shimmerAnimation = null;
			_clearAnimation?.Cancel();
			_clearAnimation = null;
			_isIndeterminate = false;

			var duration = fadeDuration ?? TimeSpan.FromMilliseconds(300);
			var manager = GetAnimationManager();

			if (manager == null || duration <= TimeSpan.Zero)
			{
				_progressRatio = 0;
				_progressGradient = null;
				_shimmerPosition = -0.15f;
				Invalidate();
				return;
			}

			var startRatio = _progressRatio;
			_clearAnimation = manager.Animate(
				startRatio,
				0f,
				duration,
				EasingFunctions.EaseOut,
				value =>
				{
					_progressRatio = value;
					Invalidate();
				},
				() =>
				{
					_progressRatio = 0;
					_progressGradient = null;
					_shimmerPosition = -0.15f;
					_clearAnimation = null;
					Invalidate();
				});
		}

		/// <summary>
		/// Sets the animation manager for testing purposes (bypasses window lookup).
		/// </summary>
		internal void SetAnimationManagerForTesting(AnimationManager manager)
		{
			_testAnimationManager = manager;
		}

		private AnimationManager? GetAnimationManager()
		{
			if (_testAnimationManager != null)
				return _testAnimationManager;

			return (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;
		}

		private void StartShimmerCycle(AnimationManager manager, TimeSpan duration)
		{
			_shimmerAnimation = manager.Animate(
				-0.15f,
				1.0f,
				duration,
				EasingFunctions.Linear,
				value =>
				{
					_shimmerPosition = value;
					Invalidate();
				},
				() =>
				{
					if (_isIndeterminate)
					{
						_shimmerPosition = -0.15f;
						StartShimmerCycle(manager, duration);
					}
				});
		}

		/// <summary>
		/// Creates a new builder for configuring a RuleControl
		/// </summary>
		/// <returns>A new builder instance</returns>
		public static Builders.RuleBuilder Create()
		{
			return new Builders.RuleBuilder();
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = Width ?? constraints.MaxWidth;
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width + Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = Container?.ForegroundColor ?? defaultFg;
			var effectiveBg = SharpConsoleUI.Color.Transparent;
			var ruleColor = _color ?? fgColor;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int ruleWidth = Width ?? targetWidth;
			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Paint the rule line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);
				}

				var box = BoxChars.FromBorderStyle(_borderStyle);
				char horizChar = box.Horizontal;

				if (string.IsNullOrEmpty(_title))
				{
					// No title — fill entire line with horizontal chars (progress-aware)
					for (int x = 0; x < ruleWidth; x++)
					{
						int px = startX + x;
						if (px >= clipRect.X && px < clipRect.Right)
						{
							var cellColor = ruleColor;
							if (_progressGradient != null && ruleWidth > 1)
							{
								float normalized = (float)x / (ruleWidth - 1);
								if (_isIndeterminate && _shimmerAnimation != null)
								{
									float shimmerEnd = _shimmerPosition + 0.15f;
									if (normalized >= _shimmerPosition && normalized <= shimmerEnd)
									{
										float segmentNorm = (normalized - _shimmerPosition) / 0.15f;
										cellColor = _progressGradient.Interpolate(segmentNorm);
									}
								}
								else if (_progressRatio > 0 && normalized <= _progressRatio)
								{
									float fillNorm = _progressRatio > 0 ? normalized / _progressRatio : 0f;
									cellColor = _progressGradient.Interpolate(fillNorm);
								}
							}
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(px, startY, horizChar, cellColor, cellBg);
						}
					}
				}
				else
				{
					// Parse title to get styled cells and measure visible length
					var titleCells = MarkupParser.Parse(_title, ruleColor, effectiveBg);
					int titleLen = titleCells.Count;

					// Add spaces around title: ─ Title ─
					int titleWithSpaces = titleLen + 2; // space before and after title
					int dashSpace = ruleWidth - titleWithSpaces;

					if (dashSpace < 2)
					{
						// Not enough room for dashes — just fill with horizontal chars
						for (int x = 0; x < ruleWidth; x++)
						{
							int px = startX + x;
							if (px >= clipRect.X && px < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(px, startY, horizChar, ruleColor, cellBg);
							}
						}
					}
					else
					{
						int leftDashes, rightDashes;
						switch (_titleAlignment)
						{
							case TextJustification.Center:
								leftDashes = dashSpace / 2;
								rightDashes = dashSpace - leftDashes;
								break;
							case TextJustification.Right:
								leftDashes = dashSpace - 1;
								rightDashes = 1;
								break;
							default: // Left
								leftDashes = 1;
								rightDashes = dashSpace - 1;
								break;
						}

						int writeX = startX;

						// Left dashes
						for (int i = 0; i < leftDashes; i++)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(writeX, startY, horizChar, ruleColor, cellBg);
							}
							writeX++;
						}

						// Space before title
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(writeX, startY, ' ', ruleColor, cellBg);
						}
						writeX++;

						// Title cells (with their own colors from markup)
						foreach (var cell in titleCells)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								buffer.SetCell(writeX, startY, cell);
							}
							writeX++;
						}

						// Space after title
						if (writeX >= clipRect.X && writeX < clipRect.Right)
						{
							var cellBg = effectiveBg;
							buffer.SetNarrowCell(writeX, startY, ' ', ruleColor, cellBg);
						}
						writeX++;

						// Right dashes
						for (int i = 0; i < rightDashes; i++)
						{
							if (writeX >= clipRect.X && writeX < clipRect.Right)
							{
								var cellBg = effectiveBg;
								buffer.SetNarrowCell(writeX, startY, horizChar, ruleColor, cellBg);
							}
							writeX++;
						}
					}
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, startY, Margin.Right, 1), fgColor, effectiveBg);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, effectiveBg);
		}

		#endregion
	}
}
