// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

/// <summary>
/// Serializes tests that mutate the static MarkupSpinnerClock time-seam so they do not run in
/// parallel with one another OR with the rest of the suite.
/// </summary>
/// <remarks>
/// <para>
/// <c>DisableParallelization</c> is the load-bearing half and was missing. A collection on its own
/// only stops its OWN tests running concurrently with each other; xUnit still runs the collection
/// in parallel with every other collection. The state these tests reach for is not scoped to them:
/// <c>MarkupSpinnerClock</c> swaps a static time provider and a static host registry, so any of the
/// thousands of tests in other collections can be mid-render while the clock underneath them jumps
/// to a fake value, and these tests can be asserting on a window some other test is still painting.
/// </para>
/// <para>
/// Every other serialized collection in this suite sets the flag; this was the only one of nine
/// without it. The symptom was TickDirtiesWindowHostingInlineSpinner failing its FIRST assert -
/// a window reporting Repaint immediately after a settling UpdateDisplay - rarely, in full-suite
/// runs only, and never in isolation or when the collection was run by itself.
/// </para>
/// </remarks>
[CollectionDefinition("InlineSpinner", DisableParallelization = true)]
public class InlineSpinnerCollection { }
