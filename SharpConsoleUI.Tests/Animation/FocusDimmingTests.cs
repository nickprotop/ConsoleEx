using SharpConsoleUI.Animation;
using SharpConsoleUI.Layout;
using Xunit;

// Cell is in SharpConsoleUI.Layout

namespace SharpConsoleUI.Tests.Animation;

public class FocusDimmingTests
{
	private static FocusDimming CreateWithTwoPanes()
	{
		var dimming = new FocusDimming();
		dimming.RegisterPane(new PaneRegistration("left", new LayoutRect(0, 0, 40, 25)));
		dimming.RegisterPane(new PaneRegistration("right", new LayoutRect(41, 0, 39, 25)));
		return dimming;
	}

	[Fact]
	public void RegisterPane_AddsPaneToTracking()
	{
		var dimming = new FocusDimming();

		dimming.RegisterPane(new PaneRegistration("pane1", new LayoutRect(0, 0, 40, 25)));

		Assert.Equal(1, dimming.PaneCount);
	}

	[Fact]
	public void RegisterPane_DuplicateId_UpdatesBounds()
	{
		var dimming = new FocusDimming();
		dimming.RegisterPane(new PaneRegistration("pane1", new LayoutRect(0, 0, 40, 25)));

		dimming.RegisterPane(new PaneRegistration("pane1", new LayoutRect(0, 0, 50, 30)));

		Assert.Equal(1, dimming.PaneCount);
	}

	[Fact]
	public void UnregisterPane_RemovesPane()
	{
		var dimming = new FocusDimming();
		dimming.RegisterPane(new PaneRegistration("pane1", new LayoutRect(0, 0, 40, 25)));

		dimming.UnregisterPane("pane1");

		Assert.Equal(0, dimming.PaneCount);
	}

	[Fact]
	public void UnregisterPane_UnknownId_DoesNotThrow()
	{
		var dimming = new FocusDimming();

		dimming.UnregisterPane("nonexistent");

		Assert.Equal(0, dimming.PaneCount);
	}

	[Fact]
	public void ClearPanes_RemovesAll()
	{
		var dimming = CreateWithTwoPanes();

		dimming.ClearPanes();

		Assert.Equal(0, dimming.PaneCount);
	}

	[Fact]
	public void UpdatePaneBounds_ChangesBoundsForExistingPane()
	{
		var dimming = new FocusDimming();
		dimming.RegisterPane(new PaneRegistration("pane1", new LayoutRect(0, 0, 40, 25)));

		dimming.UpdatePaneBounds("pane1", new LayoutRect(5, 5, 30, 20));

		// No exception — bounds updated. We verify indirectly via GetPaneBounds.
		Assert.Equal(new LayoutRect(5, 5, 30, 20), dimming.GetPaneBounds("pane1"));
	}

	[Fact]
	public void UpdatePaneBounds_UnknownId_DoesNotThrow()
	{
		var dimming = new FocusDimming();

		dimming.UpdatePaneBounds("nonexistent", new LayoutRect(0, 0, 10, 10));

		Assert.Equal(0, dimming.PaneCount);
	}

	[Fact]
	public void ActivePaneId_InitiallyNull()
	{
		var dimming = new FocusDimming();

		Assert.Null(dimming.ActivePaneId);
	}

	[Fact]
	public void SetActivePane_UpdatesActivePaneId()
	{
		var dimming = CreateWithTwoPanes();

		dimming.SetActivePane("left");

		Assert.Equal("left", dimming.ActivePaneId);
	}

	[Fact]
	public void SetActivePane_Null_ClearsActivePaneId()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		dimming.SetActivePane(null);

