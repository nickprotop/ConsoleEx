// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Convenience extensions for <see cref="SpinnerStyle"/>.
/// </summary>
public static class SpinnerStyleExtensions
{
	/// <summary>
	/// The reserved column width for the style — the widest frame's display width, measured with
	/// markup stripped and Unicode (East Asian / ambiguous) width applied so the value matches what
	/// the spinner actually occupies on screen. Lets you reserve a stable footprint (e.g. a
	/// status-bar label) up front, without constructing a spinner or animator first.
	/// </summary>
	/// <remarks>Thin sugar over <see cref="MarkupSpinnerClock.ReservedWidth(SpinnerStyle)"/> — the
	/// single source of truth shared by the inline <c>[spinner]</c> markup, <see cref="SpinnerControl"/>,
	/// and <see cref="Helpers.SpinnerTextAnimator"/>.</remarks>
	public static int FrameWidth(this SpinnerStyle style) => MarkupSpinnerClock.ReservedWidth(style);

	/// <summary>
	/// The reserved column width honoring an explicit minimum <paramref name="requestedWidth"/>: a
	/// value narrower than the style's natural width is clamped up so the glyph never clips, and a
	/// non-positive value uses the natural width. Mirrors the inline <c>[spinner … width:N]</c> semantics.
	/// </summary>
	public static int FrameWidth(this SpinnerStyle style, int requestedWidth)
		=> MarkupSpinnerClock.ReservedWidth(style, requestedWidth);
}
