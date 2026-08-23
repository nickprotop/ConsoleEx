// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using TextMateSharp.Grammars;

namespace SharpConsoleUI.Highlighting.TextMate
{
	/// <summary>
	/// Colours one language using its TextMate grammar. Parser state is carried between lines,
	/// so constructs that span lines (block comments, here-docs) highlight correctly.
	/// </summary>
	public sealed class TextMateHighlighter : ISyntaxHighlighter
	{
		private readonly IGrammar _grammar;
		private readonly TextMateEngine _engine;

		// TextMateSharp compiles grammar rules lazily *during* tokenization and mutates shared
		// per-grammar state while doing so, so concurrent TokenizeLine calls on one grammar
		// corrupt it (verified: 64/64 threads throw on a freshly loaded grammar). Windows render
		// on independent threads here, so every call is serialized per grammar.
		private readonly object _tokenizeLock = new();

		// Tokenizing is the expensive part of a repaint, and callers legitimately re-tokenize the
		// same text: a control may drop its own token cache, and several controls may show the
		// same snippet. Results are keyed by (line text + start state), which is exactly what
		// determines the output, so a hit is always correct.
		private readonly Dictionary<CacheKey, CacheEntry> _cache = new();
		private readonly LinkedList<CacheKey> _lru = new();

		internal TextMateHighlighter(IGrammar grammar, TextMateEngine engine)
		{
			_grammar = grammar;
			_engine = engine;
		}

		/// <inheritdoc/>
		public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
			Tokenize(string line, int lineIndex, SyntaxLineState startState)
		{
			IStateStack? stack = (startState as TextMateLineState)?.Stack;

			var key = new CacheKey(line, stack);

			lock (_tokenizeLock)
			{
				if (_cache.TryGetValue(key, out CacheEntry hit))
				{
					// Colours can change under a theme swap while the tokens stay valid, so a hit
					// is only reusable when it was produced by the resolver still in force.
					if (ReferenceEquals(hit.Resolver, _engine.Resolver))
					{
						Touch(key);
						return (hit.Tokens, hit.EndState);
					}

					Remove(key);
				}
			}

			ITokenizeLineResult result;
			lock (_tokenizeLock)
			{
				result = _grammar.TokenizeLine(
					line, stack, TimeSpan.FromMilliseconds(ControlDefaults.SyntaxTokenizeTimeoutMs));
			}

			// Read the resolver per call rather than capturing it, so a theme swap reaches
			// highlighters that were handed out earlier and are cached by their callers.
			ScopeColorResolver resolver = _engine.Resolver;

			var tokens = new List<SyntaxToken>(result.Tokens.Length);
			foreach (IToken token in result.Tokens)
			{
				int end = Math.Min(token.EndIndex, line.Length);
				int length = end - token.StartIndex;
				if (length <= 0) continue;

				tokens.Add(new SyntaxToken(token.StartIndex, length, resolver.Resolve(token.Scopes)));
			}

			var endState = new TextMateLineState(result.RuleStack);

			lock (_tokenizeLock)
			{
				Store(key, new CacheEntry(tokens, endState, resolver));
			}

			return (tokens, endState);
		}

		// --- token cache -------------------------------------------------------------------

		private void Touch(CacheKey key)
		{
			if (_cache.TryGetValue(key, out CacheEntry entry) && entry.Node != null)
			{
				_lru.Remove(entry.Node);
				_lru.AddLast(entry.Node);
			}
		}

		private void Remove(CacheKey key)
		{
			if (!_cache.TryGetValue(key, out CacheEntry entry)) return;
			if (entry.Node != null) _lru.Remove(entry.Node);
			_cache.Remove(key);
		}

		private void Store(CacheKey key, CacheEntry entry)
		{
			Remove(key);

			while (_cache.Count >= ControlDefaults.SyntaxTokenCacheSize && _lru.First != null)
			{
				CacheKey oldest = _lru.First.Value;
				_lru.RemoveFirst();
				_cache.Remove(oldest);
			}

			entry.Node = _lru.AddLast(key);
			_cache[key] = entry;
		}

		// The output of TokenizeLine depends only on the line text and the incoming rule stack,
		// so those two together identify a result. The stack is compared by reference: TextMate
		// hands back the same instance for equivalent states, and a miss only costs a re-tokenize.
		private readonly struct CacheKey : IEquatable<CacheKey>
		{
			private readonly string _line;
			private readonly IStateStack? _stack;

			public CacheKey(string line, IStateStack? stack)
			{
				_line = line;
				_stack = stack;
			}

			public bool Equals(CacheKey other)
				=> ReferenceEquals(_stack, other._stack)
				   && string.Equals(_line, other._line, StringComparison.Ordinal);

			public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

			public override int GetHashCode()
				=> HashCode.Combine(
					_line.GetHashCode(StringComparison.Ordinal),
					_stack == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_stack));
		}

		private sealed class CacheEntry
		{
			public CacheEntry(
				IReadOnlyList<SyntaxToken> tokens, SyntaxLineState endState, ScopeColorResolver resolver)
			{
				Tokens = tokens;
				EndState = endState;
				Resolver = resolver;
			}

			public IReadOnlyList<SyntaxToken> Tokens { get; }
			public SyntaxLineState EndState { get; }
			public ScopeColorResolver Resolver { get; }
			public LinkedListNode<CacheKey>? Node { get; set; }
		}
	}
}
