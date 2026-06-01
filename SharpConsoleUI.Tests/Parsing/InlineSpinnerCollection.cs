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
/// Serializes tests that mutate the static MarkupSpinnerClock time-seam so they
/// do not run in parallel and interfere with one another.
/// </summary>
[CollectionDefinition("InlineSpinner")]
public class InlineSpinnerCollection { }
