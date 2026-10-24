using Xunit;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Collection definition for tests that render a window, where the result depends on
/// <see cref="SharpConsoleUI.Windows.WindowRenderer.RootLayoutFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// That factory is static and mutable: it is the application-wide default root layout, and a
/// test that swaps it in a try/finally still has it installed while OTHER test classes run.
/// xUnit parallelises across classes, so a concurrently-running render picks up the foreign
/// root and lays out against a model it was not written for.
/// </para>
/// <para>
/// This surfaced as <c>GoldenBufferOracleTests.TheOracle_CatchesAScrolledBody</c> failing on CI
/// while passing locally: under the alternate root the window was not scrollable,
/// <c>Window.ScrollBy</c> silently no-opped, and the buffers compared equal - which the test
/// read as "the oracle missed a scroll".
/// </para>
/// <para>
/// Tests in this collection run sequentially. Any test that either MUTATES the factory or
/// RENDERS a window whose layout it cares about belongs here.
/// </para>
/// </remarks>
[CollectionDefinition("RootLayout", DisableParallelization = true)]
public class RootLayoutCollection { }
