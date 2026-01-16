// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Information about a registered theme including its name, description, and factory method.
	/// </summary>
	/// <param name="Name">The unique name of the theme.</param>
	/// <param name="Description">A description of the theme's visual style and characteristics.</param>
	/// <param name="Factory">A factory method that creates a new instance of the theme.</param>
	public record ThemeInfo(string Name, string Description, Func<ITheme> Factory);

	/// <summary>
	/// Central registry for discovering and accessing available themes by name.
	/// Provides thread-safe theme registration, lookup, and management.
	/// </summary>
	public static class ThemeRegistry
	{
		private static readonly ConcurrentDictionary<string, ThemeInfo> _themes = new();
		private static readonly object _defaultThemeLock = new();
		private static string _defaultThemeName = "ModernGray";

		/// <summary>
		/// Static constructor that registers built-in themes.
		/// </summary>
		static ThemeRegistry()
		{
			// Register built-in themes
			RegisterTheme(
				"Classic",
				"Classic Windows-style theme with bright blue and green accents",
				() => new ClassicTheme()
			);

			RegisterTheme(
				"ModernGray",
				"Professional dark theme with grayscale foundation and cyan accents, inspired by modern developer tools",
				() => new ModernGrayTheme()
			);
		}

		/// <summary>
		/// Gets or sets the name of the default theme to use when none is specified.
		/// Thread-safe property with synchronized access.
		/// </summary>
		public static string DefaultThemeName
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
		/// Registers a theme in the registry.
		/// If a theme with the same name already exists, it will be replaced.
		/// </summary>
		/// <param name="name">The unique name for the theme.</param>
		/// <param name="description">A description of the theme.</param>
		/// <param name="factory">A factory method that creates new instances of the theme.</param>
		/// <exception cref="ArgumentNullException">Thrown if name, description, or factory is null.</exception>
		/// <exception cref="ArgumentException">Thrown if name is empty or whitespace.</exception>
		public static void RegisterTheme(string name, string description, Func<ITheme> factory)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Theme name cannot be empty or whitespace.", nameof(name));
			if (description == null)
				throw new ArgumentNullException(nameof(description));
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));

			var info = new ThemeInfo(name, description, factory);
			_themes[name] = info;
		}

		/// <summary>
		/// Unregisters a theme from the registry.
		/// </summary>
		/// <param name="name">The name of the theme to unregister.</param>
		/// <returns>True if the theme was removed, false if it was not found.</returns>
		public static bool UnregisterTheme(string name)
		{
			if (name == null)
				return false;

			return _themes.TryRemove(name, out _);
		}

		/// <summary>
		/// Gets a theme by name.
		/// </summary>
		/// <param name="name">The name of the theme to retrieve.</param>
		/// <returns>A new instance of the theme, or null if not found.</returns>
		public static ITheme? GetTheme(string name)
		{
			if (name == null)
				return null;

			if (_themes.TryGetValue(name, out var info))
				return info.Factory();

			return null;
		}

		/// <summary>
		/// Gets a theme by name, or returns a default theme if not found.
		/// </summary>
		/// <param name="name">The name of the theme to retrieve.</param>
		/// <param name="defaultTheme">The theme to return if the named theme is not found.</param>
		/// <returns>A new instance of the named theme, or the default theme if not found.</returns>
		public static ITheme GetThemeOrDefault(string name, ITheme defaultTheme)
		{
			if (defaultTheme == null)
				throw new ArgumentNullException(nameof(defaultTheme));

			var theme = GetTheme(name);
			return theme ?? defaultTheme;
		}

		/// <summary>
		/// Gets the default theme as configured by <see cref="DefaultThemeName"/>.
		/// </summary>
		/// <returns>A new instance of the default theme.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the default theme is not registered.</exception>
		public static ITheme GetDefaultTheme()
		{
			string themeName = DefaultThemeName;  // Thread-safe property access
			var theme = GetTheme(themeName);
			if (theme == null)
				throw new InvalidOperationException($"Default theme '{themeName}' is not registered. " +
				                                    "Ensure the theme is registered before calling GetDefaultTheme().");
			return theme;
		}

		/// <summary>
		/// Gets a list of all available theme names.
		/// </summary>
		/// <returns>A read-only list of theme names.</returns>
		public static IReadOnlyList<string> GetAvailableThemeNames()
		{
			return _themes.Keys.OrderBy(name => name).ToList();
		}

		/// <summary>
		/// Gets a list of all available themes with their information.
		/// </summary>
		/// <returns>A read-only list of theme information records.</returns>
		public static IReadOnlyList<ThemeInfo> GetAvailableThemes()
		{
			return _themes.Values.OrderBy(info => info.Name).ToList();
		}

		/// <summary>
		/// Checks if a theme with the specified name is registered.
		/// </summary>
		/// <param name="name">The name of the theme to check.</param>
		/// <returns>True if the theme is registered, false otherwise.</returns>
		public static bool IsThemeRegistered(string name)
		{
			if (name == null)
				return false;

			return _themes.ContainsKey(name);
		}

		/// <summary>
		/// Gets the number of registered themes.
		/// </summary>
		public static int Count => _themes.Count;
	}
}
