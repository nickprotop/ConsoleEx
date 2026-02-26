// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Provides rendering context for a single row of a gutter renderer.
	/// Passed by-ref to avoid heap allocation on every row/renderer combination.
	/// </summary>
	public readonly struct GutterRenderContext
	{
		/// <summary>The character buffer to paint into.</summary>
		public CharacterBuffer Buffer { get; init; }

		/// <summary>The X coordinate where this renderer's columns start.</summary>
		public int X { get; init; }

		/// <summary>The Y coordinate of the current row.</summary>
		public int Y { get; init; }

		/// <summary>
		/// The zero-based source line index for this row, or -1 if the row is beyond content
		/// (e.g. empty rows at the bottom of the viewport).
		/// </summary>
		public int SourceLineIndex { get; init; }

		/// <summary>
		/// Whether this row corresponds to the first wrapped segment of the source line.
		/// False for continuation rows produced by word/character wrapping.
		/// </summary>
		public bool IsFirstWrappedSegment { get; init; }

		/// <summary>Whether this row is on the line that contains the cursor.</summary>
		public bool IsCursorLine { get; init; }

		/// <summary>Whether the control currently has keyboard focus.</summary>
		public bool HasFocus { get; init; }

		/// <summary>The control's current foreground color.</summary>
		public Color ForegroundColor { get; init; }

		/// <summary>The control's current background color.</summary>
		public Color BackgroundColor { get; init; }

		/// <summary>The total number of source lines in the document.</summary>
		public int TotalLineCount { get; init; }
	}

	/// <summary>
	/// Defines a pluggable gutter renderer for <see cref="MultilineEditControl"/>.
	/// Multiple renderers are stacked left-to-right in the gutter area.
	/// </summary>
	public interface IGutterRenderer
	{
		/// <summary>
		/// Returns the width in columns that this renderer needs for the given document.
		/// Called once per paint pass; the sum of all renderer widths determines the total gutter width.
		/// </summary>
		/// <param name="totalLineCount">The total number of source lines in the document.</param>
		int GetWidth(int totalLineCount);

		/// <summary>
		/// Renders a single row of the gutter.
		/// </summary>
		/// <param name="context">The rendering context for this row.</param>
		/// <param name="width">The column width allocated to this renderer (from <see cref="GetWidth"/>).</param>
		void Render(in GutterRenderContext context, int width);
	}
}
