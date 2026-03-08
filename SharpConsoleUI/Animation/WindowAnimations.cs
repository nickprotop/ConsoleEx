using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Animation;

/// <summary>
/// Direction from which a window slides into or out of view.
/// </summary>
public enum SlideDirection
{
	/// <summary>Slide from/to the left edge.</summary>
	Left,
	/// <summary>Slide from/to the right edge.</summary>
	Right,
	/// <summary>Slide from/to the top edge.</summary>
	Top,
	/// <summary>Slide from/to the bottom edge.</summary>
	Bottom
}

/// <summary>
/// Pre-built window animation helpers for common transitions.
/// </summary>
public static class WindowAnimations
{
	/// <summary>
	/// Slides a window in from offscreen to its current position.
	/// </summary>
	public static IAnimation SlideIn(
		Window window,
		SlideDirection direction,
		TimeSpan? duration = null,
		EasingFunction? easing = null,
		Action? onComplete = null)
	{
		var manager = GetAnimationManager(window);
		var system = window.GetConsoleWindowSystem!;
		var dur = duration ?? TimeSpan.FromMilliseconds(AnimationDefaults.DefaultSlideDurationMs);

		int targetLeft = window.Left;
		int targetTop = window.Top;
		int startLeft = targetLeft;
		int startTop = targetTop;

		switch (direction)
		{
			case SlideDirection.Left:
				startLeft = -window.Width;
				break;
			case SlideDirection.Right:
				startLeft = system.DesktopDimensions.Width;
				break;
			case SlideDirection.Top:
				startTop = -window.Height;
				break;
			case SlideDirection.Bottom:
				startTop = system.DesktopDimensions.Height;
				break;
		}

		system.Positioning.MoveWindowTo(window, startLeft, startTop);

		return manager.Animate(
			from: 0.0f,
			to: 1.0f,
			duration: dur,
			easing: easing ?? EasingFunctions.EaseOut,
			onUpdate: t =>
			{
				int left = IntInterpolator.Instance.Interpolate(startLeft, targetLeft, t);
				int top = IntInterpolator.Instance.Interpolate(startTop, targetTop, t);
				system.Positioning.MoveWindowTo(window, left, top);
			},
			onComplete: onComplete);
	}

	/// <summary>
	/// Slides a window out of view in the specified direction.
	/// </summary>
	public static IAnimation SlideOut(
		Window window,
		SlideDirection direction,
		TimeSpan? duration = null,
		EasingFunction? easing = null,
		Action? onComplete = null)
	{
		var manager = GetAnimationManager(window);
		var system = window.GetConsoleWindowSystem!;
		var dur = duration ?? TimeSpan.FromMilliseconds(AnimationDefaults.DefaultSlideDurationMs);

		int startLeft = window.Left;
		int startTop = window.Top;
		int endLeft = startLeft;
		int endTop = startTop;

		switch (direction)
		{
			case SlideDirection.Left:
				endLeft = -window.Width;
				break;
			case SlideDirection.Right:
				endLeft = system.DesktopDimensions.Width;
				break;
			case SlideDirection.Top:
				endTop = -window.Height;
				break;
			case SlideDirection.Bottom:
				endTop = system.DesktopDimensions.Height;
				break;
		}

		return manager.Animate(
			from: 0.0f,
			to: 1.0f,
			duration: dur,
			easing: easing ?? EasingFunctions.EaseIn,
			onUpdate: t =>
			{
				int left = IntInterpolator.Instance.Interpolate(startLeft, endLeft, t);
				int top = IntInterpolator.Instance.Interpolate(startTop, endTop, t);
				system.Positioning.MoveWindowTo(window, left, top);
			},
			onComplete: onComplete);
	}

