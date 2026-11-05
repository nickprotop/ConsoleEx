// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class PaletteThemeGeneratorTests
{
	private static readonly Color Primary = new Color(0xFD, 0x7E, 0x14); // orange

	private static MutableTheme Gen(Palette p) => PaletteThemeGenerator.Generate(p);

	[Fact]
	public void Primary_DrivesActiveBorder()
		=> Assert.Equal(Primary, Gen(new Palette { Primary = Primary }).ActiveBorderForegroundColor);

	[Fact]
	public void NoMode_NoBackground_DefaultsToDark()
		=> Assert.Equal(ThemeMode.Dark, Gen(new Palette { Primary = Primary }).Mode);

	[Fact]
	public void LightBackground_InfersLightMode_WithDarkText()
	{
		var theme = Gen(new Palette { Background = new Color(245, 245, 245) });
		Assert.Equal(ThemeMode.Light, theme.Mode);
		Assert.True(theme.WindowForegroundColor.Luminance() < 128, "light theme has dark text");
	}

	[Fact]
	public void DarkBackground_InfersDarkMode_WithLightText()
	{
		var theme = Gen(new Palette { Background = new Color(20, 20, 20) });
		Assert.Equal(ThemeMode.Dark, theme.Mode);
		Assert.True(theme.WindowForegroundColor.Luminance() > 128, "dark theme has light text");
	}

	[Fact]
	public void ExplicitMode_OverridesInference()
		=> Assert.Equal(ThemeMode.Light, Gen(new Palette { Background = new Color(20, 20, 20), Mode = ThemeMode.Light }).Mode);

	[Fact]
	public void Deterministic_SamePaletteSameColors()
	{
		var a = Gen(new Palette { Primary = Primary });
		var b = Gen(new Palette { Primary = Primary });
		Assert.Equal(a.ButtonBackgroundColor, b.ButtonBackgroundColor);
		Assert.Equal(a.WindowBackgroundColor, b.WindowBackgroundColor);
	}

	[Fact]
	public void DangerOverride_IsHonored()
	{
		var danger = new Color(0xE5, 0x48, 0x4D);
		Assert.Equal(danger, Gen(new Palette { Primary = Primary, Danger = danger }).NotificationDangerWindowBackgroundColor);
	}

	[Fact]
	public void EmptyPalette_ProducesCoherentDarkTheme()
	{
		var theme = Gen(new Palette());
		Assert.Equal(ThemeMode.Dark, theme.Mode);
		Assert.True(theme.WindowBackgroundColor.IsDark());
	}

	/// <summary>
	/// Members that are deliberately null in EVERY palette, because null is their meaning rather
	/// than an oversight: ITheme documents these as "Null means transparent", and the control they
	/// belong to is a surface-text control that draws onto whatever is behind it.
	/// </summary>
	/// <remarks>
	/// The tripwire below cannot tell "nobody derived this" from "the derived answer is no fill",
	/// since both come out equal across two palettes. Listing the second case here keeps the
	/// tripwire sharp for the first. A member only belongs here if null is a decision: if it is
	/// null merely because nothing got round to setting it, that is exactly the miss being hunted.
	/// </remarks>
	private static readonly HashSet<string> IntentionallyTransparent = new()
	{
		// A checkbox and a radio paint a marker and a label onto the window surface. Giving them a
		// resting fill put every one of them inside a visible slab; see
		// SurfaceTextControlBackgroundTests.
		"CheckboxBackgroundColor",
	};

	// TRIPWIRE: every ITheme color member must be DERIVED from the palette. Two palettes with entirely
	// different cores must produce different values for every color member; any member equal across the
	// two was left at a palette-independent constant (inherited ModernGray default or hardcoded) — a miss.
	// The one exception is a member whose derived answer is "no fill at all"; see IntentionallyTransparent.
	[Fact]
	public void Generated_DerivesEveryColorMemberFromPalette()
	{
		var a = (ITheme)Gen(new Palette
		{
			Primary = new Color(200, 0, 200),
			Background = new Color(30, 0, 40),
			Foreground = new Color(230, 230, 230),
			Success = new Color(10, 200, 10),
			Warning = new Color(200, 200, 10),
			Danger = new Color(200, 10, 10),
			Info = new Color(10, 200, 200),
			Mode = ThemeMode.Dark
		});
		var b = (ITheme)Gen(new Palette
		{
			Primary = new Color(255, 140, 0),
			Background = new Color(0, 40, 30),
			Foreground = new Color(210, 210, 210),
			Success = new Color(20, 150, 60),
			Warning = new Color(180, 120, 0),
			Danger = new Color(150, 0, 40),
			Info = new Color(0, 150, 180),
			Mode = ThemeMode.Dark
		});

		var notDerived = new System.Collections.Generic.List<string>();
		foreach (var pr in typeof(ITheme).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (IntentionallyTransparent.Contains(pr.Name))
			{
				// Null is this member's derived answer, and it must stay null in BOTH palettes:
				// that keeps the exemption honest, so a stray fill creeping back still fails here.
				Assert.Null((Color?)pr.GetValue(a));
				Assert.Null((Color?)pr.GetValue(b));
				continue;
			}

			if (pr.PropertyType == typeof(Color))
			{
				var va = (Color)pr.GetValue(a)!;
				var vb = (Color)pr.GetValue(b)!;
				if (va.Equals(vb)) notDerived.Add(pr.Name);
			}
			else if (pr.PropertyType == typeof(Color?))
			{
				var va = (Color?)pr.GetValue(a);
				var vb = (Color?)pr.GetValue(b);
				if (Nullable.Equals(va, vb)) notDerived.Add(pr.Name);
			}
		}
		Assert.True(notDerived.Count == 0,
			"palette generator did NOT derive these ITheme color member(s) from the palette (equal across two very different palettes):\n"
			+ string.Join("\n", notDerived));
	}

	private static double LumGap(Color? a, Color? b)
	{
		Assert.NotNull(a);
		Assert.NotNull(b);
		return System.Math.Abs(a.Value.Luminance() - b.Value.Luminance());
	}

	[Fact]
	public void Foreground_StaysReadable_EvenWhenUserSuppliesLowContrast()
	{
		// User sets a dark foreground but no background (default dark bg) — must NOT produce dark-on-dark.
		var theme = Gen(new Palette { Foreground = new Color(30, 30, 30) });
		Assert.True(LumGap(theme.WindowBackgroundColor, theme.WindowForegroundColor) >= 80,
			$"window fg must stay readable; gap was {LumGap(theme.WindowBackgroundColor, theme.WindowForegroundColor):0}");
	}

	[Fact]
	public void Foreground_CloseToBackground_IsCorrected()
	{
		var theme = Gen(new Palette { Background = new Color(20, 20, 20), Foreground = new Color(40, 40, 40) });
		Assert.True(LumGap(theme.WindowBackgroundColor, theme.WindowForegroundColor) >= 80);
	}

	[Fact]
	public void ActiveBorder_StaysVisible_WhenPrimaryCloseToBackground()
	{
		var theme = Gen(new Palette { Background = new Color(40, 40, 60), Primary = new Color(50, 50, 70) });
		Assert.True(LumGap(theme.WindowBackgroundColor, theme.ActiveBorderForegroundColor) >= 40,
			$"active border must stay visible against the window bg; gap was {LumGap(theme.WindowBackgroundColor, theme.ActiveBorderForegroundColor):0}");
	}

	[Fact]
	public void ActiveBorder_StaysVisible_YellowOnWhite()
	{
		var theme = Gen(new Palette { Background = new Color(255, 255, 255), Primary = new Color(240, 240, 0) });
		Assert.True(LumGap(theme.WindowBackgroundColor, theme.ActiveBorderForegroundColor) >= 40);
	}
}
