// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Diagnostics.Snapshots;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.Diagnostics.Analysis;

/// <summary>
/// Analyzes rendering output for quality issues and optimization opportunities.
/// Detects redundant ANSI sequences, inefficient cursor moves, and over-invalidation.
/// </summary>
public class QualityAnalyzer
{
	/// <summary>
	/// Analyzes snapshots and metrics to produce a quality report.
	/// </summary>
	public QualityReport Analyze(
		AnsiLinesSnapshot? ansi,
		ConsoleBufferSnapshot? console,
		RenderingMetrics metrics)
	{
		var report = new QualityReport
		{
			FrameNumber = metrics.FrameNumber
		};

		// Analyze ANSI optimization
		if (ansi != null)
		{
			report.RedundantAnsiCount = CountRedundantAnsi(ansi);
			report.InefficientCursorMoves = CountInefficientCursorMoves(ansi);
		}

		// Analyze dirty tracking efficiency
		if (console != null)
		{
			report.OverInvalidationRatio = CalculateOverInvalidation(console, metrics);
		}

		// Calculate overall optimization score
		report.OptimizationScore = CalculateOptimizationScore(metrics, report);

		// Detect specific quality issues
		report.Issues = DetectQualityIssues(ansi, console, metrics, report);

		return report;
	}

	/// <summary>
	/// Counts redundant ANSI sequences (same color/attribute set twice in a row).
	/// </summary>
	private int CountRedundantAnsi(AnsiLinesSnapshot ansi)
	{
		int redundantCount = 0;

		// Pattern: same ANSI code appearing consecutively
		var colorRegex = new Regex(@"\x1b\[([0-9;]+)m");

		foreach (var line in ansi.Lines)
		{
			var matches = colorRegex.Matches(line);
			string? lastCode = null;

			foreach (Match match in matches)
			{
				var code = match.Groups[1].Value;
				if (code == lastCode)
				{
					redundantCount++;
				}
				lastCode = code;
			}
		}

		return redundantCount;
	}

	/// <summary>
	/// Counts inefficient cursor moves (moves to adjacent cells that could be combined).
	/// </summary>
	private int CountInefficientCursorMoves(AnsiLinesSnapshot ansi)
	{
		int inefficientCount = 0;

		// Pattern: cursor positioning ESC[y;xH
		var cursorRegex = new Regex(@"\x1b\[(\d+);(\d+)H");

		foreach (var line in ansi.Lines)
		{
			var matches = cursorRegex.Matches(line);
			(int y, int x)? lastPos = null;

			foreach (Match match in matches)
			{
				int y = int.Parse(match.Groups[1].Value);
				int x = int.Parse(match.Groups[2].Value);

				if (lastPos.HasValue)
				{
					// Check if this is an adjacent cell on the same line
					if (lastPos.Value.y == y && Math.Abs(lastPos.Value.x - x) == 1)
					{
						inefficientCount++;
					}
				}

				lastPos = (y, x);
			}
		}

		return inefficientCount;
	}

	/// <summary>
	/// Calculates the over-invalidation ratio.
	/// High values indicate many cells marked dirty but not actually changed.
	/// </summary>
	private double CalculateOverInvalidation(ConsoleBufferSnapshot console, RenderingMetrics metrics)
	{
		if (metrics.DirtyCellsMarked == 0)
			return 0.0;

		// Count cells that were marked dirty but didn't actually differ
		int actualDifferences = console.GetDifferences().Count;

		if (actualDifferences == 0 && metrics.DirtyCellsMarked == 0)
			return 0.0;

		// Over-invalidation = (dirty marked - actual changes) / dirty marked
		double overInvalidation = (metrics.DirtyCellsMarked - actualDifferences) / (double)metrics.DirtyCellsMarked;
		return Math.Max(0.0, overInvalidation);
	}

	/// <summary>
	/// Calculates an overall optimization score based on multiple factors.
	/// </summary>
	private double CalculateOptimizationScore(RenderingMetrics metrics, QualityReport report)
	{
		double score = 1.0;

		// Penalize redundant ANSI sequences
		if (metrics.AnsiEscapeSequences > 0)
		{
			double redundancyRatio = report.RedundantAnsiCount / (double)metrics.AnsiEscapeSequences;
			score -= redundancyRatio * 0.3; // Up to 30% penalty
		}

		// Penalize inefficient cursor moves
		if (metrics.CursorMovements > 0)
		{
			double inefficiencyRatio = report.InefficientCursorMoves / (double)metrics.CursorMovements;
			score -= inefficiencyRatio * 0.2; // Up to 20% penalty
		}

		// Penalize over-invalidation
		score -= report.OverInvalidationRatio * 0.3; // Up to 30% penalty

		// Penalize static frames that produce output
		if (metrics.IsStaticFrame && metrics.BytesWritten > 0)
		{
			score -= 0.5; // Major penalty for failing static frame optimization
		}

		return Math.Max(0.0, Math.Min(1.0, score));
	}

	/// <summary>
	/// Detects specific quality issues and returns them as a list.
	/// </summary>
	private List<QualityIssue> DetectQualityIssues(
		AnsiLinesSnapshot? ansi,
		ConsoleBufferSnapshot? console,
		RenderingMetrics metrics,
		QualityReport report)
	{
		var issues = new List<QualityIssue>();

		// Issue: Static frame producing output
		if (metrics.IsStaticFrame && metrics.BytesWritten > 0)
		{
			issues.Add(new QualityIssue(
				"Double Buffering",
				$"Static frame produced {metrics.BytesWritten} bytes of output (should be 0)",
				$"Frame {metrics.FrameNumber}"));
		}

		// Issue: High redundant ANSI count
		if (report.RedundantAnsiCount > 10)
		{
			issues.Add(new QualityIssue(
				"ANSI Optimization",
				$"{report.RedundantAnsiCount} redundant ANSI sequences detected",
				"Consider optimizing color change detection"));
		}

		// Issue: High over-invalidation
		if (report.OverInvalidationRatio > 0.5)
		{
			issues.Add(new QualityIssue(
				"Dirty Tracking",
				$"Over-invalidation ratio: {report.OverInvalidationRatio:P1}",
				"Many cells marked dirty but not actually changed"));
		}

		// Issue: Low efficiency ratio
		if (metrics.EfficiencyRatio < 0.5 && metrics.DirtyCellsMarked > 0)
		{
			issues.Add(new QualityIssue(
				"Rendering Efficiency",
				$"Efficiency ratio: {metrics.EfficiencyRatio:P1} (marked {metrics.DirtyCellsMarked} dirty, rendered {metrics.CellsActuallyRendered})",
				"Consider improving dirty cell detection"));
		}

		return issues;
	}
}
