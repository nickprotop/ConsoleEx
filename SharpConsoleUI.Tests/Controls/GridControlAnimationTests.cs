// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class GridControlAnimationTests
{
	private static (ConsoleWindowSystem system, Window window, GridControl grid) BuildGrid(
		GridLength[] cols, GridLength[] rows, int width = 60, int height = 16)
	{
		var grid = new GridControl { Width = width - 2, Height = height - 2 };
		foreach (var c in cols) grid.ColumnDefinitions.Add(c);
		foreach (var r in rows) grid.RowDefinitions.Add(r);
		for (int r = 0; r < rows.Length; r++)
			for (int c = 0; c < cols.Length; c++)
				grid.Place(new MarkupControl(new List<string> { $"r{r}c{c}" }), r, c);

		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system) { Left = 0, Top = 0, Width = width, Height = height };
		window.AddControl(grid);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		return (system, window, grid);
	}

	private static void RunToCompletion(ConsoleWindowSystem system, AnimationManager manager)
	{
		for (int i = 0; i < 1000 && manager.HasActiveAnimations; i++)
		{
			manager.Update(TimeSpan.FromMilliseconds(16));
			system.Render.UpdateDisplay();
		}
	}

	[Fact]
	public void AnimateColumnWidth_FixedTrack_ReachesExactTarget()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Cells(20) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 30, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);

		Assert.Equal(30, grid.GetColumnArrangedSizeForTest(0));
		Assert.Equal(GridUnitType.Fixed, grid.ColumnDefinitions[0].Type);
		Assert.Equal(30, grid.ColumnDefinitions[0].Value);
	}

	[Fact]
	public void AnimateColumnWidth_StarTrack_ReachesTargetWithinOneCell_AndStaysStar()
	{
		// Star restoration scales col0's WEIGHT by target/current, then the grid reflows proportionally
		// within the fixed content width — so the absolute target is only reproduced when the geometry
		// works out. For two equal Star columns the post-reflow col0 = W*scale/(scale+1) with
		// scale = target/(W/2); this equals `target` exactly when content width W = 2*target (= 80 here,
		// window width 82). That is the honest end size for a proportional track at this total width.
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) }, width: 82);
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 40, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);

		Assert.InRange(grid.GetColumnArrangedSizeForTest(0), 39, 41);
		Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[0].Type);
	}

	[Fact]
	public void AnimateColumnWidth_NoManager_AppliesEndStateImmediately_ReturnsNull()
	{
		var grid = new GridControl();
		grid.ColumnDefinitions.Add(GridLength.Cells(10));
		grid.ColumnDefinitions.Add(GridLength.Cells(10));
		grid.RowDefinitions.Add(GridLength.Star(1));

		var result = grid.AnimateColumnWidth(0, 25, TimeSpan.FromMilliseconds(100));

		Assert.Null(result);
		Assert.Equal(GridUnitType.Fixed, grid.ColumnDefinitions[0].Type);
		Assert.Equal(25, grid.ColumnDefinitions[0].Value);
	}

	[Fact]
	public void AnimateColumnWidth_IndexOutOfRange_ReturnsNull_NoThrow()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Cells(20) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		Assert.Null(grid.AnimateColumnWidth(99, 5, TimeSpan.FromMilliseconds(50)));
		Assert.Null(grid.AnimateColumnWidth(-1, 5, TimeSpan.FromMilliseconds(50)));
	}

	[Fact]
	public void AnimatedStarColumn_ReflowsOnWindowResize_AfterCompletion()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1), GridLength.Star(1) }, new[] { GridLength.Star(1) }, width: 60);
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 30, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);
		int before = grid.GetColumnArrangedSizeForTest(0);

		window.Width = 100;
		grid.Width = 98;
		grid.ColumnDefinitions[0] = GridLength.Star(
			grid.ColumnDefinitions[0].Weight, grid.ColumnDefinitions[0].Min, grid.ColumnDefinitions[0].Max);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();
		int after = grid.GetColumnArrangedSizeForTest(0);

		Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[0].Type);
		Assert.True(after > before, $"Star col should reflow wider on a wider window (before={before}, after={after})");
	}

	[Fact]
	public void AnimatedAutoColumn_ReturnsToAuto_AfterCompletion()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Auto(), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 12, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);

		Assert.Equal(GridUnitType.Auto, grid.ColumnDefinitions[0].Type);
	}

	[Fact]
	public void AnimateColumnWidth_CollapseToZero_TrackOccupiesNoCells()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 0, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);

		Assert.Equal(0, grid.GetColumnArrangedSizeForTest(0));
	}

	[Fact]
	public void AnimateRowHeight_FixedTrack_ReachesExactTarget()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Star(1) }, new[] { GridLength.Cells(3), GridLength.Cells(3) },
			width: 24, height: 20);
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateRowHeight(0, 6, TimeSpan.FromMilliseconds(100));
		RunToCompletion(system, manager);

		Assert.Equal(6, grid.GetRowArrangedSizeForTest(0));
		Assert.Equal(GridUnitType.Fixed, grid.RowDefinitions[0].Type);
		Assert.Equal(6, grid.RowDefinitions[0].Value);
	}

	[Fact]
	public void ReAnimatingSameColumn_CancelsPriorAnimation()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		var first = grid.AnimateColumnWidth(0, 40, TimeSpan.FromMilliseconds(500));
		manager.Update(TimeSpan.FromMilliseconds(16));
		system.Render.UpdateDisplay();
		Assert.NotNull(first);
		Assert.False(first!.IsComplete);

		var second = grid.AnimateColumnWidth(0, 10, TimeSpan.FromMilliseconds(100));
		Assert.True(first.IsComplete);

		RunToCompletion(system, manager);
		Assert.InRange(grid.GetColumnArrangedSizeForTest(0), 9, 11);
		Assert.NotNull(second);
	}

	[Fact]
	public void AnimateColumnWidth_ManagerDisabled_JumpsToEndStateInstantly()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(20), GridLength.Star(1) }, new[] { GridLength.Star(1) });
		var manager = new AnimationManager { IsEnabled = false };
		grid.SetAnimationManagerForTesting(manager);

		grid.AnimateColumnWidth(0, 25, TimeSpan.FromMilliseconds(500));
		system.Render.UpdateDisplay();

		Assert.Equal(25, grid.GetColumnArrangedSizeForTest(0));
		Assert.Equal(GridUnitType.Fixed, grid.ColumnDefinitions[0].Type);
		Assert.Equal(25, grid.ColumnDefinitions[0].Value);
	}

	[Fact]
	public void RealWindow_AnimateColumn_ProgressesAcrossFrames_AndSurvivesRerender()
	{
		var (system, window, grid) = BuildGrid(
			new[] { GridLength.Cells(10), GridLength.Star(1) }, new[] { GridLength.Star(1) }, width: 60);
		var manager = new AnimationManager();
		grid.SetAnimationManagerForTesting(manager);

		int start = grid.GetColumnArrangedSizeForTest(0);
		grid.AnimateColumnWidth(0, 40, TimeSpan.FromMilliseconds(160));

		var samples = new List<int>();
		for (int i = 0; i < 12 && manager.HasActiveAnimations; i++)
		{
			manager.Update(TimeSpan.FromMilliseconds(16));
			system.Render.UpdateDisplay();
			samples.Add(grid.GetColumnArrangedSizeForTest(0));
		}

		Assert.Contains(samples, s => s > start && s < 40);

		RunToCompletion(system, manager);
		int end = grid.GetColumnArrangedSizeForTest(0);
		Assert.InRange(end, 39, 41);

		system.Render.UpdateDisplay();
		Assert.Equal(end, grid.GetColumnArrangedSizeForTest(0));
	}
}
