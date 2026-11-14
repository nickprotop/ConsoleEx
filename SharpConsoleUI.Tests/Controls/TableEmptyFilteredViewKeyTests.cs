// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A confirmed filter that matches no rows used to be a trap, found while building cxlog.
/// <para>
/// <c>ProcessKey</c> guarded the whole key switch behind <c>if (rowCount == 0) return false;</c>,
/// so on an empty filtered view Escape never reached the branch that clears a confirmed filter and
/// '/' never reached the branch that starts a new one. Worse than inert: because ProcessKey
/// returned false, the window took the Escape and moved focus off the table, so the user's way out
/// of the filter also cost them their place.
/// </para>
/// <para>
/// The fix moves the keys that act on the TABLE rather than on a row ahead of the row-count guard.
/// Navigation, selection and editing still require rows, which the last test here pins.
/// </para>
/// </summary>
public class TableEmptyFilteredViewKeyTests
{
	#region Harness

	/// <summary>
	/// The real nesting on a HeadlessConsoleDriver, focused, so keys travel the actual input path:
	/// InputStateService to InputCoordinator to the window dispatcher to the control.
	/// </summary>
	private static (TableControl table, ConsoleWindowSystem system, Window window) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16, Title = "Logs" };

		var table = new TableControl
		{
			FilteringEnabled = true,
			ReadOnly = false,
			ShowHeader = true,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		table.AddColumn("Level");
		table.AddColumn("Message");

		// Deliberately no ERROR row: the filter below is the one that matches nothing.
		for (int i = 0; i < 12; i++)
			table.AddRow(i % 2 == 0 ? "INFO" : "WARN", $"message {i:00}");

		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();
		window.FocusControl(table);
		system.Render.UpdateDisplay();

		return (table, system, window);
	}

	private static void Send(ConsoleWindowSystem system, ConsoleKeyInfo key)
	{
		system.InputStateService.EnqueueKey(key);
		system.Input.ProcessInput();
		system.Render.UpdateDisplay();
	}

	private static ConsoleKeyInfo Esc() => new('\u001b', ConsoleKey.Escape, false, false, false);

	/// <summary>'/' as a terminal actually delivers it: a character, with no meaningful ConsoleKey.</summary>
	private static ConsoleKeyInfo Slash() => new('/', ConsoleKey.Oem2, false, false, false);

	/// <summary>Applies a filter that matches nothing and asserts the premise of every test here.</summary>
	private static void ApplyEmptyMatchingFilter(TableControl table, ConsoleWindowSystem system)
	{
		table.ApplyFilter("level:ERROR");
		system.Render.UpdateDisplay();

		Assert.Equal(FilterMode.Confirmed, table.CurrentFilterMode);
		Assert.Equal(0, table.RowCount);
	}

	#endregion

	#region Escape on an empty filtered view

	/// <summary>
	/// THE REPORTED BUG: Escape must clear a confirmed filter even when it matched no rows, which is
	/// precisely when the user most needs it to.
	/// </summary>
	[Fact]
	public void Escape_OnEmptyFilteredView_ClearsTheFilter()
	{
		var (table, system, window) = Build();
		ApplyEmptyMatchingFilter(table, system);

		int cleared = 0;
		table.FilterCleared += (_, _) => cleared++;

		Send(system, Esc());

		Assert.Equal(FilterMode.None, table.CurrentFilterMode);
		Assert.Equal(1, cleared);
		Assert.Equal(12, table.RowCount);

		// And it survives the re-render, rather than being a transient the next frame undoes.
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		Assert.Equal(FilterMode.None, table.CurrentFilterMode);
		Assert.Equal(12, table.RowCount);
	}

	/// <summary>
	/// The consequence that made the bug worse than an inert key: ProcessKey returning false let the
	/// window use the Escape to unfocus the table. Clearing the filter must keep the focus.
	/// </summary>
	[Fact]
	public void Escape_OnEmptyFilteredView_KeepsFocusOnTheTable()
	{
		var (table, system, _) = Build();
		ApplyEmptyMatchingFilter(table, system);
		Assert.True(table.HasFocus);

		Send(system, Esc());

		Assert.True(table.HasFocus);
	}

	#endregion

	#region '/' on an empty filtered view

	/// <summary>
	/// THE REPORTED BUG: '/' must start filter typing on an empty view, so a filter that matched
	/// nothing can be retyped without first having to clear it.
	/// </summary>
	[Fact]
	public void Slash_OnEmptyFilteredView_StartsFilterTyping()
	{
		var (table, system, _) = Build();
		ApplyEmptyMatchingFilter(table, system);

		Send(system, Slash());

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	/// <summary>
	/// A table with no rows at all, filter or no filter, still opens the filter editor on '/'. This
	/// is the same code path but a different premise: there is nothing to un-filter here.
	/// </summary>
	[Fact]
	public void Slash_OnATableWithNoRowsAtAll_StartsFilterTyping()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 16, Title = "Empty" };
		var table = new TableControl
		{
			FilteringEnabled = true,
			ReadOnly = false,
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
		};
		table.AddColumn("Level");
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		system.Render.UpdateDisplay();
		window.FocusControl(table);
		system.Render.UpdateDisplay();

		Assert.Equal(0, table.RowCount);
		Assert.Equal(FilterMode.None, table.CurrentFilterMode);

		Send(system, Slash());

		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);
	}

	#endregion

	#region Guards against over-fixing

	/// <summary>
	/// THE REGRESSION GUARD: navigation keys must still require rows. Pulling Escape and '/' ahead
	/// of the row-count guard must not drag the row keys with them — a table that claims Up, Down,
	/// PageUp, PageDown, Home and End on an empty view would swallow keys its container needs.
	/// Asserted on ProcessKey directly, because the point is the return value the window reads.
	/// </summary>
	[Fact]
	public void NavigationKeys_OnEmptyFilteredView_StillReturnFalse()
	{
		var (table, system, _) = Build();
		ApplyEmptyMatchingFilter(table, system);

		foreach (var key in new[]
		{
			ConsoleKey.DownArrow, ConsoleKey.UpArrow, ConsoleKey.PageDown, ConsoleKey.PageUp,
			ConsoleKey.Home, ConsoleKey.End, ConsoleKey.Enter,
		})
		{
			Assert.False(
				table.ProcessKey(new ConsoleKeyInfo('\0', key, false, false, false)),
				$"{key} must not be claimed by a table with no rows");
		}

		// The filter is untouched by all of that: none of those keys did anything.
		Assert.Equal(FilterMode.Confirmed, table.CurrentFilterMode);
	}

	/// <summary>
	/// Escape on a POPULATED table with no filter still falls through, so a window that uses Escape
	/// to close a dialog or unfocus keeps working. The empty-view fix must not make Escape greedy.
	/// </summary>
	[Fact]
	public void Escape_OnPopulatedUnfilteredTable_StillReturnsFalse()
	{
		var (table, system, _) = Build();
		Assert.Equal(FilterMode.None, table.CurrentFilterMode);
		Assert.Equal(12, table.RowCount);

		Assert.False(table.ProcessKey(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false)));
	}

	/// <summary>
	/// And the behaviour this all exists to preserve: on a populated view the filter keys work
	/// exactly as before, through the real input path.
	/// </summary>
	[Fact]
	public void FilterKeys_OnPopulatedView_BehaveAsBefore()
	{
		var (table, system, _) = Build();

		Send(system, Slash());
		Assert.Equal(FilterMode.Typing, table.CurrentFilterMode);

		Send(system, Esc());
		Assert.Equal(FilterMode.None, table.CurrentFilterMode);

		// A filter that DOES match, then Escape clears it.
		table.ApplyFilter("level:WARN");
		system.Render.UpdateDisplay();
		Assert.Equal(FilterMode.Confirmed, table.CurrentFilterMode);
		Assert.Equal(6, table.RowCount);

		Send(system, Esc());
		Assert.Equal(FilterMode.None, table.CurrentFilterMode);
		Assert.Equal(12, table.RowCount);
	}

	#endregion
}
