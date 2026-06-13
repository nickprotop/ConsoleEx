// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using BenchmarkDotNet.Attributes;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Benchmarks;

[MemoryDiagnoser]
public class UnicodeWidthBenchmarks
{
	public static IEnumerable<string> Inputs => new[]
	{
		"the quick brown fox jumps over the lazy dog 0123456789",   // ascii
		"你好世界这是一个中文字符串测试宽度计算的性能表现如何呢",       // cjk (wide)
		"📦🚀🎉👍🔥💡⭐✅❌⚙️🧰📝🔧🎯",                              // emoji + vs16
		"áéíóú combining acute marks",                               // combining marks
	};

	[ParamsSource(nameof(Inputs))]
	public string Text = "";

	[Benchmark]
	public int GetStringWidth() => UnicodeWidth.GetStringWidth(Text);
}
