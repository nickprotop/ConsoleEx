// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Themes;
using TextMateSharp.Themes;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Presents a <see cref="SyntaxPalette"/> to TextMateSharp as a theme, so grammar scopes
	/// resolve to the application's own colours instead of a bundled editor theme.
	/// </summary>
	public sealed class SharpConsoleUIThemeAdapter : IRawTheme
	{
		private readonly List<IRawThemeSetting> _settings = new();

		/// <summary>Builds a TextMate theme from the supplied palette.</summary>
		/// <param name="palette">The role colours to project onto TextMate scopes.</param>
		public SharpConsoleUIThemeAdapter(SyntaxPalette palette)
		{
			Add("default", string.Empty, palette.Default);
			Add("comment", "comment", palette.Comment);
			Add("string", "string, constant.character", palette.String);
			Add("number", "constant.numeric", palette.Number);
			Add("constant", "constant.language, constant.other", palette.Constant);
			Add("keyword", "keyword, storage, storage.type, storage.modifier", palette.Keyword);

			// Must follow "keyword": a bare keyword selector otherwise swallows
			// keyword.operator.* and renders '=' in the keyword colour.
			Add("operator", "keyword.operator", palette.Operator);

			Add("type", "entity.name.type, entity.name.class, support.type, support.class", palette.Type);
			Add("function", "entity.name.function, support.function", palette.Function);
			Add("variable", "variable, entity.name.variable", palette.Variable);
			Add("tag", "entity.name.tag", palette.Tag);
			Add("attribute", "entity.other.attribute-name", palette.Attribute);
			Add("punctuation", "punctuation", palette.Punctuation);

			// Punctuation that belongs to a construct must take that construct's colour, not the
			// generic punctuation colour: the "#" of a shell comment and the quotes around a
			// string are more specific than "punctuation" and would otherwise split the span.
			Add("comment-punctuation", "punctuation.definition.comment", palette.Comment);
			Add("string-punctuation", "punctuation.definition.string", palette.String);
			Add("invalid", "invalid", palette.Invalid);
		}

		private void Add(string name, string scope, Color? color)
		{
			if (color is not Color c) return;
			_settings.Add(new RawSetting(name, scope, new ThemeSetting($"#{c.R:X2}{c.G:X2}{c.B:X2}")));
		}

		/// <inheritdoc/>
		public string GetName() => "SharpConsoleUI";

		/// <inheritdoc/>
		public string? GetInclude() => null;

		/// <inheritdoc/>
		public ICollection<IRawThemeSetting> GetSettings() => _settings;

		/// <inheritdoc/>
		public ICollection<IRawThemeSetting> GetTokenColors() => _settings;

		/// <inheritdoc/>
		public ICollection<KeyValuePair<string, object>> GetGuiColors()
			=> new List<KeyValuePair<string, object>>();

		private sealed class ThemeSetting : IThemeSetting
		{
			private readonly string _foreground;

			public ThemeSetting(string foreground) => _foreground = foreground;

			public object? GetFontStyle() => null;

			public string GetForeground() => _foreground;

			public string? GetBackground() => null;
		}

		private sealed class RawSetting : IRawThemeSetting
		{
			private readonly string _name;
			private readonly object _scope;
			private readonly IThemeSetting _setting;

			public RawSetting(string name, object scope, IThemeSetting setting)
			{
				_name = name;
				_scope = scope;
				_setting = setting;
			}

			public string GetName() => _name;

			public object GetScope() => _scope;

			public IThemeSetting GetSetting() => _setting;
		}
	}
}
