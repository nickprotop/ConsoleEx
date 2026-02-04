// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics;

/// <summary>
/// Captures per-frame performance and quality metrics for the rendering pipeline.
/// Used to measure efficiency, detect performance issues, and validate optimizations.
/// </summary>
public class RenderingMetrics
{
	/// <summary>
	/// Gets or sets the frame number this metrics instance represents.
	/// </summary>
	public int FrameNumber { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when this frame was rendered.
	/// </summary>
	public DateTime Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the total number of bytes written to console output.
	/// For static content, this should be zero after the first frame.
	/// </summary>
	public int BytesWritten { get; set; }

	/// <summary>
	/// Gets or sets the number of characters that changed in this frame.
	/// </summary>
	public int CharactersChanged { get; set; }

	/// <summary>
	/// Gets or sets the total number of ANSI escape sequences in the output.
	/// </summary>
	public int AnsiEscapeSequences { get; set; }

	/// <summary>
	/// Gets or sets the number of cursor movement commands.
	/// </summary>
	public int CursorMovements { get; set; }

	/// <summary>
	/// Gets or sets the number of cells marked as dirty in the CharacterBuffer.
	/// </summary>
	public int DirtyCellsMarked { get; set; }

	/// <summary>
	/// Gets or sets the number of cells actually rendered to the console.
	/// </summary>
	public int CellsActuallyRendered { get; set; }

	/// <summary>
	/// Gets the efficiency ratio (actual rendered / dirty marked).
	/// A value close to 1.0 indicates efficient dirty tracking.
	/// </summary>
	public double EfficiencyRatio =>
		DirtyCellsMarked > 0 ? (double)CellsActuallyRendered / DirtyCellsMarked : 1.0;

	/// <summary>
	/// Gets or sets the number of redundant ANSI sequences detected (e.g., setting the same color twice).
	/// </summary>
	public int RedundantAnsiSequences { get; set; }

	/// <summary>
	/// Gets or sets the number of unnecessary reset sequences.
	/// </summary>
	public int UnnecessaryResets { get; set; }

	/// <summary>
	/// Gets or sets the number of color changes in this frame.
	/// </summary>
	public int ColorChanges { get; set; }

	/// <summary>
	/// Gets or sets the ANSI optimization ratio (1.0 = fully optimized, 0.0 = no optimization).
	/// </summary>
	public double AnsiOptimizationRatio { get; set; } = 1.0;

	/// <summary>
	/// Gets or sets the number of cells that differ from the previous frame.
	/// </summary>
	public int CellsDifferentFromPreviousFrame { get; set; }

	/// <summary>
	/// Gets whether this frame had no changes from the previous frame.
	/// Static frames should produce zero console output.
	/// </summary>
	public bool IsStaticFrame => CellsDifferentFromPreviousFrame == 0;

	/// <summary>
	/// Gets or sets the time spent in the DOM layout phase (milliseconds).
	/// </summary>
	public double DomLayoutTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the time spent generating ANSI sequences (milliseconds).
	/// </summary>
	public double AnsiGenerationTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the time spent in buffer comparison (milliseconds).
	/// </summary>
	public double BufferComparisonTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the time spent writing to console (milliseconds).
	/// </summary>
	public double ConsoleOutputTimeMs { get; set; }

	/// <summary>
	/// Gets the total frame time across all phases.
	/// </summary>
	public double TotalFrameTimeMs =>
		DomLayoutTimeMs + AnsiGenerationTimeMs + BufferComparisonTimeMs + ConsoleOutputTimeMs;

	/// <summary>
	/// Returns a summary string of the metrics.
	/// </summary>
	public override string ToString()
	{
		return $"Frame {FrameNumber}: {BytesWritten} bytes, {CharactersChanged} chars changed, " +
		       $"Efficiency: {EfficiencyRatio:P1}, Static: {IsStaticFrame}, Time: {TotalFrameTimeMs:F2}ms";
	}
}
