// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Highlighting;
using SharpConsoleUI.Highlighting.TextMate;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Highlighting.TextMate
{
	public class TokenCacheTests
	{
		[Fact]
		public void RepeatedTokenize_ReturnsIdenticalTokens()
		{
			var hl = SyntaxHighlighters.For("csharp")!;
			const string line = "public class Foo { int x = 42; }";

			var (first, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);
			var (second, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);

			Assert.Equal(first.Count, second.Count);
			for (int i = 0; i < first.Count; i++)
			{
				Assert.Equal(first[i].StartIndex, second[i].StartIndex);
				Assert.Equal(first[i].Length, second[i].Length);
				Assert.Equal(first[i].ForegroundColor, second[i].ForegroundColor);
			}
		}

		[Fact]
		public void RepeatedTokenize_IsServedFromCache_NotRecomputed()
		{
			// Use a language nothing else has warmed, so the first pass pays real grammar cost.
			var hl = SyntaxHighlighters.For("ruby")!;
			string[] lines = Enumerable.Range(0, 200)
				.Select(i => $"def method_{i}(a, b) # comment {i}")
				.ToArray();

			void Pass()
			{
				SyntaxLineState state = SyntaxLineState.Initial;
				foreach (string line in lines)
				{
					var (_, next) = hl.Tokenize(line, 0, state);
					state = next;
				}
			}

			Pass();                                   // warm: compiles rules + fills the cache
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < 5; i++) Pass();       // 5 more passes, all cache hits
			sw.Stop();

			// 1000 cached tokenizations must be far cheaper than uncached work (~0.24ms/line
			// uncached would be ~240ms). A generous bound keeps this stable on slow CI.
			Assert.True(sw.ElapsedMilliseconds < 100,
				$"1000 cached tokenizations took {sw.ElapsedMilliseconds}ms — cache not effective");
		}

		[Fact]
		public void DifferentStartState_ProducesDifferentResult_NotACacheCollision()
		{
			var hl = SyntaxHighlighters.For("csharp")!;

			var (_, insideComment) = hl.Tokenize("/* open", 0, SyntaxLineState.Initial);

			// Same text, different incoming state: the cache must not conflate them.
			var (fresh, _) = hl.Tokenize("int x;", 1, SyntaxLineState.Initial);
			var (commented, _) = hl.Tokenize("int x;", 1, insideComment);

			Assert.NotEqual(fresh[0].ForegroundColor, commented[0].ForegroundColor);
		}

		[Fact]
		public void ThemeChange_InvalidatesCachedColours()
		{
			var hl = SyntaxHighlighters.For("csharp")!;
			const string line = "int x = 42;";

			var (before, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);
			Color originalKeyword = before[0].ForegroundColor;

			try
			{
				TextMateHighlighting.UseTheme(new SyntaxPalette
				{
					Default = Color.White,
					Keyword = Color.Red,
					Number = Color.Green,
					String = Color.Blue,
					Comment = Color.Grey,
				}, Color.Black);

				var (after, _) = hl.Tokenize(line, 0, SyntaxLineState.Initial);

				// Same line, same state — but a new theme must not serve stale colours.
				Assert.NotEqual(originalKeyword, after[0].ForegroundColor);
			}
			finally
			{
				// Restore so later tests see the default palette.
				TextMateHighlighting.UseTheme(SyntaxPalette.DeriveFrom(new ModernGrayTheme()));
			}
		}

		[Fact]
		public void Cache_IsBounded_DoesNotGrowWithoutLimit()
		{
			var hl = SyntaxHighlighters.For("go")!;

			// Far more distinct lines than the cache bound; must not throw or exhaust memory.
			for (int i = 0; i < ControlDefaults.SyntaxTokenCacheSize + 500; i++)
			{
				var (tokens, _) = hl.Tokenize($"var x{i} = {i}", 0, SyntaxLineState.Initial);
				Assert.NotEmpty(tokens);
			}
		}
	}
}
