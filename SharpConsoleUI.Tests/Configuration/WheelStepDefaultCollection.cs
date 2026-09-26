// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Xunit;

namespace SharpConsoleUI.Tests.Configuration;

/// <summary>
/// Tests that mutate the process-wide <c>ControlDefaults.DefaultScrollWheelLines</c> must share
/// this collection so xUnit never runs them in parallel with one another.
/// </summary>
/// <remarks>
/// This collection stops the mutating tests from colliding with EACH OTHER. It does not isolate
/// them from the rest of the suite: xUnit runs separate collections in parallel, and this assembly
/// sets no <c>CollectionBehavior</c> override.
/// <para>
/// So also join this collection if your test asserts a scroll amount that comes from the default —
/// <c>ListControl</c>, <c>HtmlControl</c> and <c>ListBuilder</c> read
/// <c>DefaultScrollWheelLines</c> in a FIELD INITIALISER, so a control constructed while another
/// test holds a non-default value silently gets that value. No test does both today; this note is
/// here so the next one does not become an intermittent failure.
/// </para>
/// Every test in here must restore the previous value in a <c>finally</c>.
/// <para>
/// <c>ControlDefaults.DefaultWindowScrollWheelLines</c> is a special case: reassigning a captured
/// "original" value does not restore an unpinned (following) state, because any concrete value
/// pins it. Tests touching it must unpin via the internal
/// <c>ResetDefaultWindowScrollWheelLinesForTests()</c> instead of restoring a captured value — see
/// <c>WindowWheelStepTests</c>.
/// </para>
/// </remarks>
[CollectionDefinition("WheelStepDefault", DisableParallelization = true)]
public class WheelStepDefaultCollection { }
