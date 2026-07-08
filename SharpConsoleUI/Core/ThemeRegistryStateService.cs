// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Per-<see cref="ConsoleWindowSystem"/> registry of available themes. Registration, lookup, and
	/// enumeration are scoped to the owning window system, so themes registered (including those
	/// contributed by a loaded plugin) never leak across instances. Pre-seeded with the built-in
	/// <c>ModernGray</c> theme plus the palette-generated seed catalog. Thread-safe.
	/// </summary>
	public class ThemeRegistryStateService
	{
		private readonly ConcurrentDictionary<string, ThemeInfo> _themes = new();
		private readonly object _defaultThemeLock = new();
		private string _defaultThemeName = "ModernGray";

		/// <summary>
		/// Creates a registry pre-seeded with the built-in <c>ModernGray</c> theme and the seed catalog.
		/// </summary>
		public ThemeRegistryStateService()
		{
			RegisterTheme(
				"ModernGray",
				"Professional dark theme with grayscale foundation and cyan accents, inspired by modern developer tools",
				() => new ModernGrayTheme());

			// Seed catalog: palette-generated themes available to every app (cheap to add).
			// Do NOT change DefaultThemeName — ModernGray stays the default.
			RegisterTheme("Ocean", "Teal accent on a deep dark surface",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#2DD4BF"), Background = Color.FromHex("#0B1F2A") }));
			RegisterTheme("Amber", "Warm amber accent, dark",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#F59E0B"), Background = Color.FromHex("#1C1917") }));
			RegisterTheme("Forest", "Green accent, dark",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#22C55E"), Background = Color.FromHex("#0F1A12") }));
			RegisterTheme("Crimson", "Red accent, dark",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#EF4444"), Background = Color.FromHex("#1A0F12") }));
			RegisterTheme("Slate", "Cool blue-grey accent, dark",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#64748B"), Background = Color.FromHex("#0F172A") }));
			RegisterTheme("Daylight", "Blue accent on a light surface",
				() => Themes.Theme.FromPalette(new Themes.Palette { Primary = Color.FromHex("#2563EB"), Background = Color.FromHex("#DFE3E9") }));
		}

		/// <summary>
		/// Gets or sets the name of the default theme to use when none is specified. Thread-safe.
		/// </summary>
		public string DefaultThemeName
		{
			get
			{
				lock (_defaultThemeLock)
				{
					return _defaultThemeName;
				}
			}
			set
			{
				if (string.IsNullOrWhiteSpace(value))
					throw new ArgumentException("Default theme name cannot be null or empty.", nameof(value));

				lock (_defaultThemeLock)
				{
					_defaultThemeName = value;
				}
			}
		}

		/// <summary>
		/// Registers a theme. If a theme with the same name already exists, it is replaced.
		/// </summary>
		public void RegisterTheme(string name, string description, Func<ITheme> factory)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Theme name cannot be empty or whitespace.", nameof(name));
			if (description == null)
				throw new ArgumentNullException(nameof(description));
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));

			_themes[name] = new ThemeInfo(name, description, factory);
		}

		/// <summary>Unregisters a theme. Returns true if it was removed, false if not found.</summary>
		public bool UnregisterTheme(string name)
		{
			if (name == null)
				return false;

			return _themes.TryRemove(name, out _);
		}

		/// <summary>Gets a new instance of the named theme, or null if not registered.</summary>
		public ITheme? GetTheme(string name)
		{
			if (name == null)
				return null;

			if (!_themes.TryGetValue(name, out var info))
				return null;

			var theme = info.Factory();
			// Stamp the registration name onto the produced theme so CurrentTheme.Name reflects the
			// theme the caller asked for. Palette/generated themes otherwise report a generic name
			// (e.g. "Custom (Palette)"), which breaks name-based lookups like "is this the current theme".
			if (theme is MutableTheme mutable)
				mutable.NameValue = name;
			return theme;
		}

		/// <summary>Gets the named theme, or <paramref name="defaultTheme"/> if not registered.</summary>
		public ITheme GetThemeOrDefault(string name, ITheme defaultTheme)
		{
			if (defaultTheme == null)
				throw new ArgumentNullException(nameof(defaultTheme));

			return GetTheme(name) ?? defaultTheme;
		}

		/// <summary>
		/// Gets a new instance of the configured default theme (<see cref="DefaultThemeName"/>).
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if the default theme is not registered.</exception>
		public ITheme GetDefaultTheme()
		{
			string themeName = DefaultThemeName;
			var theme = GetTheme(themeName);
			if (theme == null)
				throw new InvalidOperationException($"Default theme '{themeName}' is not registered. " +
					"Ensure the theme is registered before calling GetDefaultTheme().");
			return theme;
		}

		/// <summary>Gets all available theme names, sorted.</summary>
		public IReadOnlyList<string> GetAvailableThemeNames()
			=> _themes.Keys.OrderBy(name => name).ToList();

		/// <summary>Gets all available themes with their information, sorted by name.</summary>
		public IReadOnlyList<ThemeInfo> GetAvailableThemes()
			=> _themes.Values.OrderBy(info => info.Name).ToList();

		/// <summary>Checks whether a theme with the given name is registered.</summary>
		public bool IsThemeRegistered(string name)
			=> name != null && _themes.ContainsKey(name);

		/// <summary>Gets the number of registered themes.</summary>
		public int Count => _themes.Count;
	}
}
