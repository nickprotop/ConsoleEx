// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class BarGraphControl
	{
		private bool _animateValue = false;
		private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(250);

		// The value the BAR (and its color) is currently drawn at. When an animation is running it
		// eases toward the real Value; otherwise it tracks Value exactly. Nullable so a control that
		// has never animated renders straight from Value (zero behavioural change when off).
		private double? _displayValue;
		private IAnimation? _valueAnim;

		private AnimationManager? _testAnimationManager;

		/// <summary>Test-only: injects an animation manager, bypassing the parent-window lookup.</summary>
		internal void SetAnimationManagerForTesting(AnimationManager manager) => _testAnimationManager = manager;

		/// <summary>
		/// Gets or sets whether <see cref="Value"/> changes animate: the bar (and its threshold/gradient
		/// color) smoothly eases to the new value over <see cref="AnimationDuration"/> instead of
		/// snapping. The displayed numeric value still shows the target immediately — only the bar
		/// tweens. Defaults to <c>false</c> (snap), so existing bars are unchanged.
		/// </summary>
		public bool AnimateValue
		{
			get => _animateValue;
			set => SetProperty(ref _animateValue, value);
		}

		/// <summary>Duration of a value transition when <see cref="AnimateValue"/> is enabled.</summary>
		public TimeSpan AnimationDuration
		{
			get => _animationDuration;
			set => SetProperty(ref _animationDuration, value);
		}

		/// <summary>The value the bar is currently drawn at — the animated value while easing, else Value.</summary>
		internal double DisplayValue => _displayValue ?? Value;

		private AnimationManager? GetAnimationManager()
		{
			if (_testAnimationManager != null)
				return _testAnimationManager;
			return (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;
		}

		/// <summary>
		/// Called by the Value setter after the field changes. Starts (or retargets) a tween from the
		/// current display value to <paramref name="target"/>. If animation is off or no manager is
		/// available, the display value snaps to the target. Re-invoking cancels any in-flight tween so
		/// rapid updates retarget smoothly instead of restarting from zero.
		/// </summary>
		private void OnValueChanged(double target)
		{
			if (!_animateValue)
			{
				_displayValue = null; // render straight from Value
				return;
			}

			var manager = GetAnimationManager();
			double from = _displayValue ?? target;

			_valueAnim?.Cancel();

			if (manager == null || _animationDuration <= TimeSpan.Zero || Math.Abs(from - target) < 0.0001)
			{
				_displayValue = target;
				Invalidate(Invalidation.Repaint);
				return;
			}

			_displayValue = from;
			_valueAnim = manager.Animate(
				(float)from, (float)target, _animationDuration, EasingFunctions.EaseOut,
				onUpdate: v => { _displayValue = v; Invalidate(Invalidation.Repaint); },
				onComplete: () => { _displayValue = target; _valueAnim = null; Invalidate(Invalidation.Repaint); });
		}
	}
}
