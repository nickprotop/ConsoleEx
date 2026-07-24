// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Unit tests for <see cref="TableControl.AutoScroll"/> — the detach-on-scroll-up /
/// re-attach-at-bottom rule ported from <c>ScrollablePanelControl.AutoScroll</c>.
/// </summary>
public class TableAutoScrollTests
{
	private static TableControl BuildTable(int rows, int height)
	{
		var table = new TableControl { Height = height, ShowHeader = false, BorderStyle = BorderStyle.None };
		table.AddColumn(new TableColumn("Col"));
		for (int i = 0; i < rows; i++)
			table.AddRow(new TableRow(new List<string> { $"R{i}" }));
		return table;
	}

	[Fact]
	public void AutoScroll_DefaultsToFalse_NoBehaviorChangeForExistingUsers()
	{
		var table = BuildTable(rows: 50, height: 10);
		Assert.False(table.AutoScroll);
	}

	[Fact]
	public void AutoScroll_DetachesWhenScrolledUp()
	{
		var table = BuildTable(rows: 50, height: 10);
		int maxOffset = Math.Max(0, table.RowCount - table.GetVisibleRowCount());

		table.ScrollOffset = maxOffset;   // at the bottom
		table.AutoScroll = true;

		table.ScrollOffset = maxOffset - 3;   // user scrolls UP

		Assert.False(table.AutoScroll);
	}

	[Fact]
	public void AutoScroll_ReattachesWhenScrolledBackToBottom()
	{
		var table = BuildTable(rows: 50, height: 10);
		int maxOffset = Math.Max(0, table.RowCount - table.GetVisibleRowCount());

		table.ScrollOffset = maxOffset;
		table.AutoScroll = true;
		table.ScrollOffset = maxOffset - 3;   // detach
		Assert.False(table.AutoScroll);

		table.ScrollOffset = maxOffset;       // back to the bottom

		Assert.True(table.AutoScroll);
	}

	[Fact]
	public void AutoScroll_DownwardMoveShortOfBottom_DoesNotReattach()
	{
		var table = BuildTable(rows: 50, height: 10);
		int maxOffset = Math.Max(0, table.RowCount - table.GetVisibleRowCount());

		table.ScrollOffset = maxOffset;
		table.AutoScroll = true;
		table.ScrollOffset = 0;               // detach (moved up)
		Assert.False(table.AutoScroll);

		table.ScrollOffset = maxOffset - 2;   // down, but NOT to the bottom

		Assert.False(table.AutoScroll);
	}

	[Fact]
	public void AutoScroll_PinningToBottom_DoesNotSelfDetach()
	{
		// The log viewer pins the tail by writing ScrollOffset = maxOffset. That is the RE-ATTACH
		// branch, never the detach branch — the control must not be able to detach itself.
		var table = BuildTable(rows: 50, height: 10);
		int maxOffset = Math.Max(0, table.RowCount - table.GetVisibleRowCount());

		table.ScrollOffset = maxOffset;
		table.AutoScroll = true;

		table.ScrollOffset = maxOffset;   // idempotent pin
		table.ScrollOffset = maxOffset;

		Assert.True(table.AutoScroll);
	}

	[Fact]
	public void AutoScroll_ThumbDragUp_Detaches()
	{
		// Thumb drag used to write _scrollOffset directly, bypassing the setter (and therefore the
		// detach rule). Driving the offset down-then-up through the public property is the same
		// mutation the drag handler now performs.
		var table = BuildTable(rows: 50, height: 10);
		int maxOffset = Math.Max(0, table.RowCount - table.GetVisibleRowCount());

		table.ScrollOffset = maxOffset;
		table.AutoScroll = true;

		table.ScrollOffset = maxOffset / 2;   // drag the thumb upward

		Assert.False(table.AutoScroll);
		Assert.Equal(maxOffset / 2, table.ScrollOffset);
	}
}
