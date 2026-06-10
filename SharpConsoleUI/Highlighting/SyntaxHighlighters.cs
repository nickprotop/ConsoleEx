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
	/// </summary>
	public static class SyntaxHighlighters
	{
		// Highlighters are stateless across Tokenize calls (per-line state lives in SyntaxLineState),
		// so one shared instance per language is reused safely.
		private static readonly ConcurrentDictionary<string, ISyntaxHighlighter> Map =
			new(StringComparer.OrdinalIgnoreCase);

		static SyntaxHighlighters()
		{
			void Reg(ISyntaxHighlighter hl, params string[] names)
			{
				foreach (var n in names) Register(n, hl);
			}

			Reg(new CSharpSyntaxHighlighter(), "csharp", "cs");
			Reg(new JsonSyntaxHighlighter(), "json");
			Reg(new JsSyntaxHighlighter(), "javascript", "js", "node", "mjs", "cjs");
			Reg(new CssSyntaxHighlighter(), "css");
			Reg(new HtmlSyntaxHighlighter(), "html", "htm");
			Reg(new XmlSyntaxHighlighter(), "xml");
			Reg(new YamlSyntaxHighlighter(), "yaml", "yml");
			Reg(new RazorSyntaxHighlighter(), "razor", "cshtml");
			Reg(new DockerfileSyntaxHighlighter(), "dockerfile", "docker");
			Reg(new SlnSyntaxHighlighter(), "sln");
			Reg(new DiffSyntaxHighlighter(), "diff", "patch");
			Reg(new MarkdownSyntaxHighlighter(), "markdown", "md");
			Reg(new BashSyntaxHighlighter(), "bash", "sh", "shell", "zsh");
		}

		/// <summary>Returns the highlighter for a language name/alias, or null if none is registered.</summary>
		/// <param name="language">Language hint, case-insensitive (e.g. "cs", "csharp"). Null/empty → null.</param>
		public static ISyntaxHighlighter? For(string? language)
		{
			if (string.IsNullOrWhiteSpace(language))
				return null;
			return Map.TryGetValue(language.Trim(), out var hl) ? hl : null;
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
