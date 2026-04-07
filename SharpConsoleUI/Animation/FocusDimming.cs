using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Animation;

/// <summary>
/// Registration info for a pane managed by <see cref="FocusDimming"/>.
/// </summary>
/// <param name="Id">Unique identifier for the pane.</param>
/// <param name="Bounds">The pane's rectangular bounds in buffer coordinates.</param>
public record PaneRegistration(string Id, LayoutRect Bounds);

/// <summary>
/// Manages PostBufferPaint-based dim overlays for inactive panes,
/// with gradient shadow edges and animated splitter accents.
/// Composes existing animation and color-blending primitives.
/// </summary>
public sealed class FocusDimming
{
	private readonly Dictionary<string, PaneState> _panes = new();

	/// <summary>Number of registered panes.</summary>
	public int PaneCount => _panes.Count;

	/// <summary>The currently active (focused) pane ID, or null if none.</summary>
	public string? ActivePaneId { get; private set; }

	#region Configuration

	/// <summary>Background dim intensity applied to inactive panes (0.0–1.0). Default 0.22.</summary>
	public float BackgroundDimIntensity { get; set; } = 0.22f;

	/// <summary>Foreground dim blend ratio for inactive panes (0.0–1.0). Default 0.12.</summary>
	public float ForegroundDimIntensity { get; set; } = 0.12f;

	/// <summary>Width in cells of the shadow gradient on inactive pane edges adjacent to the active pane. Default 2.</summary>
	public int ShadowEdgeWidth { get; set; } = 2;

	/// <summary>Extra dim intensity at the shadow edge peak (0.0–1.0). Default 0.15.</summary>
	public float ShadowExtraIntensity { get; set; } = 0.15f;

	/// <summary>Accent color for splitter tinting. Default SteelBlue.</summary>
	public Color AccentColor { get; set; } = Color.SteelBlue;

	/// <summary>Opacity of the splitter accent tint (0.0–1.0). Default 0.8.</summary>
	public float AccentOpacity { get; set; } = 0.8f;

	/// <summary>Duration of dim/undim transitions. Default 200ms.</summary>
	public TimeSpan TransitionDuration { get; set; } = TimeSpan.FromMilliseconds(200);

	/// <summary>Easing function for transitions. Default EaseOut.</summary>
	public EasingFunction TransitionEasing { get; set; } = EasingFunctions.EaseOut;

	/// <summary>
	/// When true, all overlays and accents are suppressed and
	/// <see cref="GetCurrentDimIntensity"/> returns 0 for every pane.
	/// </summary>
	public bool Suspended { get; set; }

	#endregion

	#region Pane Registration

	/// <summary>
	/// Registers a pane. If a pane with the same ID already exists, its bounds are updated.
	/// </summary>
	public void RegisterPane(PaneRegistration registration)
	{
		if (_panes.TryGetValue(registration.Id, out var existing))
		{
			existing.Bounds = registration.Bounds;
		}
		else
		{
			var isActive = ActivePaneId == registration.Id;
			var intensity = isActive ? 0f : BackgroundDimIntensity;
			_panes[registration.Id] = new PaneState
			{
				Bounds = registration.Bounds,
				CurrentIntensity = intensity,
				TargetIntensity = intensity
			};
		}
	}

	/// <summary>
	/// Removes a pane from tracking. If it was the active pane, clears the active pane.
	/// </summary>
	public void UnregisterPane(string id)
	{
		if (_panes.Remove(id) && ActivePaneId == id)
			ActivePaneId = null;
	}

	/// <summary>
	/// Removes all panes and clears the active pane.
	/// </summary>
	public void ClearPanes()
	{
		_panes.Clear();
		ActivePaneId = null;
	}

	/// <summary>
	/// Updates the bounds for an already-registered pane. No-op if the pane is not registered.
	/// </summary>
	public void UpdatePaneBounds(string id, LayoutRect bounds)
	{
		if (_panes.TryGetValue(id, out var state))
			state.Bounds = bounds;
	}

	/// <summary>
	/// Gets the bounds for a registered pane.
	/// </summary>
	/// <exception cref="KeyNotFoundException">Thrown if the pane is not registered.</exception>
	public LayoutRect GetPaneBounds(string id) => _panes[id].Bounds;

	#endregion

	#region Focus Tracking

	/// <summary>
	/// Sets the active (focused) pane. Pass null to clear focus.
	/// Updates target intensities for all panes and snaps current intensities immediately.
	/// Use <see cref="SetActivePaneAnimated"/> if you intend to animate the transition.
	/// </summary>
	public void SetActivePane(string? id)
	{
		ActivePaneId = id;
		foreach (var kvp in _panes)
		{
			var target = kvp.Key == id ? 0f : BackgroundDimIntensity;
			kvp.Value.TargetIntensity = target;
			// For non-animated usage, snap current intensity to target
			kvp.Value.CurrentIntensity = target;
		}
	}

	/// <summary>
	/// Sets the active (focused) pane for animated transitions.
	/// Updates target intensities but leaves current intensities unchanged,
	/// so that <see cref="AnimateTransition"/> can create tweens from current to target.
	/// </summary>
	public void SetActivePaneAnimated(string? id)
	{
		ActivePaneId = id;
		foreach (var kvp in _panes)
		{
			kvp.Value.TargetIntensity = kvp.Key == id ? 0f : BackgroundDimIntensity;
		}
	}

	/// <summary>
	/// Gets the current dim intensity for a pane. Returns 0 if suspended or pane not found.
	/// </summary>
	public float GetCurrentDimIntensity(string id)
	{
		if (Suspended) return 0f;
		return _panes.TryGetValue(id, out var state) ? state.CurrentIntensity : 0f;
	}

