// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Regression for the HorizontalGridControl (HGC) "Fill table does not fill the column" bug that
/// surfaced when HGC was reimplemented as a single-row <see cref="GridControl"/>.
///
/// The real reproduction (Cratis workbench) is:
///   HGC(VerticalAlignment.Fill)
///     -> column (ColumnContainer)
///        -> leftPane (ScrollablePanelControl, default VerticalAlignment.Top, ScrollMode.None)
///           -> table (TableControl, VerticalAlignment.Fill)
///
/// HGC opts in to <see cref="IGridSource.StarTracksSelfSizeToContentInMeasure"/>, so the grid
/// MEASURES its cells content-loose (StarToAuto) to report a content-based natural size. That left
/// each cell's DesiredSize content-sized; the Top leftPane was then arranged by its own
/// VerticalStackLayout at that content height, so the Fill table could only fill the content rows
/// and left blank space below. The retired HorizontalLayout engine avoided this by delivering the
/// real column extent to the column's content; <see cref="GridLayout.ArrangeChildren"/> now
/// re-measures each cell against its real arranged box (HGC opt-in only) to reproduce that.
///
/// These tests drive the layout tree directly (Measure/Arrange at the layout-method level, no Window
/// and no render loop) against the documented bare-windowed-harness hang. The measure is run with an
/// UNBOUNDED height — the exact regressing path observed live — then the node is arranged at a bounded
/// tall box, mirroring how a Fill HGC is arranged into a bounded panel slot.
/// </summary>
public class HgcFillTableHeightTests
{
	private const int ColumnHeight = 30;

	/// <summary>
	/// Builds the Cratis-shaped chain and returns the HGC plus the deepest table's layout node so the
	/// test can assert the height the table is actually arranged at.
	/// </summary>
	private static (HorizontalGridControl Hgc, LayoutNode Root, TableControl Table) BuildChain()
	{
		var table = new TableControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
		};
		table.AddColumn("State");
		table.AddColumn("Id");
		// A few content rows: the bug is that the table renders ONLY these (content height) and leaves
		// the rest of the column blank instead of filling.
		table.AddRow("Active", "alpha");
		table.AddRow("Active", "beta");
		table.AddRow("Active", "gamma");

		var leftPane = new ScrollablePanelControl
		{
			BorderStyle = BorderStyle.None,
			VerticalScrollMode = ScrollMode.None,
		};
		// leftPane is NOT explicitly Fill: it keeps BaseControl's default Top alignment, exactly as the
		// Cratis FilterableTableView leaves it. This is the load-bearing part of the repro.
		leftPane.AddControl(table);

		var hgc = HorizontalGridControl.Create()
			.Column(c => c.Add(leftPane))
			.Build();
		hgc.VerticalAlignment = VerticalAlignment.Fill;

		var root = LayoutNodeFactory.CreateSubtree(hgc);

		// Locate the table's node by walking to the deepest node whose control is the table.
		LayoutNode? tableNode = FindNode(root, table);
		Assert.NotNull(tableNode);

		return (hgc, root, table);
	}

	private static LayoutNode? FindNode(LayoutNode node, IWindowControl target)
	{
		if (ReferenceEquals(node.Control, target))
		{
			return node;
		}

		foreach (var child in node.Children)
		{
			var found = FindNode(child, target);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	/// <summary>
	/// The core regression: with the column measured content-loose (unbounded height) and then arranged
	/// at a bounded tall box, the Fill table must fill the column height — not stop at its ~3 content rows.
	/// Pre-fix the table node's arranged height was content-sized (a handful of rows); post-fix it is the
	/// full column height.
	/// </summary>
	[Fact]
	public void FillTable_BehindTopScrollPanel_FillsColumnHeight()
	{
		var (_, root, table) = BuildChain();
		var tableNode = FindNode(root, table)!;

		// Regressing path: measure with UNBOUNDED height (what the framework panel hands a Fill HGC at
		// steady state), then arrange into a bounded tall column.
		root.Measure(LayoutConstraints.Loose(60, int.MaxValue));
		root.Arrange(new LayoutRect(0, 0, 60, ColumnHeight));

		// The table is arranged tall enough to fill (allowing for the leftPane's chrome, which is none
		// here). It must be MUCH taller than its ~3-row content height — the bug capped it at content.
		Assert.True(
			tableNode.Bounds.Height >= ColumnHeight - 2,
			$"Fill table should fill the {ColumnHeight}-row column, but was arranged at height {tableNode.Bounds.Height} (regression: capped at content height).");
	}

	/// <summary>
	/// Non-breaking guard: an ORDINARY <see cref="GridControl"/> (no HGC opt-in,
	/// StarTracksSelfSizeToContentInMeasure == false) must NOT be perturbed by the HGC-scoped re-measure.
	/// A Top child keeps its content height; only the HGC opt-in path stretches the cell content.
	/// </summary>
	[Fact]
	public void OrdinaryGrid_TopChild_KeepsContentHeight()
	{
		var label = new MarkupControl(new List<string> { "X" })
		{
			VerticalAlignment = VerticalAlignment.Top,
		};

		var grid = new GridControl();
		grid.ColumnDefinitions.Add(GridLength.Star());
		grid.RowDefinitions.Add(GridLength.Star());
		grid.Place(label, 0, 0);

		Assert.False(((IGridSource)grid).StarTracksSelfSizeToContentInMeasure);

		var root = LayoutNodeFactory.CreateSubtree(grid);
		var labelNode = FindNode(root, label)!;

		root.Measure(LayoutConstraints.Loose(20, int.MaxValue));
		root.Arrange(new LayoutRect(0, 0, 20, ColumnHeight));

		// A Top single-line label stays one row tall in an ordinary grid — the HGC re-measure must not
		// reach this path.
		Assert.Equal(1, labelNode.Bounds.Height);
	}
}
