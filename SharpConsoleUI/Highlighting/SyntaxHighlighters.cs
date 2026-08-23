// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Highlighting
{
	/// <summary>
	/// Central registry mapping language names (and aliases) to <see cref="ISyntaxHighlighter"/>
	/// instances. The single source of truth for the language→highlighter mapping, shared by the
	/// markdown code-block renderer and any other consumer (e.g. MultilineEditControl), so the
	/// mapping is never duplicated per consumer.
	/// <para>
	/// Languages that are not explicitly registered fall back to a TextMate grammar, so roughly
	/// 64 languages resolve out of the box with no initialization call. Grammars load on first
	/// use. An explicit <see cref="Register"/> call always takes precedence over the fallback.
	/// </para>
	/// </summary>
	public static class SyntaxHighlighters
	{
		// Highlighters are stateless across Tokenize calls (per-line state lives in SyntaxLineState),
		// so one shared instance per language is reused safely.
		private static readonly ConcurrentDictionary<string, ISyntaxHighlighter> Map =
			new(StringComparer.OrdinalIgnoreCase);

		// Maps historical alias spellings onto TextMate language ids.
		private static readonly ConcurrentDictionary<string, string> Aliases =
			new(StringComparer.OrdinalIgnoreCase);

		static SyntaxHighlighters()
		{
			// Only what the TextMate fallback cannot resolve is pre-registered. Everything else
			// (csharp/cs, json, javascript/js/mjs/cjs, css, html/htm, xml, yaml/yml, razor/cshtml,
			// dockerfile/docker, diff/patch, markdown/md, bash/sh/zsh) resolves lazily through
			// TextMate. Registering them here would both add a redundant hop and force the engine
			// to be built during static initialization, defeating the lazy default.
#pragma warning disable CS0618 // SlnSyntaxHighlighter has no TextMate grammar and stays regex-based.
			Register("sln", new SlnSyntaxHighlighter());
#pragma warning restore CS0618

			// Aliases TextMate does not know, bridged onto the language ids it does.
			Aliases["node"] = "javascript";
			Aliases["shell"] = "shellscript";
		}

		/// <summary>Returns the highlighter for a language name/alias, or null if none is registered.</summary>
		/// <param name="language">Language hint, case-insensitive (e.g. "cs", "csharp"). Null/empty → null.</param>
		public static ISyntaxHighlighter? For(string? language)
		{
			if (string.IsNullOrWhiteSpace(language))
				return null;

			string key = language.Trim();
			if (Map.TryGetValue(key, out var hl))
				return hl;

			// Translate historical aliases onto TextMate language ids.
			if (Aliases.TryGetValue(key, out var mapped))
				key = mapped;

			// Fall back to a TextMate grammar. Grammars load lazily, so languages nobody asks
			// for cost nothing. An explicit Register() call always takes precedence.
			return TextMate.TextMateEngine.Instance.GetHighlighter(key);
		}

		/// <summary>Registers (or overrides) a highlighter for a language name/alias. Additive; built-ins remain.</summary>
		/// <param name="language">The language name or alias to register.</param>
		/// <param name="highlighter">The highlighter instance.</param>
		public static void Register(string language, ISyntaxHighlighter highlighter)
		{
			if (string.IsNullOrWhiteSpace(language)) return;
			Map[language.Trim()] = highlighter;
		}

		/// <summary>True if a highlighter is registered for the language/alias.</summary>
		public static bool Has(string? language) => For(language) != null;
	}
}
