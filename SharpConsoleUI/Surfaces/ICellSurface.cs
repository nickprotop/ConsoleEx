// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Surfaces;

/// <summary>
/// A readable rectangle of cells, whatever produced it.
/// </summary>
/// <remarks>
/// <para>
/// The library holds cells in several shapes - a live <see cref="Layout.CharacterBuffer"/>, a
/// <see cref="Layout.BufferSnapshot"/>, and the diagnostics snapshots - and they do not agree
/// on how colour is stored. This is the one read lens over all of them, so an exporter is
/// written once rather than once per source.
/// </para>
/// <para>
/// Read-only and cheap to wrap: an implementation is expected to be a view over existing
/// storage, not a copy of it.
/// </para>
/// </remarks>
public interface ICellSurface
{
	/// <summary>Columns in the surface.</summary>
	int Width { get; }

	/// <summary>Rows in the surface.</summary>
	int Height { get; }

	/// <summary>Gets the cell at a surface-relative position.</summary>
	/// <param name="x">Column, from 0.</param>
	/// <param name="y">Row, from 0.</param>
	/// <returns>The cell.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The position is outside the surface.</exception>
	SurfaceCell this[int x, int y] { get; }
}

/// <summary>
/// One cell of a surface, with colour already resolved.
/// </summary>
/// <param name="Character">The base rune. A blank cell carries a space.</param>
/// <param name="Foreground">The glyph colour.</param>
/// <param name="Background">The cell background.</param>
/// <param name="Decorations">Bold, underline, and the rest.</param>
/// <param name="IsWideContinuation">
/// True for the second column of a wide glyph. The glyph belongs to the preceding cell and
/// must not be drawn again here.
/// </param>
/// <param name="Combiners">
/// Combining marks and ZWJ cluster continuations attached to <paramref name="Character"/>,
/// or null. Dropping this truncates a grapheme to its base rune.
/// </param>
/// <param name="BackgroundWasDefaulted">
/// True when <paramref name="Background"/> was substituted rather than measured, because the
/// source recorded "the terminal's default" - a colour this process does not know. Only an
/// ANSI-backed source can set this.
/// </param>
/// <remarks>
/// <see cref="BackgroundWasDefaulted"/> exists so a substituted value is distinguishable from
/// a measured one. A consumer that cares can check it; one that does not still gets a sensible
/// colour.
/// </remarks>
public readonly record struct SurfaceCell(
	Rune Character,
	Color Foreground,
	Color Background,
	TextDecoration Decorations,
	bool IsWideContinuation,
	string? Combiners,
	bool BackgroundWasDefaulted);
