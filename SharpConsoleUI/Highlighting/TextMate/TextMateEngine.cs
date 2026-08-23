// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Themes;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TmRegistry = TextMateSharp.Registry.Registry;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Owns the shared TextMate grammar registry and caches one highlighter per language.
	/// Grammars load on first use, so an application that never highlights pays no startup cost.
	/// </summary>
	public sealed class TextMateEngine
	{
		/// <summary>The process-wide engine instance.</summary>
		public static TextMateEngine Instance { get; } = new();

		private readonly RegistryOptions _options = new(ThemeName.DarkPlus);
		private readonly TmRegistry _registry;
		// Alias (as requested by callers) -> highlighter.
		private readonly ConcurrentDictionary<string, TextMateHighlighter?> _byAlias =
			new(StringComparer.OrdinalIgnoreCase);

		// Resolved language id -> highlighter. Several aliases collapse onto one entry here.
		private readonly Dictionary<string, TextMateHighlighter?> _byLanguageId =
			new(StringComparer.OrdinalIgnoreCase);

		// TextMateSharp's registry throws if the same grammar scope is loaded twice, so all
		// grammar loading is serialized.
		private readonly object _loadLock = new();
		private readonly object _themeLock = new();

		// volatile: read by Tokenize on render threads without taking _themeLock.
		private volatile ScopeColorResolver _resolver;
		private Color _background = Color.Black;

		private TextMateEngine()
		{
			_registry = new TmRegistry(_options);
			SyntaxPalette palette = SyntaxPalette.DeriveFrom(new ModernGrayTheme());
			_registry.SetTheme(new SharpConsoleUIThemeAdapter(palette));
			_resolver = BuildResolver(palette);
		}

		/// <summary>
		/// The active colour resolver. Read per tokenize call so a theme swap takes effect on
		/// highlighters that were already handed out.
		/// </summary>
		internal ScopeColorResolver Resolver => _resolver;

		/// <summary>Recolours every language from the supplied palette.</summary>
		/// <param name="palette">The role colours to apply.</param>
		/// <param name="background">The surface code is drawn on, used for the contrast floor.</param>
		public void SetTheme(SyntaxPalette palette, Color? background = null)
		{
			lock (_themeLock)
			{
				if (background is Color bg) _background = bg;
				_registry.SetTheme(new SharpConsoleUIThemeAdapter(palette));
				_resolver = BuildResolver(palette);
			}
		}

		/// <summary>Switches to one of TextMateSharp's bundled editor themes.</summary>
		/// <param name="themeName">The bundled theme to apply.</param>
		public void SetBundledTheme(ThemeName themeName)
		{
			lock (_themeLock)
			{
				_registry.SetTheme(_options.LoadTheme(themeName));
				_resolver = new ScopeColorResolver(_registry.GetTheme(), Color.Silver, _background);
			}
		}

		/// <summary>Returns a highlighter for a language name or alias, or null if no grammar exists.</summary>
		/// <param name="language">Language name or alias (for example "csharp", "cs", "rust").</param>
		/// <returns>A cached highlighter, or null when the language has no TextMate grammar.</returns>
		public TextMateHighlighter? GetHighlighter(string? language)
		{
			if (string.IsNullOrWhiteSpace(language)) return null;

			string key = language.Trim();

			// Alias -> highlighter, so repeat lookups skip the language-table scan.
			if (_byAlias.TryGetValue(key, out TextMateHighlighter? aliased))
				return aliased;

			TextMateHighlighter? resolved = Load(key);
			_byAlias[key] = resolved;
			return resolved;
		}

		private TextMateHighlighter? Load(string language)
		{
			// Lookup order matters: id -> alias -> extension.
			//   id only        misses "cs", "js", "md", "yml"
			//   extension only misses "csharp", "rust", "python", "shellscript"
			// "bash"/"sh"/"zsh" resolve ONLY through Aliases (to the "shellscript" language).
			Language? lang = FindLanguage(language);
			if (lang == null) return null;

			// Several aliases share one language, so cache by resolved id: this keeps a single
			// highlighter per language AND ensures LoadGrammar is called once per scope
			// (calling it twice for the same scope throws inside TextMateSharp's registry).
			lock (_loadLock)
			{
				if (_byLanguageId.TryGetValue(lang.Id, out TextMateHighlighter? existing))
					return existing;

				string scope = _options.GetScopeByLanguageId(lang.Id);
				if (string.IsNullOrEmpty(scope)) return null;

				IGrammar? grammar = _registry.LoadGrammar(scope);
				TextMateHighlighter? highlighter =
					grammar == null ? null : new TextMateHighlighter(grammar, this);

				_byLanguageId[lang.Id] = highlighter;
				return highlighter;
			}
		}

		private Language? FindLanguage(string language)
		{
			List<Language> languages = _options.GetAvailableLanguages();

			foreach (Language candidate in languages)
			{
				if (string.Equals(candidate.Id, language, StringComparison.OrdinalIgnoreCase))
					return candidate;
			}

			foreach (Language candidate in languages)
			{
				if (candidate.Aliases == null) continue;
				foreach (string alias in candidate.Aliases)
				{
					if (string.Equals(alias, language, StringComparison.OrdinalIgnoreCase))
						return candidate;
				}
			}

			return _options.GetLanguageByExtension("." + language);
		}

		private ScopeColorResolver BuildResolver(SyntaxPalette palette)
			=> new(_registry.GetTheme(), palette.Default ?? Color.Silver, _background);
	}
}
