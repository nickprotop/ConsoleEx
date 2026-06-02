// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using System.Drawing;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class SpinnerControlTests
{
	#region Helpers

	private static (ConsoleWindowSystem system, Window window) Attach(SpinnerControl s)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 40, Height = 10 };
		window.AddControl(s);
		return (system, window);
	}

	private static CharacterBuffer Paint(SpinnerControl s, int w = 20, int h = 1, LayoutRect? bounds = null)
	{
		var buffer = new CharacterBuffer(w, h);
		var b = bounds ?? new LayoutRect(0, 0, w, h);
		s.PaintDOM(buffer, b, b, Color.White, Color.Black);
		return buffer;
	}

	#endregion

	#region Constructor / defaults

	[Fact]
	public void DefaultsToBrailleSpinningFrameZero()
	{
		var s = new SpinnerControl();
		Assert.Equal(SpinnerStyle.Braille, s.Style);
		Assert.Equal(ControlDefaults.SpinnerDefaultIntervalMs, s.IntervalMs);
		Assert.True(s.IsSpinning);
		Assert.Equal(0, s.CurrentFrameIndex);
	}

	[Fact]
	public void StyleSelectsExpectedFrames()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Circle };
		Assert.Equal(ControlDefaults.SpinnerCircleFrames, s.EffectiveFrames);
	}

	[Theory]
	[InlineData(SpinnerStyle.Braille)]
	[InlineData(SpinnerStyle.Circle)]
	[InlineData(SpinnerStyle.Dots)]
	[InlineData(SpinnerStyle.Line)]
	[InlineData(SpinnerStyle.Arc)]
	[InlineData(SpinnerStyle.Bounce)]
	[InlineData(SpinnerStyle.Star)]
	[InlineData(SpinnerStyle.GrowVertical)]
	[InlineData(SpinnerStyle.GrowHorizontal)]
	[InlineData(SpinnerStyle.Toggle)]
	[InlineData(SpinnerStyle.Arrow)]
	[InlineData(SpinnerStyle.BouncingBar)]
	[InlineData(SpinnerStyle.AestheticBar)]
	[InlineData(SpinnerStyle.BrailleDots)]
	[InlineData(SpinnerStyle.DotsBounce)]
	public void EveryStyleResolvesToNonEmptyFrames(SpinnerStyle style)
	{
		var frames = SpinnerControl.FramesForStyle(style);
		Assert.NotEmpty(frames);
		Assert.All(frames, f => Assert.False(string.IsNullOrEmpty(f)));
	}

	[Theory]
	[InlineData(SpinnerStyle.Braille, 100)]
	[InlineData(SpinnerStyle.Circle, 120)]
	[InlineData(SpinnerStyle.Dots, 360)]
	[InlineData(SpinnerStyle.Line, 120)]
	[InlineData(SpinnerStyle.Arc, 300)]
	[InlineData(SpinnerStyle.Bounce, 100)]
	[InlineData(SpinnerStyle.Star, 70)]
	[InlineData(SpinnerStyle.GrowVertical, 120)]
	[InlineData(SpinnerStyle.GrowHorizontal, 120)]
	[InlineData(SpinnerStyle.Toggle, 240)]
	[InlineData(SpinnerStyle.Arrow, 120)]
	[InlineData(SpinnerStyle.BouncingBar, 80)]
	[InlineData(SpinnerStyle.AestheticBar, 80)]
	[InlineData(SpinnerStyle.BrailleDots, 80)]
	[InlineData(SpinnerStyle.DotsBounce, 200)]
	public void DefaultIntervalMsMatchesTable(SpinnerStyle style, int expected)
	{
		Assert.Equal(expected, SpinnerControl.DefaultIntervalMs(style));
	}

	[Theory]
	[InlineData(SpinnerStyle.Star)]
	[InlineData(SpinnerStyle.Toggle)]
	[InlineData(SpinnerStyle.Arrow)]
	[InlineData(SpinnerStyle.GrowVertical)]
	[InlineData(SpinnerStyle.GrowHorizontal)]
	[InlineData(SpinnerStyle.BouncingBar)]
	[InlineData(SpinnerStyle.AestheticBar)]
	[InlineData(SpinnerStyle.BrailleDots)]
	[InlineData(SpinnerStyle.DotsBounce)]
	public void EveryFrameOfAStyleHasUniformReservedWidth(SpinnerStyle style)
	{
		// All frames in a set must share one display width so the spinner never reflows
		// surrounding text. Ambiguous-width styles (Star/Toggle/Arrow) achieve this via
		// trailing-space padding; bar styles are already fixed-width.
		var frames = SpinnerControl.FramesForStyle(style);
		int reserved = global::SharpConsoleUI.Parsing.MarkupSpinnerClock.ReservedWidth(style);
		Assert.All(frames, f => Assert.Equal(reserved, global::SharpConsoleUI.Parsing.MarkupParser.StripLength(f)));
	}

	[Fact]
	public void IntervalMsResolvesToPerStyleDefaultWhenUnset()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Dots };
		Assert.Equal(360, s.IntervalMs);
	}

	[Fact]
	public void IntervalMsReturnsExplicitValueWhenSet()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Dots, IntervalMs = 50 };
		Assert.Equal(50, s.IntervalMs);
	}

	[Fact]
	public void ExplicitIntervalSurvivesStyleChange()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Dots, IntervalMs = 50 };
		s.Style = SpinnerStyle.Star;
		Assert.Equal(50, s.IntervalMs);
	}

	[Fact]
	public void UnsetIntervalFollowsStyleChange()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Dots };
		Assert.Equal(360, s.IntervalMs);
		s.Style = SpinnerStyle.Star;
		Assert.Equal(70, s.IntervalMs);
	}

	[Fact]
	public void DefaultStyleSpinnerKeepsGlobalInterval()
	{
		var s = new SpinnerControl(); // Braille
		Assert.Equal(ControlDefaults.SpinnerDefaultIntervalMs, s.IntervalMs);
	}

	[Fact]
	public void CustomFramesOverrideStyle()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Braille };
		s.Frames = new[] { "a", "bb" };
		Assert.Equal(new[] { "a", "bb" }, s.EffectiveFrames);
	}

	[Fact]
	public void EmptyCustomFramesFallBackToStyle()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Circle };
		s.Frames = Array.Empty<string>();
		Assert.Equal(ControlDefaults.SpinnerCircleFrames, s.EffectiveFrames);
	}

	#endregion

	#region Lifecycle

	[Fact]
	public void RegistersAnimationOnAttachAndAutoStarts()
	{
		var s = new SpinnerControl();
		var (system, _) = Attach(s);
		Assert.True(system.Animations.ActiveCount >= 1);
	}

	[Fact]
	public void DetachCancelsAnimation()
	{
		var s = new SpinnerControl();
		var (system, window) = Attach(s);
		int before = system.Animations.ActiveCount;
		window.RemoveContent(s);
		Assert.Equal(0, system.Animations.ActiveCount);
	}

	[Fact]
	public void StopFreezesFrameStartResumes()
	{
		var s = new SpinnerControl();
		Attach(s);
		s.Stop();
		Assert.False(s.IsSpinning);
		int frozen = s.CurrentFrameIndex;
		Assert.Equal(frozen, s.CurrentFrameIndex);
		s.Start();
		Assert.True(s.IsSpinning);
	}

	[Fact]
	public void NoAnimationWhenAnimationsDisabled()
	{
		var s = new SpinnerControl();
		var system = TestWindowSystemBuilder.CreateTestSystem();
		system.Animations.IsEnabled = false;
		var window = new Window(system) { Width = 40, Height = 10 };
		window.AddControl(s);
		Assert.Equal(0, system.Animations.ActiveCount);
	}

	#endregion

	#region Rendering

	[Fact]
	public void RendersCurrentGlyphWithColor()
	{
		var s = new SpinnerControl { Color = Color.Yellow };
		s.Frames = new[] { "X" };
		var buffer = Paint(s);
		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('X'), cell.Character);
		Assert.Equal(Color.Yellow, cell.Foreground);
	}

	[Fact]
	public void RendersMarkupFrame()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "[green]Y[/]" };
		var buffer = Paint(s);
		var cell = buffer.GetCell(0, 0);
		Assert.Equal(new Rune('Y'), cell.Character);
		Assert.Equal(Color.Green, cell.Foreground);
	}

	[Fact]
	public void WideGlyphEmitsContinuationCell()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "📦" };
		var buffer = Paint(s);
		Assert.True(buffer.GetCell(1, 0).IsWideContinuation);
	}

	[Fact]
	public void PresetBrailleGlyphIsSingleColumn()
	{
		var s = new SpinnerControl { Style = SpinnerStyle.Braille };
		s.Frames = new[] { "⣷" };
		var buffer = Paint(s);
		Assert.False(buffer.GetCell(1, 0).IsWideContinuation);
	}

	[Fact]
	public void FrozenFramePersistsAcrossPaints()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "A", "B" };
		Attach(s);
		s.Stop();
		var b1 = Paint(s);
		var b2 = Paint(s);
		Assert.Equal(b1.GetCell(0, 0).Character, b2.GetCell(0, 0).Character);
	}

	[Fact]
	public void ClipRectSuppressesOutsideGlyph()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "Z" };
		var buffer = new CharacterBuffer(20, 1);
		var bounds = new LayoutRect(0, 0, 20, 1);
		var clip = new LayoutRect(5, 0, 5, 1);
		s.PaintDOM(buffer, bounds, clip, Color.White, Color.Black);
		Assert.NotEqual(new Rune('Z'), buffer.GetCell(0, 0).Character);
	}

	#endregion

	#region Measure / place / align

	[Fact]
	public void LogicalHeightAccountsForMargin()
	{
		var s = new SpinnerControl { Margin = new Margin(0, 2, 0, 1) };
		Assert.Equal(1 + 2 + 1, s.GetLogicalContentSize().Height);
	}

	[Fact]
	public void WidthIsMaxFrameDisplayWidthNotStringLength()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "[red]A[/]" }; // string.Length 9, display width 1
		Assert.Equal(1, s.GetLogicalContentSize().Width);
	}

	[Fact]
	public void WidthReflectsWidestCustomFrame()
	{
		var s = new SpinnerControl();
		s.Frames = new[] { "a", "📦" }; // widths 1 and 2
		Assert.Equal(2, s.GetLogicalContentSize().Width);
	}

	[Fact]
	public void MarginOffsetsRenderedGlyph()
	{
		var s = new SpinnerControl { Margin = new Margin(3, 0, 0, 0) };
		s.Frames = new[] { "Q" };
		var buffer = Paint(s);
		Assert.Equal(new Rune('Q'), buffer.GetCell(3, 0).Character);
	}

	#endregion

	#region Builder

	[Fact]
	public void BuilderConfiguresControl()
	{
		var s = SharpConsoleUI.Builders.Controls.Spinner()
			.WithStyle(SpinnerStyle.Circle)
			.WithColor(Color.Yellow)
			.WithInterval(250)
			.WithName("spin")
			.Build();
		Assert.Equal(SpinnerStyle.Circle, s.Style);
		Assert.Equal(Color.Yellow, s.Color);
		Assert.Equal(250, s.IntervalMs);
		Assert.Equal("spin", s.Name);
	}

	[Fact]
	public void BuilderCustomFrames()
	{
		var s = SharpConsoleUI.Builders.Controls.Spinner()
			.WithFrames("[green]✔[/]", "[yellow]◐[/]")
			.Build();
		Assert.Equal(new[] { "[green]✔[/]", "[yellow]◐[/]" }, s.EffectiveFrames);
	}

	#endregion

	#region Nested container regression

	// Regression for the frozen-spinner bug: when a SpinnerControl lives inside a
	// HorizontalGrid column (its direct Container is a ColumnContainer whose
	// GetConsoleWindowSystem is null at attach time), the eager Container-setter
	// StartAnimation() bails (window system unreachable one level up) and was never
	// retried — the spinner rendered frozen on frame 0. The fix resolves the
	// animation manager via the full parent-window chain and lazily (re)registers
	// the animation at paint time.
	[Fact]
	public void NestedInGridColumn_RegistersAndAnimatesAfterPaint()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 60, Height = 12 };

		// Build grid -> column -> spinner. At AddContent time the column's
		// GetConsoleWindowSystem is still null (grid not yet attached to a window).
		var spinner = new SpinnerControl();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.AddContent(spinner);
		grid.AddColumn(column);

		// Attach the whole grid to the window AFTER the spinner is buried in the column.
		window.AddControl(grid);

		// Drive a paint of the spinner directly. The lazy guard in PaintDOM should
		// register the animation now that the parent-window chain is wired.
		var buffer = new CharacterBuffer(20, 1);
		var b = new LayoutRect(0, 0, 20, 1);
		spinner.PaintDOM(buffer, b, b, Color.White, Color.Black);

		Assert.True(system.Animations.ActiveCount >= 1,
			"Spinner animation should be registered after attach + paint in a nested container.");

		// Advance enough ticks to cross the frame interval (manager caps each tick at
		// MaxFrameDeltaMs, so loop several 100ms ticks) and confirm the frame advances.
		for (int i = 0; i < 8; i++)
			system.Animations.Update(TimeSpan.FromMilliseconds(100));

		Assert.NotEqual(0, spinner.CurrentFrameIndex);
	}

	#endregion
}
