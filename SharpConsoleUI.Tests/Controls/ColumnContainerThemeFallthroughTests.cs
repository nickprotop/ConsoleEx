// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A control inside a grid column must resolve theme colors, whatever order it was built in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this pins.</b> <see cref="ColumnContainer"/> caches the owning
/// <see cref="ConsoleWindowSystem"/> in its CONSTRUCTOR, reading it through the owner's container.
/// Build the grid before adding it to a window, which is the order every fluent builder produces,
/// and that read happens while the owner is still unparented: the cache is null, and nothing ever
/// fills it in, because the only writer is a setter that propagates to nested GRIDS and skips
/// ordinary child controls.
/// </para>
/// <para>
/// Every control in that column then resolves colors through
/// <c>Container?.GetConsoleWindowSystem?.Theme</c>, gets null, and falls through to its own
/// last-resort literal. The user sees it as one page ignoring the theme: on the DemoApp Radio
/// Buttons page under Slate, the radio markers painted <c>Color.Cyan1</c> and
/// <c>Color.White</c> while every control outside a column painted correct Slate colors.
/// </para>
/// <para>
/// <b>Why it is tested at the container, not the control.</b> Nothing is wrong with
/// <see cref="RadioControl{T}"/>: its resolution chain is correct and identical to the one in
/// <see cref="CheckboxControl"/>. The defect is that the chain is handed a null window system, so
/// the test asserts the container supplies one. Fixing it at the container fixes it for every
/// control type a column can hold, which is the whole point.
/// </para>
/// </remarks>
public class ColumnContainerThemeFallthroughTests
{
	/// <summary>
	/// Build the grid fully, THEN attach it, which is the order a fluent builder produces and the
	/// order the Radio Buttons demo uses.
	/// </summary>
	[Fact]
	public void ColumnBuiltBeforeAttach_StillResolvesTheWindowSystem()
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		var window = new Window(system) { Width = 60, Height = 20 };

		// Built while unparented: the constructor's cache read sees no container.
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		grid.AddColumn(column);

		// The real topology: a panel INSIDE the column, the control inside the panel. A control
		// added straight to the column is reached by the attach-time propagation; one behind an
		// intervening container is not, which is why a simpler tree does not reproduce this.
		var panel = new ScrollablePanelControl();
		var radio = new RadioControl<string>(new RadioGroup<string>(), "opt", "Option");
		panel.AddControl(radio);
		column.AddContent(panel);

		// Attached only now, and through an outer panel: the live tree is
		// Window > ScrollablePanelControl > HorizontalGridControl > ColumnContainer > panel.
		var outer = new ScrollablePanelControl();
		outer.AddControl(grid);
		window.AddControl(outer);

		Assert.NotNull(column.GetConsoleWindowSystem);
		Assert.Same(system, column.GetConsoleWindowSystem);
		Assert.Same(system, radio.Container?.GetConsoleWindowSystem);
	}

	/// <summary>
	/// The observable consequence: a themed color, not the control's hardcoded last-resort literal.
	/// </summary>
	[Fact]
	public void RadioInAColumn_UsesTheThemeCheckmarkColorNotItsFallback()
	{
		var system = TestWindowSystemBuilder.CreateTestSystemWithoutDiagnostics();
		system.ThemeStateService.SwitchTheme("Slate");
		var window = new Window(system) { Width = 60, Height = 20 };

		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		grid.AddColumn(column);

		var panel = new ScrollablePanelControl();
		var radio = new RadioControl<string>(new RadioGroup<string>(), "opt", "Option");
		panel.AddControl(radio);
		column.AddContent(panel);

		var outer = new ScrollablePanelControl();
		outer.AddControl(grid);
		window.AddControl(outer);

		Color expected = system.Theme.CheckboxCheckmarkColor!.Value;

		Assert.Equal(expected, radio.CheckmarkColor);
		Assert.NotEqual(Color.Cyan1, radio.CheckmarkColor);
	}
}
