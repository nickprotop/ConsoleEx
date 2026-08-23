// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using TextMateSharp.Themes;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Resolves a TextMate token's scope stack to a concrete colour, most-specific scope first,
	/// then guarantees the result stays legible against the code background.
	/// </summary>
	public sealed class ScopeColorResolver
	{
		private readonly Theme _theme;
		private readonly Color _defaultForeground;
		private readonly Color _background;
		private readonly ConcurrentDictionary<string, Color> _cache = new(StringComparer.Ordinal);

		/// <summary>Creates a resolver for one theme and background pairing.</summary>
		/// <param name="theme">The TextMate theme supplying scope colours.</param>
		/// <param name="defaultForeground">Colour for tokens no scope rule matches.</param>
		/// <param name="background">The surface the code is drawn on.</param>
		public ScopeColorResolver(Theme theme, Color defaultForeground, Color background)
		{
			_theme = theme;
			_defaultForeground = defaultForeground;
			_background = background;
		}

		/// <summary>Resolves the colour for a token's scope stack.</summary>
		/// <param name="scopes">The token's scopes, outermost first.</param>
		/// <returns>The colour to render the token in.</returns>
		public Color Resolve(IList<string> scopes)
		{
			// Most specific scope wins, so walk from the end of the stack.
			for (int i = scopes.Count - 1; i >= 0; i--)
			{
				string scope = scopes[i];
				if (_cache.TryGetValue(scope, out Color cached))
					return cached;

				foreach (ThemeTrieElementRule rule in _theme.Match(new[] { scope }))
				{
					if (rule.foreground <= 0) continue;

					Color resolved = Parse(_theme.GetColor(rule.foreground));
					_cache[scope] = resolved;
					return resolved;
				}
			}

			return _defaultForeground;
		}

		private Color Parse(string hex)
		{
			// TextMate themes emit "#RRGGBB"; anything else falls back to the default foreground.
			if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#')
				return _defaultForeground;

			byte r = Convert.ToByte(hex.Substring(1, 2), 16);
			byte g = Convert.ToByte(hex.Substring(3, 2), 16);
			byte b = Convert.ToByte(hex.Substring(5, 2), 16);

			return PaletteColors.EnsureContrast(
				new Color(r, g, b), _background, ControlDefaults.SyntaxMinimumContrastGap);
		}
	}
}
