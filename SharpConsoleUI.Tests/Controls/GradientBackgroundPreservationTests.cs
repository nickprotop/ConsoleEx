using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class GradientBackgroundPreservationTests
{
	[Fact]
	public void HasGradientBackground_Window_WithGradient_ReturnsTrue()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed),
			GradientDirection.Vertical);

		Assert.True(((IContainer)window).HasGradientBackground);
	}

	[Fact]
	public void HasGradientBackground_Window_WithoutGradient_ReturnsFalse()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);

		Assert.False(((IContainer)window).HasGradientBackground);
	}

	[Fact]
	public void HasGradientBackground_Propagates_WhenNoExplicitBg()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed),
			GradientDirection.Vertical);

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		// Column has no explicit background (_backgroundColorValue is null)
		column.Container = window;

		Assert.True(column.HasGradientBackground);
	}

	[Fact]
	public void HasGradientBackground_Blocked_WhenContainerHasExplicitBg()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed),
			GradientDirection.Vertical);

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.Container = window;
		// Set explicit background - should block gradient propagation
		column.BackgroundColor = Color.DarkGreen;

		Assert.False(column.HasGradientBackground);
	}

	[Fact]
	public void ColumnContainer_PreservesGradient_WhenTransparent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed),
			GradientDirection.Horizontal);

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.Container = window;

		// Column with no explicit bg should propagate gradient
		Assert.True(column.HasGradientBackground);
	}

	[Fact]
	public void ColumnContainer_FillsSolid_WhenExplicitBg()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system);
		window.BackgroundGradient = new GradientBackground(
			ColorGradient.FromColors(Color.DarkBlue, Color.DarkRed),
			GradientDirection.Horizontal);

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.Container = window;
		column.BackgroundColor = Color.Navy;

		// Column with explicit bg should block gradient
		Assert.False(column.HasGradientBackground);
	}
}
