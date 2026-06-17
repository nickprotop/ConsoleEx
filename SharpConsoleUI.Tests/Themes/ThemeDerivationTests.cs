using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Tests for theme derivation: <see cref="Theme.From(ITheme)"/> + <see cref="ThemeBuilder"/> +
/// <see cref="MutableTheme.CopyFrom(ITheme)"/>.
/// </summary>
public class ThemeDerivationTests
{
	/// <summary>
	/// The tripwire: every readable <see cref="ITheme"/> member of a derived theme must equal the base
	/// theme's value. If someone adds an <see cref="ITheme"/> member and forgets to copy it in
	/// <see cref="MutableTheme.CopyFrom"/>, this fails (the derived value falls to MutableTheme's
	/// bare default instead of the fully-populated built-in source value). The specific base theme
	/// doesn't matter — only that it is a fully-populated built-in.
	/// </summary>
	[Fact]
	public void CopyFrom_CopiesEveryIThemeMember()
	{
		var baseTheme = new ModernGrayTheme();
		var derived = Theme.From(baseTheme).Build();

		var properties = typeof(ITheme).GetProperties(BindingFlags.Public | BindingFlags.Instance);
		Assert.NotEmpty(properties);

		var mismatches = new List<string>();
		foreach (var prop in properties)
		{
			if (prop.GetIndexParameters().Length > 0) continue; // skip indexers (none expected)
			var expected = prop.GetValue(baseTheme);
			var actual = prop.GetValue(derived);
			if (!Equals(expected, actual))
				mismatches.Add($"{prop.Name}: expected {expected}, got {actual}");
		}

		Assert.True(mismatches.Count == 0,
			"CopyFrom missed ITheme member(s):\n" + string.Join("\n", mismatches));
	}

	[Fact]
	public void From_Null_Throws()
	{
		Assert.Throws<ArgumentNullException>(() => Theme.From(null!));
	}

	[Fact]
	public void With_Null_Throws()
	{
		var builder = Theme.From(new ModernGrayTheme());
		Assert.Throws<ArgumentNullException>(() => builder.With(null!));
	}

	[Fact]
	public void With_OverridesOnlyTheTouchedMember()
	{
		var baseTheme = new ModernGrayTheme();
		var derived = Theme.From(baseTheme)
			.With(t => t.ButtonBackgroundColor = Color.Red)
			.Build();

		Assert.Equal(Color.Red, derived.ButtonBackgroundColor);
		// An untouched member still equals the base.
		Assert.Equal(baseTheme.WindowBackgroundColor, derived.WindowBackgroundColor);
	}

	[Fact]
	public void With_MultipleCallsAccumulate()
	{
		var derived = Theme.From(new ModernGrayTheme())
			.With(t => t.ButtonBackgroundColor = Color.Red)
			.With(t => t.ActiveBorderForegroundColor = Color.Orange1)
			.Build();

		Assert.Equal(Color.Red, derived.ButtonBackgroundColor);
		Assert.Equal(Color.Orange1, derived.ActiveBorderForegroundColor);
	}

	[Fact]
	public void WithName_AndWithDescription_SetMembers()
	{
		var derived = Theme.From(new ModernGrayTheme())
			.WithName("MyDark")
			.WithDescription("My dark variant")
			.Build();

		Assert.Equal("MyDark", derived.Name);
		Assert.Equal("My dark variant", derived.Description);
	}

	[Fact]
	public void WithName_NullOrEmpty_KeepsBaseName()
	{
		var derived = Theme.From(new ModernGrayTheme())
			.WithName("")
			.Build();

		Assert.Equal(new ModernGrayTheme().Name, derived.Name);
	}

	/// <summary>
	/// The general scrollbar colors and collapsible-header focused colors are settable directly on the
	/// derived theme (they used to be interface-default-only), and the override survives when read back
	/// through an <see cref="ITheme"/> reference — which is how controls consume them.
	/// </summary>
	[Fact]
	public void ScrollbarColorOverride_VisibleThroughIThemeReference()
	{
		ITheme derived = Theme.From(new ModernGrayTheme())
			.With(t =>
			{
				t.ScrollbarThumbColor = Color.Red;
				t.ScrollbarTrackColor = Color.Blue;
				t.CollapsibleHeaderFocusedBackgroundColor = Color.Green;
			})
			.Build();

		Assert.Equal(Color.Red, derived.ScrollbarThumbColor);
		Assert.Equal(Color.Blue, derived.ScrollbarTrackColor);
		Assert.Equal(Color.Green, derived.CollapsibleHeaderFocusedBackgroundColor);
	}

	/// <summary>Themes are mutable by design: setting a color on a built/concrete theme takes effect directly.</summary>
	[Fact]
	public void ConcreteTheme_ScrollbarColor_IsDirectlySettable()
	{
		var theme = new ModernGrayTheme();
		theme.ScrollbarThumbColor = Color.HotPink;
		Assert.Equal(Color.HotPink, theme.ScrollbarThumbColor);
		Assert.Equal(Color.HotPink, ((ITheme)theme).ScrollbarThumbColor);
	}

	[Fact]
	public void Build_ReturnsMutableInstance_MutationsFlowThrough()
	{
		var built = Theme.From(new ModernGrayTheme()).Build();
		// Single shared instance: later mutation is visible (the accepted live-tweak footgun).
		built.ButtonBackgroundColor = Color.Yellow;
		Assert.Equal(Color.Yellow, built.ButtonBackgroundColor);
	}

	[Fact]
	public void RegisterAndSwitch_EndToEnd_MakesDerivedThemeLive()
	{
		var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

		var derived = Theme.From(new ModernGrayTheme())
			.WithName("MyDark")
			.With(t => t.ButtonBackgroundColor = Color.DarkRed)
			.Build();

		system.ThemeRegistryService.RegisterTheme("MyDark", "My dark variant", () => derived);
		Assert.True(system.ThemeStateService.SwitchTheme("MyDark"));

		Assert.Equal("MyDark", system.Theme.Name);
		Assert.Equal(Color.DarkRed, system.Theme.ButtonBackgroundColor);
	}

	[Fact]
	public void BareMutableTheme_HasBlankNeutralDefaults_NotModernGray()
	{
		var t = new MutableTheme();
		// Blank canvas: window bg is transparent/default, NOT ModernGray's Grey15 — proves decoupling.
		Assert.NotEqual(new ModernGrayTheme().WindowBackgroundColor, t.WindowBackgroundColor);
		Assert.True(t.WindowBackgroundColor.A == 0 || t.WindowBackgroundColor.IsDefault,
			"bare MutableTheme window background should be transparent/default");
	}
}
