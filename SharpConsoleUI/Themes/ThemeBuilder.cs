// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Entry point for deriving a theme from an existing one. Copies every member of a base
	/// <see cref="ITheme"/>, lets you override any subset, and returns a mutable theme you can register
	/// and switch to.
	/// </summary>
	/// <example>
	/// <code>
	/// var myDark = Theme.From(new ModernGrayTheme())
	///     .WithName("MyDark")
	///     .With(t => t.ButtonBackgroundColor = Color.DarkRed)
	///     .Build();
	///
	/// windowSystem.ThemeRegistryService.RegisterTheme("MyDark", "My dark variant", () => myDark);
	/// windowSystem.ThemeStateService.SwitchTheme("MyDark");
	/// </code>
	/// </example>
	public static class Theme
	{
		/// <summary>
		/// Begins deriving a theme from <paramref name="baseTheme"/>. The returned builder works on a
		/// fresh <see cref="MutableTheme"/> seeded with every member value copied from the base.
		/// </summary>
		/// <param name="baseTheme">The theme to copy all member values from.</param>
		/// <returns>A <see cref="ThemeBuilder"/> for further customization.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="baseTheme"/> is null.</exception>
		public static ThemeBuilder From(ITheme baseTheme)
		{
			if (baseTheme == null) throw new ArgumentNullException(nameof(baseTheme));
			return new ThemeBuilder(new MutableTheme().CopyFrom(baseTheme));
		}

		/// <summary>
		/// Generates a complete theme from a small <see cref="Palette"/> of seed colors. Everything not
		/// supplied is derived (see <see cref="Palette"/>). Returns a mutable theme you can register and
		/// switch to like any other.
		/// </summary>
		/// <param name="palette">The seed colors.</param>
		/// <returns>The generated (mutable) theme.</returns>
		/// <exception cref="System.ArgumentNullException"><paramref name="palette"/> is null.</exception>
		public static MutableTheme FromPalette(Palette palette)
		{
			if (palette == null) throw new System.ArgumentNullException(nameof(palette));
			return PaletteThemeGenerator.Generate(palette);
		}
	}

	/// <summary>
	/// Fluent builder produced by <see cref="Theme.From(ITheme)"/>. Mutates a working
	/// <see cref="MutableTheme"/> and returns it from <see cref="Build"/> (no copy, no freeze — the
	/// result stays mutable by design).
	/// </summary>
	public sealed class ThemeBuilder
	{
		private readonly MutableTheme _theme;

		internal ThemeBuilder(MutableTheme theme)
		{
			_theme = theme;
		}

		/// <summary>Sets the derived theme's <see cref="ITheme.Name"/>.</summary>
		/// <param name="name">The theme name. Null/empty leaves the copied base name unchanged.</param>
		/// <returns>This builder (for chaining).</returns>
		public ThemeBuilder WithName(string name)
		{
			if (!string.IsNullOrEmpty(name)) _theme.NameValue = name;
			return this;
		}

		/// <summary>Sets the derived theme's <see cref="ITheme.Description"/>.</summary>
		/// <param name="description">The theme description. Null/empty leaves the copied base description unchanged.</param>
		/// <returns>This builder (for chaining).</returns>
		public ThemeBuilder WithDescription(string description)
		{
			if (!string.IsNullOrEmpty(description)) _theme.DescriptionValue = description;
			return this;
		}

		/// <summary>
		/// Applies <paramref name="mutate"/> to the working theme so you can override any member(s).
		/// Multiple calls accumulate.
		/// </summary>
		/// <param name="mutate">An action that mutates the working <see cref="MutableTheme"/>.</param>
		/// <returns>This builder (for chaining).</returns>
		/// <exception cref="ArgumentNullException"><paramref name="mutate"/> is null.</exception>
		public ThemeBuilder With(Action<MutableTheme> mutate)
		{
			if (mutate == null) throw new ArgumentNullException(nameof(mutate));
			mutate(_theme);
			return this;
		}

		/// <summary>Returns the built (mutable) theme.</summary>
		/// <returns>The working <see cref="MutableTheme"/>.</returns>
		public MutableTheme Build() => _theme;
	}
}
