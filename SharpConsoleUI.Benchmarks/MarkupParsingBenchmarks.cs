// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using BenchmarkDotNet.Attributes;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Benchmarks;

[MemoryDiagnoser]
public class MarkupParsingBenchmarks
{
	// Input shapes covering the realistic spectrum of markup complexity.
	public static IEnumerable<string> Inputs => new[]
	{
		"plain text with no markup at all, just a sentence of words",                       // plain
		"[bold]Hello[/] [blue]World[/] — [green]status ok[/]",                             // moderate
		"[bold][red]A[/][/] [u][yellow]B[/][/] [i][cyan]C[/][/] [bold][green]D[/][/] x5", // heavy-nested
		"📦 [bold]ship[/] 你好 [blue]世界[/] 🚀 café résumé naïve",                        // wide-unicode
	};

	[ParamsSource(nameof(Inputs))]
	public string Markup = "";

	[Benchmark]
	public List<Cell> Parse() => MarkupParser.Parse(Markup, Color.White, Color.Black);

	[Benchmark]
	public int StripLength() => MarkupParser.StripLength(Markup);

	[Benchmark]
	public string Truncate() => MarkupParser.Truncate(Markup, 10);
}
