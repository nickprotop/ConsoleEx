// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for an interactive TableControl with VerticalAlignment.Fill
/// hosted inside a content-sized ScrollablePanelControl that is itself inside a
/// Fill TabControl (the real-world LazyCaddy "Upstreams" layout).
///
/// Bug: the table rendered only ~2 rows tall, left empty space below it (Fill was a
/// no-op), and when rows were added it scrolled internally (hiding earlier rows)
/// instead of growing.
/// </summary>
public class TableFillInScrollPanelTests
{
	/// <summary>
	/// Builds: Window -> TabControl(Fill) -> tab -> ScrollablePanel(content-sized) ->
	/// [header label, interactive Fill table]. Mirrors RouteEditModal + ReverseProxyEditor.
	/// </summary>
	private static (TableControl table, ScrollablePanelControl panel, ConsoleWindowSystem system, Window window)
		BuildUpstreamsLayout(int rows)
	{
		var table = TableControl.Create()
			.AddColumn("Dial", TextJustification.Left)
			.Rounded()
			.Interactive()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithVerticalScrollbar(ScrollbarVisibility.Auto)
			.Build();
		for (int i = 1; i <= rows; i++) table.AddRow(new TableRow($"127.0.0.1:900{i}"));
		if (rows > 0) table.SelectedRowIndex = rows - 1;

		// Content-sized panel (no explicit Height) — like Controls.ScrollablePanel().Build().
		// Mirror ReverseProxyEditor.Build()'s fixed siblings above the table.
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel().WithScrollbar(true).Build();
		panel.AddControl(ContainerTestHelpers.CreateLabel("Proxy targets (host:port)."));
		panel.AddControl(new CheckboxControl { Label = "Stream immediately" });
		panel.AddControl(ContainerTestHelpers.CreateLabel("Upstreams"));
		panel.AddControl(ContainerTestHelpers.CreateButton("Add"));
		panel.AddControl(table);

		var tabs = SharpConsoleUI.Builders.Controls.TabControl().Fill().Build();
		tabs.AddTab("Upstreams", panel);

		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(tabs);
		// StickyBottom siblings like the modal's status + Apply/Close toolbar.
		var status = ContainerTestHelpers.CreateLabel("status");
		status.StickyPosition = StickyPosition.Bottom;
		window.AddControl(status);
		var toolbar = ContainerTestHelpers.CreateButton("Apply");
		toolbar.StickyPosition = StickyPosition.Bottom;
		window.AddControl(toolbar);
		window.RenderAndGetVisibleContent();

		return (table, panel, system, window);
	}

	[Fact]
	public void FillTable_FillsAvailableTabHeight_NotJustContent()
	{
		var (table, panel, _, _) = BuildUpstreamsLayout(rows: 2);

		// With 2 rows the bordered content height is ~6 lines. The tab has well over
		// 20 lines available, so a Fill table must take far more than its content size —
		// essentially the whole viewport minus the fixed-height siblings above it.
		const int fixedSiblingsHeight = 6; // label(2)+checkbox(1)+label(2)+button(1)
		Assert.True(
			table.ActualHeight >= panel.ViewportHeight - fixedSiblingsHeight - 1,
			$"Fill table should fill the tab viewport ({panel.ViewportHeight}) minus fixed siblings, " +
			$"but ActualHeight={table.ActualHeight}. Fill is a no-op — it stayed at content size.");
	}

	[Fact]
	public void FillTable_AddingRow_DoesNotScrollAwayFirstRow()
	{
		var (table, panel, _, window) = BuildUpstreamsLayout(rows: 2);

		// Add a third row at runtime, like clicking "Add upstream".
		table.AddRow(new TableRow("127.0.0.1:9003"));
		window.ForceRebuildLayout();
		window.RenderAndGetVisibleContent();

		// The table has room for far more than 3 rows, so all three must be visible:
		// the first row must NOT have scrolled off the top.
		Assert.True(
			table.GetVisibleRowCount() >= 3,
			$"After adding a row the table should show all 3 rows (it has the space), " +
			$"but GetVisibleRowCount={table.GetVisibleRowCount()} ActualHeight={table.ActualHeight} " +
			$"viewport={panel.ViewportHeight} — it scrolled instead of growing/filling.");
	}

	[Fact]
	public void FillTable_RendersBottomBorderAtBottomOfSlot_NotAtContentHeight()
	{
		var (table, _, _, window) = BuildUpstreamsLayout(rows: 2);
		var lines = window.RenderAndGetVisibleContent();
		string text = string.Join("\n", lines);

		// The data must render...
		Assert.Contains("127.0.0.1:9001", text);
		Assert.Contains("127.0.0.1:9002", text);

		// ...and the rounded bottom border must be pushed down to the bottom of the
		// table's slot, several lines BELOW the last data row — not closed right after it.
		int lastDataLine = -1;
		int bottomBorderLine = -1;
		for (int i = 0; i < lines.Count; i++)
		{
			if (lines[i].Contains("127.0.0.1:9002")) lastDataLine = i;
			if (lastDataLine >= 0 && i > lastDataLine && lines[i].Contains("╰")) { bottomBorderLine = i; break; }
		}

		Assert.True(lastDataLine >= 0, "Expected a data row in the rendered output.");
		Assert.True(bottomBorderLine >= 0, "Expected a bottom border below the data.");
		Assert.True(
			bottomBorderLine - lastDataLine > 2,
			$"Bottom border (line {bottomBorderLine}) should be well below the last data row " +
			$"(line {lastDataLine}) because the Fill table expands to fill its slot, " +
			$"but it closed immediately after the data — gap was only {bottomBorderLine - lastDataLine}.");
	}
}
