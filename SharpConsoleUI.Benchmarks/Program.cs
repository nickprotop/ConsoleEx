// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using BenchmarkDotNet.Running;

// Dispatches to any [MemoryDiagnoser]-annotated benchmark class by name/filter.
//   dotnet run -c Release -- --filter '*'
//   dotnet run -c Release -- --filter '*MarkupParsingBenchmarks*' --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

// Marker type so FromAssembly has a stable handle.
public partial class Program { }