	#endregion

	#region Animated Transitions

	/// <summary>
	/// Creates animations for panes that are transitioning between dim states.
	/// Returns the list of created animations (already added to the manager).
	/// </summary>
	/// <param name="animations">The animation manager to register tweens with.</param>
	/// <param name="onFrame">Optional callback invoked on each animation frame update.</param>
	public List<IAnimation> AnimateTransition(AnimationManager animations, Action? onFrame = null)
	{
		var result = new List<IAnimation>();

		foreach (var kvp in _panes)
		{
			var state = kvp.Value;
			float from = state.CurrentIntensity;
			float to = state.TargetIntensity;

			if (Math.Abs(from - to) < 0.001f)
				continue;

			var capturedState = state;
			var anim = animations.Animate(
				from, to,
				TransitionDuration,
				TransitionEasing,
				onUpdate: v =>
				{
					capturedState.CurrentIntensity = v;
					onFrame?.Invoke();
				},
				onComplete: () =>
				{
					capturedState.CurrentIntensity = to;
				});

			result.Add(anim);
		}

		return result;
	}

	#endregion

	#region Overlay Application

	/// <summary>
	/// Applies dim overlays to inactive panes in the buffer, including shadow edge gradients.
	/// No-op when <see cref="Suspended"/> is true.
	/// </summary>
	public void ApplyOverlays(CharacterBuffer buffer)
	{
		if (Suspended) return;

		LayoutRect? activeBounds = null;
		if (ActivePaneId != null && _panes.TryGetValue(ActivePaneId, out var activeState))
			activeBounds = activeState.Bounds;

		foreach (var kvp in _panes)
		{
			if (kvp.Key == ActivePaneId)
				continue;

			var state = kvp.Value;
			float intensity = state.CurrentIntensity;
			if (intensity <= 0.001f)
				continue;

			// Apply base dim overlay
			float fgRatio = BackgroundDimIntensity > 0f
				? ForegroundDimIntensity / BackgroundDimIntensity
				: 0f;
			ColorBlendHelper.ApplyColorOverlay(
				buffer, Color.Black, intensity, fgRatio,
				state.Bounds);

			// Apply shadow edge gradient on edges adjacent to active pane
			if (activeBounds.HasValue && ShadowEdgeWidth > 0 && ShadowExtraIntensity > 0f)
				ApplyShadowEdge(buffer, state.Bounds, activeBounds.Value);
		}
	}

	/// <summary>
	/// Tints splitter cells with the accent color.
	/// No-op when <see cref="Suspended"/> is true.
	/// </summary>
	/// <param name="buffer">The character buffer to modify.</param>
	/// <param name="splitterX">The X column of the splitter.</param>
	/// <param name="startY">The starting Y row.</param>
	/// <param name="height">Number of rows to tint.</param>
	public void ApplySplitterAccent(CharacterBuffer buffer, int splitterX, int startY, int height)
	{
		if (Suspended) return;

		int endY = Math.Min(buffer.Height, startY + height);
		for (int y = Math.Max(0, startY); y < endY; y++)
		{
			if (splitterX < 0 || splitterX >= buffer.Width)
				continue;

			var cell = buffer.GetCell(splitterX, y);
			var newBg = ColorBlendHelper.BlendColor(cell.Background, AccentColor, AccentOpacity);
			var newFg = ColorBlendHelper.BlendColor(cell.Foreground, AccentColor, AccentOpacity * 0.5f);
			buffer.SetCellColors(splitterX, y, newFg, newBg);
		}
	}

	#endregion

	#region Shadow Edge

	private void ApplyShadowEdge(CharacterBuffer buffer, LayoutRect inactiveBounds, LayoutRect activeBounds)
	{
		// Determine which edge of the inactive pane is adjacent to the active pane
		// Left edge of inactive is adjacent if active is to the left
		if (activeBounds.Right <= inactiveBounds.X && activeBounds.Right >= inactiveBounds.X - 1)
		{
			ApplyShadowGradientVertical(buffer, inactiveBounds, fromLeft: true);
		}
		// Right edge of inactive is adjacent if active is to the right
		else if (activeBounds.X >= inactiveBounds.Right && activeBounds.X <= inactiveBounds.Right + 1)
		{
			ApplyShadowGradientVertical(buffer, inactiveBounds, fromLeft: false);
		}
	}

	private void ApplyShadowGradientVertical(CharacterBuffer buffer, LayoutRect bounds, bool fromLeft)
	{
		int width = Math.Min(ShadowEdgeWidth, bounds.Width);
		int startY = Math.Max(0, bounds.Y);
		int endY = Math.Min(buffer.Height, bounds.Bottom);

		for (int col = 0; col < width; col++)
		{
			// Ramp from ShadowExtraIntensity (at edge) to 0 (interior)
			float t = 1f - (float)col / width;
			float extraIntensity = ShadowExtraIntensity * t;

			int x = fromLeft
				? bounds.X + col
				: bounds.Right - 1 - col;

			if (x < 0 || x >= buffer.Width)
				continue;

			for (int y = startY; y < endY; y++)
			{
				var cell = buffer.GetCell(x, y);
				var newBg = ColorBlendHelper.BlendColor(cell.Background, Color.Black, extraIntensity);
				var newFg = ColorBlendHelper.BlendColor(cell.Foreground, Color.Black, extraIntensity * 0.5f);
				buffer.SetCellColors(x, y, newFg, newBg);
			}
		}
	}

	#endregion

	/// <summary>
	/// Internal state for a tracked pane.
	/// </summary>
	private sealed class PaneState
	{
		public LayoutRect Bounds { get; set; }
		public float CurrentIntensity { get; set; }
		public float TargetIntensity { get; set; }
	}
}
