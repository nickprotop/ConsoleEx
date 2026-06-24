// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class CollapsiblePanel
	{
		private AnimationManager? _testAnimationManager;
		private int? _animatedBodyHeight; // null = not animating (layout uses natural/full height)

		/// <summary>
		/// Gets or sets whether expand/collapse animates the body height.
		/// Defaults to <see cref="CollapsibleAnimationMode.None"/> (instant re-layout).
		/// When set to <see cref="CollapsibleAnimationMode.Height"/>, the body height tweens
		/// open/closed over <see cref="ControlDefaults.CollapsiblePanelAnimationDurationMs"/> milliseconds.
		/// In headless contexts (no parent window / animation manager) the toggle remains instant.
		/// </summary>
		public CollapsibleAnimationMode AnimationMode
		{
			get => _animationMode;
			set => SetProperty(ref _animationMode, value);
		}

		/// <summary>
		/// Gets the current animated body height in rows (0 when fully collapsed), or
		/// <see langword="null"/> when no animation is in progress (layout uses the natural height).
		/// Exposed for the layout engine and tests.
		/// </summary>
		internal int? AnimatedBodyHeight => _animatedBodyHeight;

		/// <summary>
		/// Injects an <see cref="AnimationManager"/> for deterministic tests, bypassing the
		/// parent-window lookup.
		/// </summary>
		/// <param name="manager">The animation manager to drive height animations.</param>
		internal void SetAnimationManagerForTesting(AnimationManager manager) => _testAnimationManager = manager;

		/// <summary>
		/// Resolves the active <see cref="AnimationManager"/>: the test override if present,
		/// otherwise the one owned by the parent window's <see cref="ConsoleWindowSystem"/>.
		/// </summary>
		private AnimationManager? GetAnimationManager() =>
			_testAnimationManager ?? (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;

		/// <summary>
		/// Implements the animation hook declared in the core partial. Called from
		/// <c>SetExpanded</c> when <see cref="AnimationMode"/> is <see cref="CollapsibleAnimationMode.Height"/>.
		/// Tweens <see cref="AnimatedBodyHeight"/> from the current height to the target
		/// (measured body height when expanding, 0 when collapsing). Falls back to instant
		/// when no animation manager is available.
		/// </summary>
		partial void StartHeightAnimationCore()
		{
			var mgr = GetAnimationManager();
			if (mgr == null)
			{
				_animatedBodyHeight = null; // headless -> instant
				return;
			}

			int measured = MeasuredBodyHeight();
			int target = _isExpanded ? measured : 0;
			int from = _animatedBodyHeight ?? (_isExpanded ? 0 : measured);

			mgr.Animate(
				from: from,
				to: target,
				duration: TimeSpan.FromMilliseconds(ControlDefaults.CollapsiblePanelAnimationDurationMs),
				onUpdate: v =>
				{
					_animatedBodyHeight = v;
					Invalidate(Invalidation.Relayout);
				},
				onComplete: () =>
				{
					// Settled: expanded -> release clamp (null = natural height); collapsed -> pinned at 0.
					_animatedBodyHeight = _isExpanded ? null : 0;
					Invalidate(Invalidation.Relayout);
				});
		}

		/// <summary>
		/// Sums the natural content height of the (now-visible) body children, capped by
		/// <see cref="MaxContentHeight"/> when set. Used as the tween target/origin.
		/// </summary>
		private int MeasuredBodyHeight()
		{
			int sum = 0;
			lock (_childrenLock)
			{
				foreach (var c in _children)
					sum += c.GetLogicalContentSize().Height;
			}
			return _maxContentHeight.HasValue ? Math.Min(sum, _maxContentHeight.Value) : sum;
		}
	}
}
