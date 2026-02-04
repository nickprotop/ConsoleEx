// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics.Analysis;

/// <summary>
/// Represents a quality analysis report for a specific frame.
/// Contains detected issues, optimization scores, and recommendations.
/// </summary>
public class QualityReport
{
	/// <summary>
	/// Gets or sets the frame number this report analyzes.
	/// </summary>
	public int FrameNumber { get; set; }

	/// <summary>
	/// Gets or sets the number of redundant ANSI sequences detected.
	/// Redundant sequences set the same color/attribute twice in a row.
	/// </summary>
	public int RedundantAnsiCount { get; set; }

	/// <summary>
	/// Gets or sets the number of inefficient cursor moves detected.
	/// Inefficient moves could be optimized by combining adjacent writes.
	/// </summary>
	public int InefficientCursorMoves { get; set; }

	/// <summary>
	/// Gets or sets the over-invalidation ratio (0.0-1.0).
	/// Higher values indicate more cells marked dirty than actually changed.
	/// </summary>
	public double OverInvalidationRatio { get; set; }

	/// <summary>
	/// Gets or sets the overall optimization score (0.0-1.0).
	/// 1.0 = fully optimized, 0.0 = no optimization.
	/// </summary>
	public double OptimizationScore { get; set; }

	/// <summary>
	/// Gets the list of detected quality issues.
	/// </summary>
	public List<QualityIssue> Issues { get; set; } = new();

	/// <summary>
	/// Gets whether this frame has any quality issues.
	/// </summary>
	public bool HasIssues => Issues.Count > 0 || RedundantAnsiCount > 0 || InefficientCursorMoves > 0;

	/// <summary>
	/// Gets a summary string of the quality report.
	/// </summary>
	public string GetSummary()
	{
		if (!HasIssues)
			return $"Frame {FrameNumber}: No quality issues detected. Optimization score: {OptimizationScore:P1}";

		return $"Frame {FrameNumber}: {Issues.Count} issues, " +
		       $"Redundant ANSI: {RedundantAnsiCount}, " +
		       $"Inefficient cursor moves: {InefficientCursorMoves}, " +
		       $"Over-invalidation: {OverInvalidationRatio:P1}, " +
		       $"Score: {OptimizationScore:P1}";
	}

	/// <summary>
	/// Returns a detailed report with all issues.
	/// </summary>
	public string GetDetailedReport()
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine($"Quality Report - Frame {FrameNumber}");
		sb.AppendLine($"Optimization Score: {OptimizationScore:P1}");
		sb.AppendLine();

		if (RedundantAnsiCount > 0)
			sb.AppendLine($"⚠ Redundant ANSI sequences: {RedundantAnsiCount}");

		if (InefficientCursorMoves > 0)
			sb.AppendLine($"⚠ Inefficient cursor moves: {InefficientCursorMoves}");

		if (OverInvalidationRatio > 0.2)
			sb.AppendLine($"⚠ Over-invalidation: {OverInvalidationRatio:P1}");

		if (Issues.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("Issues:");
			foreach (var issue in Issues)
			{
				sb.AppendLine($"  [{issue.Category}] {issue.Description}");
				if (!string.IsNullOrEmpty(issue.Location))
					sb.AppendLine($"    Location: {issue.Location}");
			}
		}

		if (!HasIssues)
		{
			sb.AppendLine("✓ No quality issues detected");
		}

		return sb.ToString();
	}
}

/// <summary>
/// Represents a specific quality issue detected during rendering.
/// </summary>
/// <param name="Category">The category of the issue (e.g., "ANSI Optimization", "Dirty Tracking").</param>
/// <param name="Description">A description of the issue.</param>
/// <param name="Location">Optional location information (e.g., "Line 5, Column 10").</param>
public record QualityIssue(string Category, string Description, string Location);