		Assert.Null(dimming.ActivePaneId);
	}

	[Fact]
	public void GetCurrentDimIntensity_ActivePane_ReturnsZero()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		Assert.Equal(0f, dimming.GetCurrentDimIntensity("left"));
	}

	[Fact]
	public void GetCurrentDimIntensity_InactivePane_ReturnsBackgroundDimIntensity()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		Assert.Equal(dimming.BackgroundDimIntensity, dimming.GetCurrentDimIntensity("right"));
	}

	[Fact]
	public void GetCurrentDimIntensity_NoActivePane_AllReturnBackgroundDimIntensity()
	{
		var dimming = CreateWithTwoPanes();

		Assert.Equal(dimming.BackgroundDimIntensity, dimming.GetCurrentDimIntensity("left"));
		Assert.Equal(dimming.BackgroundDimIntensity, dimming.GetCurrentDimIntensity("right"));
	}

	[Fact]
	public void GetCurrentDimIntensity_Suspended_ReturnsZero()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");
		dimming.Suspended = true;

		Assert.Equal(0f, dimming.GetCurrentDimIntensity("left"));
		Assert.Equal(0f, dimming.GetCurrentDimIntensity("right"));
	}

	[Fact]
	public void GetCurrentDimIntensity_UnknownPane_ReturnsZero()
	{
		var dimming = new FocusDimming();

		Assert.Equal(0f, dimming.GetCurrentDimIntensity("nonexistent"));
	}

	[Fact]
	public void DefaultConfiguration_HasExpectedValues()
	{
		var dimming = new FocusDimming();

		Assert.Equal(0.22f, dimming.BackgroundDimIntensity);
		Assert.Equal(0.12f, dimming.ForegroundDimIntensity);
		Assert.Equal(2, dimming.ShadowEdgeWidth);
		Assert.Equal(0.15f, dimming.ShadowExtraIntensity);
		Assert.Equal(Color.SteelBlue, dimming.AccentColor);
		Assert.Equal(0.8f, dimming.AccentOpacity);
		Assert.Equal(TimeSpan.FromMilliseconds(200), dimming.TransitionDuration);
		Assert.False(dimming.Suspended);
	}

	[Fact]
	public void ConfigurationProperties_AreSettable()
	{
		var dimming = new FocusDimming
		{
			BackgroundDimIntensity = 0.5f,
			ForegroundDimIntensity = 0.3f,
			ShadowEdgeWidth = 4,
			ShadowExtraIntensity = 0.25f,
			AccentColor = Color.Red,
			AccentOpacity = 0.6f,
			TransitionDuration = TimeSpan.FromMilliseconds(500)
		};

		Assert.Equal(0.5f, dimming.BackgroundDimIntensity);
		Assert.Equal(0.3f, dimming.ForegroundDimIntensity);
		Assert.Equal(4, dimming.ShadowEdgeWidth);
		Assert.Equal(0.25f, dimming.ShadowExtraIntensity);
		Assert.Equal(Color.Red, dimming.AccentColor);
		Assert.Equal(0.6f, dimming.AccentOpacity);
		Assert.Equal(TimeSpan.FromMilliseconds(500), dimming.TransitionDuration);
	}

	[Fact]
	public void SetActivePane_ChangingFocus_UpdatesTargetIntensities()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		Assert.Equal(0f, dimming.GetCurrentDimIntensity("left"));
		Assert.Equal(dimming.BackgroundDimIntensity, dimming.GetCurrentDimIntensity("right"));

		dimming.SetActivePane("right");

		Assert.Equal(dimming.BackgroundDimIntensity, dimming.GetCurrentDimIntensity("left"));
		Assert.Equal(0f, dimming.GetCurrentDimIntensity("right"));
	}

	[Fact]
	public void ApplyOverlays_WhenSuspended_IsNoOp()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");
		dimming.Suspended = true;

		var buffer = new CharacterBuffer(80, 25);

		// Should not throw and should not modify buffer
		dimming.ApplyOverlays(buffer);
	}

	[Fact]
	public void ApplySplitterAccent_WhenSuspended_IsNoOp()
	{
		var dimming = new FocusDimming();
		dimming.Suspended = true;

		var buffer = new CharacterBuffer(80, 25);

		dimming.ApplySplitterAccent(buffer, 40, 0, 25);
	}

	[Fact]
	public void ApplyOverlays_DimmsInactivePanes()
	{
		var dimming = new FocusDimming();
		dimming.RegisterPane(new PaneRegistration("left", new LayoutRect(0, 0, 10, 5)));
		dimming.RegisterPane(new PaneRegistration("right", new LayoutRect(11, 0, 10, 5)));
		dimming.SetActivePane("left");

		var buffer = new CharacterBuffer(21, 5, Color.White);
		// Fill buffer with white foreground
		for (int y = 0; y < 5; y++)
			for (int x = 0; x < 21; x++)
				buffer.SetCell(x, y, new Cell(new System.Text.Rune(' '), Color.White, Color.White));

		dimming.ApplyOverlays(buffer);

		// Active pane (left) should be unmodified
		var activeCell = buffer.GetCell(5, 2);
		Assert.Equal(Color.White, activeCell.Background);

		// Inactive pane (right) should be dimmed
		var inactiveCell = buffer.GetCell(15, 2);
		Assert.NotEqual(Color.White, inactiveCell.Background);
	}

	[Fact]
	public void AnimateTransition_ReturnsAnimationsForTransitioningPanes()
	{
		var dimming = CreateWithTwoPanes();
		// Set initial focus so intensities are snapped
		dimming.SetActivePane("left");

		// Now change focus — use AnimateTransition instead of SetActivePane
		// so that current intensities don't snap to targets
		dimming.SetActivePaneAnimated("right");

		var manager = new AnimationManager();
		var animations = dimming.AnimateTransition(manager);

		// Should have created animations for both panes transitioning
		Assert.NotEmpty(animations);
	}

	[Fact]
	public void UnregisterPane_ClearsActiveIfRemoved()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		dimming.UnregisterPane("left");

		Assert.Null(dimming.ActivePaneId);
	}

	[Fact]
	public void ClearPanes_ClearsActivePane()
	{
		var dimming = CreateWithTwoPanes();
		dimming.SetActivePane("left");

		dimming.ClearPanes();

		Assert.Null(dimming.ActivePaneId);
	}

	[Fact]
	public void TransitionEasing_DefaultIsEaseOut()
	{
		var dimming = new FocusDimming();

		Assert.Equal(EasingFunctions.EaseOut, dimming.TransitionEasing);
	}
}
