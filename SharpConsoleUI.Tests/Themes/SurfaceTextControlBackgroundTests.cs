// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// A checkbox and a radio are SURFACE-TEXT controls: a marker glyph and a label drawn straight onto
/// whatever is behind them. They must not be given a background fill of their own.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this pins.</b> The palette generator assigned
/// <c>CheckboxBackgroundColor = Raise(background, 0.2)</c>, an opaque surface raised well clear of
/// the window. Every checkbox and radio in a palette theme was then painted into a visible grey
/// slab that no other text control has, which reads as a selection highlight that is stuck on.
/// </para>
/// <para>
/// <b>Why it stayed hidden.</b> <c>ModernGray</c>, the default and the theme every screenshot was
/// taken against, leaves this null. The slab only appeared on the palette themes, and on the Radio
/// Buttons demo page it was masked a second time by the ColumnContainer defect: with a null window
/// system the control could not read the theme at all, so it fell through to <c>Transparent</c> and
/// looked correct by accident. Fixing the container made the slab visible.
/// </para>
/// <para>
/// <b>Why transparent is the right answer, not a darker fill.</b> Three sources agree.
/// <see cref="Themes.ITheme.CheckboxBackgroundColor"/> is documented "Null means transparent";
/// <c>ModernGrayTheme</c> sets it to null; and <c>RadioControl.ResolveBackground</c> carries the
/// comment "A radio is a surface-text control (its label sits on the window surface), so a role
/// must NOT introduce a solid background fill". The generator was the only piece that disagreed.
/// </para>
/// <para>
/// Panel controls are deliberately NOT covered here. A list, tree, table or dropdown legitimately
/// owns a surface, and the generator correctly gives those the window background rather than a
/// raised one, so they composite invisibly while still being a real surface to draw on.
/// </para>
/// </remarks>
public class SurfaceTextControlBackgroundTests
{
	public static TheoryData<string> CatalogThemes() =>
		new() { "Ocean", "Amber", "Forest", "Crimson", "Slate", "Daylight" };

	/// <summary>
	/// The resting state must not paint a fill. Focused and disabled states are allowed one: those
	/// are deliberate, momentary feedback, and a user reads them as "this control, right now".
	/// </summary>
	[Theory]
	[MemberData(nameof(CatalogThemes))]
	public void CatalogTheme_CheckboxHasNoRestingBackgroundFill(string themeName)
	{
		var theme = new ThemeRegistryStateService().GetTheme(themeName)!;
		Color? fill = theme.CheckboxBackgroundColor;

		// Null is the documented "transparent". An explicit Transparent, or a color that composites
		// to the window surface anyway, is equally invisible and equally acceptable.
		bool invisible =
			fill is null
			|| fill.Value.A == 0
			|| fill.Value == theme.WindowBackgroundColor;

		Assert.True(
			invisible,
			$"{themeName} paints checkboxes and radios into a {fill} slab over a "
			+ $"{theme.WindowBackgroundColor} window. A surface-text control draws onto what is "
			+ "behind it; see ITheme.CheckboxBackgroundColor ('Null means transparent').");
	}

	/// <summary>
	/// The default theme already behaves correctly; this states that outright so the palette themes
	/// have something to be consistent WITH, rather than each being judged on its own.
	/// </summary>
	[Fact]
	public void ModernGray_CheckboxHasNoRestingBackgroundFill()
	{
		var theme = new ThemeRegistryStateService().GetTheme("ModernGray")!;
		Assert.Null(theme.CheckboxBackgroundColor);
	}
}