	/// <summary>
	/// Fades a window in by reducing a solid color overlay from full intensity to zero.
	/// The overlay is registered via PostBufferPaint BEFORE the first frame renders,
	/// so the window appears fully covered initially and gradually reveals.
	/// </summary>
	/// <param name="window">The window to fade in.</param>
	/// <param name="duration">Fade duration. Defaults to AnimationDefaults.DefaultFadeDurationMs.</param>
	/// <param name="fadeColor">The overlay color. Defaults to Color.Black.</param>
	/// <param name="easing">Easing function. Defaults to EaseInOut.</param>
	/// <param name="onComplete">Optional callback invoked when the fade completes.</param>
	/// <returns>The animation driving the fade.</returns>
	public static IAnimation FadeIn(
		Window window,
		TimeSpan? duration = null,
		Color? fadeColor = null,
		EasingFunction? easing = null,
		Action? onComplete = null)
	{
		var manager = GetAnimationManager(window);
		var dur = duration ?? TimeSpan.FromMilliseconds(AnimationDefaults.DefaultFadeDurationMs);
		var color = fadeColor ?? Color.Black;

		float currentIntensity = 1.0f;

		void FadeOverlay(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
		{
			ColorBlendHelper.ApplyColorOverlay(buffer, color, currentIntensity, AnimationDefaults.FadeForegroundBlendRatio);
		}

		// Register BEFORE first frame so window starts fully covered
		window.PostBufferPaint += FadeOverlay;

		return manager.Animate(
			from: 1.0f,
			to: 0.0f,
			duration: dur,
			easing: easing ?? EasingFunctions.EaseInOut,
			onUpdate: intensity =>
			{
				currentIntensity = intensity;
				window.Invalidate(redrawAll: true);
			},
			onComplete: () =>
			{
				currentIntensity = 0f;
				window.PostBufferPaint -= FadeOverlay;
				window.Invalidate(redrawAll: true);
				onComplete?.Invoke();
			});
	}

	/// <summary>
	/// Fades a window out by increasing a solid color overlay from zero to full intensity.
	/// Useful for graceful window dismissal - pair with an onComplete that closes the window.
	/// </summary>
	/// <param name="window">The window to fade out.</param>
	/// <param name="duration">Fade duration. Defaults to AnimationDefaults.DefaultFadeDurationMs.</param>
	/// <param name="fadeColor">The overlay color. Defaults to Color.Black.</param>
	/// <param name="easing">Easing function. Defaults to EaseInOut.</param>
	/// <param name="onComplete">Optional callback invoked when the fade completes (e.g., close the window).</param>
	/// <returns>The animation driving the fade.</returns>
	public static IAnimation FadeOut(
		Window window,
		TimeSpan? duration = null,
		Color? fadeColor = null,
		EasingFunction? easing = null,
		Action? onComplete = null)
	{
		var manager = GetAnimationManager(window);
		var dur = duration ?? TimeSpan.FromMilliseconds(AnimationDefaults.DefaultFadeDurationMs);
		var color = fadeColor ?? Color.Black;

		float currentIntensity = 0f;

		void FadeOverlay(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
		{
			ColorBlendHelper.ApplyColorOverlay(buffer, color, currentIntensity, AnimationDefaults.FadeForegroundBlendRatio);
		}

		window.PostBufferPaint += FadeOverlay;

		return manager.Animate(
			from: 0.0f,
			to: 1.0f,
			duration: dur,
			easing: easing ?? EasingFunctions.EaseInOut,
			onUpdate: intensity =>
			{
				currentIntensity = intensity;
				window.Invalidate(redrawAll: true);
			},
			onComplete: () =>
			{
				currentIntensity = 1.0f;
				// Call onComplete first (e.g., close window) before cleanup.
				// If the window is closed, no need to invalidate.
				onComplete?.Invoke();
				window.PostBufferPaint -= FadeOverlay;
			});
	}

	private static AnimationManager GetAnimationManager(Window window)
	{
		var system = window.GetConsoleWindowSystem
			?? throw new InvalidOperationException("Window must be associated with a ConsoleWindowSystem to animate.");
		return system.Animations;
	}
}
