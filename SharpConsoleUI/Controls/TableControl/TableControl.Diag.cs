// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// TEMPORARY diagnostics for the "table width one column less on hover vs full on selected" report.
// Gated by the SCUI_TABLE_DIAG environment variable (set it to a file path). Safe no-op otherwise.
// Remove this file once the bug is diagnosed.

namespace SharpConsoleUI.Controls;

public partial class TableControl
{
	private static readonly string? _diagPath = Environment.GetEnvironmentVariable("SCUI_TABLE_DIAG");

	private void Diag(string phase, int boundsWidth, int[]? colWidths)
	{
		if (string.IsNullOrEmpty(_diagPath)) return;
		try
		{
			string cols = colWidths == null ? "-" : string.Join(",", colWidths);
			string line =
				$"{phase} name={Name} boundsW={boundsWidth} actualW={ActualWidth} actualH={ActualHeight} " +
				$"rowCount={RowCount} visRows={GetVisibleRowCount()} vScroll={ShouldShowVerticalScrollbar()} " +
				$"gutter={ScrollbarGutterWidth} sel={_selectedRowIndex} hover={_hoveredRowIndex} cols=[{cols}]" +
				Environment.NewLine;
			File.AppendAllText(_diagPath, line);
		}
		catch
		{
			// Diagnostics must never affect rendering.
		}
	}
}
