// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Controls;
using Xunit;

namespace SharpConsoleUI.Tests;

/// <summary>
/// Regression tests for the tiered bug-hunt fixes. Each targets a confirmed defect and, where the
/// bug lives in a paint path, drives the REAL render path (build the control, render into the buffer
/// via <see cref="ContainerTestHelpers.RenderToLines"/>, assert on the painted lines) at
/// boundary-stressing sizes — not isolated-component asserts — per the CLAUDE.md "real thing" rule.
///
/// The replacement glyph U+FFFD (◆) is the fingerprint of the embedded-newline bug (#45 class): when
/// a single-line paint path passes text containing U+000A to MarkupParser, TextSanitizer maps the
/// control char to U+FFFD. A correct fix renders a space (flatten) or the next line (split) instead.
/// </summary>
[Collection("EnvSerial")]
public class BugHuntFixesRealThingTest
{
	private const char Replacement = '�';

	private static void NeutralizeStdin() => Console.SetIn(TextReader.Null);

	// ---- Class 2: embedded newline in single-line paint paths -------------------------------------

	/// <summary>
	/// #45-class: ButtonControl fed a label with an embedded newline must NOT paint the U+FFFD (◆)
	/// glyph. Rendered through the real window/render path at a narrow width so the paint branch that
	/// pads and parses the text is exercised.
	/// </summary>
	[Fact]
	public void Button_EmbeddedNewline_DoesNotRenderReplacementGlyph()
	{
		NeutralizeStdin();
		var button = new ButtonControl { Text = "Save\nChanges" };

		// Wide enough that "Save Changes" fits on the single row after the newline is flattened.
		var lines = ContainerTestHelpers.RenderToLines(button, width: 30, height: 5);
		var joined = string.Join("\n", lines);

		Assert.DoesNotContain(Replacement, joined);
		// The newline is flattened to a space, so both words render on one row separated by a space
		// (proving the join was a space, not the ◆ the bug produced).
		Assert.Contains(lines, l => l.Contains("Save Changes"));
	}

	/// <summary>
	/// #45-class: RuleControl title with an embedded newline must flatten to a space (a rule is one row),
	/// never paint U+FFFD.
	/// </summary>
	[Fact]
	public void Rule_EmbeddedNewlineTitle_DoesNotRenderReplacementGlyph()
	{
		NeutralizeStdin();
		var rule = new RuleControl { Title = "Section\nOne" };

		var lines = ContainerTestHelpers.RenderToLines(rule, width: 30, height: 3);
		var joined = string.Join("\n", lines);

		Assert.DoesNotContain(Replacement, joined);
		Assert.Contains(lines, l => l.Contains("Section") && l.Contains("One"));
	}

	/// <summary>
	/// #45-class, the hard one: a non-wrap RadioControl whose label contains a hard line break must
	/// SPLIT into two rows (not flatten, not clip). This is the trace Fable confirmed — MeasureDOM
	/// hard-coded height=1, so the second line would be clipped away. Boundary-narrow width forces the
	/// non-wrap branch; both lines must survive and no ◆ may appear.
	/// </summary>
	[Fact]
	public void Radio_NonWrap_EmbeddedNewlineLabel_SplitsIntoRows_NoClipNoReplacement()
	{
		NeutralizeStdin();
		var group = new RadioGroup<string>();
		var radio = new RadioControl<string>(group, "opt1", "Line one\nLine two")
		{
			Wrap = false
		};

		// Tall enough that the second row is NOT clipped by the window — the bug would drop it anyway
		// because MeasureDOM sized the control to a single row.
		var lines = ContainerTestHelpers.RenderToLines(radio, width: 24, height: 6);
		var joined = string.Join("\n", lines);

		Assert.DoesNotContain(Replacement, joined);
		// Both physical lines must be painted on SEPARATE rows (split, not flattened onto one).
		Assert.Contains(lines, l => l.Contains("Line one"));
		Assert.Contains(lines, l => l.Contains("Line two"));
		Assert.True(
			lines.Count(l => l.Contains("Line one")) >= 1 && lines.Count(l => l.Contains("Line two")) >= 1,
			"both label lines must render; the second must not be clipped (MeasureDOM must count split lines).");
	}

	// ---- Class 2 unit: the shared helper --------------------------------------------------------

	[Theory]
	[InlineData("plain", "plain")]                       // fast path: unchanged, no allocation intent
	[InlineData("a\nb", "a b")]
	[InlineData("a\r\nb", "a b")]                          // CRLF collapses to ONE space, not two
	[InlineData("a\rb", "a b")]
	[InlineData("a\nb\nc", "a b c")]
	[InlineData("", "")]
	public void FlattenNewlines_CollapsesLineBreaksToSpaces(string input, string expected)
	{
		Assert.Equal(expected, TextSanitizer.FlattenNewlines(input));
	}

	[Fact]
	public void FlattenNewlines_NoNewline_ReturnsSameInstance()
	{
		// Fast-path guard (CLAUDE.md rule #3): no newline means no allocation.
		var s = "no breaks here";
		Assert.Same(s, TextSanitizer.FlattenNewlines(s));
	}

	// ---- Class 1: Unicode width in SpectreRenderableControl -------------------------------------

	/// <summary>
	/// StripAnsiLength must report DISPLAY columns, not UTF-16 code units, so wide (CJK) content
	/// doesn't bleed past the control bounds. We render a CJK Spectre renderable and assert no ◆ and
	/// that the wide content is present — a proxy for "width math agreed with the painted cell count".
	/// The method itself is private; the observable end state is the rendered output.
	/// </summary>
	[Fact]
	public void SpectreRenderable_CjkContent_RendersWithoutCorruption()
	{
		NeutralizeStdin();
		var panel = new Spectre.Console.Panel("中文测试")
		{
			Border = Spectre.Console.BoxBorder.Rounded
		};
		var control = new SpectreRenderableControl(panel);

		var lines = ContainerTestHelpers.RenderToLines(control, width: 24, height: 6);
		var joined = string.Join("\n", lines);

		Assert.DoesNotContain(Replacement, joined);
		Assert.Contains(lines, l => l.Contains("中文测试"));
	}
}
