// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Checkboxes on a <see cref="TreeTableControl"/>: what is checked, what that does to the rows
/// nested under it, and what the queries report.
/// </summary>
public class TreeTableCheckboxTests
{
	#region Helpers

	/// <summary>Feature A > (Story A1 > (Task A1a, Task A1b), Story A2); Feature B > (Story B1).</summary>
	private static TreeTableControl Backlog()
	{
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Title");
		var featureA = table.AddRootRow("Feature A");
		var storyA1 = featureA.AddChild("Story A1");
		storyA1.AddChild("Task A1a");
		storyA1.AddChild("Task A1b");
		featureA.AddChild("Story A2");
		table.AddRootRow("Feature B").AddChild("Story B1");
		return table;
	}

	private static TreeTableRow Row(TreeTableControl table, string title)
	{
		TreeTableRow? Find(IReadOnlyList<TreeTableRow> rows)
		{
			foreach (var row in rows)
			{
				if (row.Cells.Count > 0 && row.Cells[0] == title) return row;
				var hit = Find(row.Children);
				if (hit is not null) return hit;
			}
			return null;
		}

		var roots = table.RootRows.OfType<TreeTableRow>().ToList();
		return Find(roots) ?? throw new InvalidOperationException($"no row '{title}'");
	}

	#endregion

	#region Task 1 - check state lives on the control

	[Fact]
	public void ACheckedRowReportsCheckedOnTheControl()
	{
		var table = Backlog();
		var featureA = Row(table, "Feature A");

		table.SetCheckState(featureA, true);

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(featureA));
	}

	[Fact]
	public void IsCheckedOnATreeRowReadsTheControlsState()
	{
		var table = Backlog();
		var featureA = Row(table, "Feature A");

		table.SetCheckState(featureA, true);

		Assert.True(featureA.IsChecked);
	}

	[Fact]
	public void SettingIsCheckedWritesThroughToTheControl()
	{
		var table = Backlog();
		var featureA = Row(table, "Feature A");

		featureA.IsChecked = true;

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(featureA));
	}

	/// <summary>
	/// A tree can hold a plain TableRow as a childless root, so check state cannot live on
	/// TreeTableRow: a visible checkbox that silently does nothing is the bug this feature exists
	/// to remove.
	/// </summary>
	[Fact]
	public void APlainTableRowRootIsCheckableToo()
	{
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Title");
		var plain = new TableRow("Loose end");
		table.AddRow(plain);

		table.SetCheckState(plain, true);

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(plain));
	}

	#endregion

	#region Task 2 - the map does not outlive its rows

	[Fact]
	public void RemovingACheckedRowDropsItFromTheMap()
	{
		var table = Backlog();
		var storyA2 = Row(table, "Story A2");
		table.SetCheckState(storyA2, true);

		Row(table, "Feature A").RemoveChild(storyA2);

		Assert.Equal(TableCheckState.Unchecked, table.GetCheckState(storyA2));
	}

	[Fact]
	public void RemovingAParentDropsItsWholeSubtree()
	{
		var table = Backlog();
		var storyA1 = Row(table, "Story A1");
		var taskA1a = Row(table, "Task A1a");
		table.SetCheckState(taskA1a, true);

		Row(table, "Feature A").RemoveChild(storyA1);

		Assert.Equal(TableCheckState.Unchecked, table.GetCheckState(taskA1a));
	}

	#endregion

	#region Task 3 - cascade and computed indeterminate

	[Fact]
	public void CheckingAParentChecksItsWholeSubtree()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.True(Row(table, "Task A1a").IsChecked);
		Assert.True(Row(table, "Story A2").IsChecked);
	}

	[Fact]
	public void APartlyCheckedParentIsIndeterminate()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(Row(table, "Story A1")));
		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(Row(table, "Feature A")));
	}

	[Fact]
	public void AParentWithEveryChildCheckedIsChecked()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Task A1a"), true);
		table.SetCheckState(Row(table, "Task A1b"), true);

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(Row(table, "Story A1")));
	}

	/// <summary>Checking a parent that was partly checked settles everything, not nothing.</summary>
	[Fact]
	public void CheckingAPartlyCheckedParentSettlesEverythingChecked()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Task A1a"), true);

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.True(Row(table, "Task A1b").IsChecked);
		Assert.Equal(TableCheckState.Checked, table.GetCheckState(Row(table, "Feature A")));
	}

	[Fact]
	public void UncheckingAParentClearsItsWholeSubtree()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Feature A"), true);

		table.SetCheckState(Row(table, "Feature A"), false);

		Assert.False(Row(table, "Task A1a").IsChecked);
		Assert.Equal(TableCheckState.Unchecked, table.GetCheckState(Row(table, "Feature A")));
	}

	[Fact]
	public void CascadeIsOffByDefault()
	{
		var table = Backlog();

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.False(Row(table, "Task A1a").IsChecked);
	}

	[Fact]
	public void ALeafIsNeverIndeterminate()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(Row(table, "Task A1a")));
	}

	/// <summary>A sibling branch is not disturbed by what happens in another.</summary>
	[Fact]
	public void CascadeStopsAtTheSubtreeItStartedIn()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.Equal(TableCheckState.Unchecked, table.GetCheckState(Row(table, "Feature B")));
		Assert.False(Row(table, "Story B1").IsChecked);
	}

	#endregion

	#region Task 4 - hiding a parent's checkbox

	[Fact]
	public void ParentCheckboxesAreVisibleByDefault()
	{
		var table = Backlog();

		Assert.True(table.IsCheckboxVisible(Row(table, "Feature A")));
	}

	[Fact]
	public void HidingParentCheckboxesLeavesLeavesAlone()
	{
		var table = Backlog();
		table.ShowParentCheckboxes = false;

		Assert.False(table.IsCheckboxVisible(Row(table, "Feature A")));
		Assert.True(table.IsCheckboxVisible(Row(table, "Task A1a")));
	}

	[Fact]
	public void ARowOverridesTheControlsSetting()
	{
		var table = Backlog();
		table.ShowParentCheckboxes = false;
		var featureA = Row(table, "Feature A");

		featureA.ShowCheckbox = true;

		Assert.True(table.IsCheckboxVisible(featureA));
	}

	/// <summary>
	/// Hiding is about the user, not the model: an application restoring a saved selection has
	/// more context than the control does.
	/// </summary>
	[Fact]
	public void AHiddenCheckboxIsStillSettableFromCode()
	{
		var table = Backlog();
		table.ShowParentCheckboxes = false;
		var featureA = Row(table, "Feature A");

		table.SetCheckState(featureA, true);

		Assert.True(featureA.IsChecked);
		Assert.Contains(featureA, table.GetCheckedRows());
	}

	[Fact]
	public void AHiddenParentStillCascadesAndStillComputes()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.ShowParentCheckboxes = false;

		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(Row(table, "Story A1")));
	}

	[Fact]
	public void ARowCanHideItsOwnCheckboxWhileOthersShow()
	{
		var table = Backlog();
		var featureA = Row(table, "Feature A");

		featureA.ShowCheckbox = false;

		Assert.False(table.IsCheckboxVisible(featureA));
		Assert.True(table.IsCheckboxVisible(Row(table, "Feature B")));
	}

	#endregion

	#region Task 5 - selection does not disturb checks

	[Fact]
	public void ClearSelectionLeavesChecksAlone()
	{
		var table = Backlog();
		table.CheckboxMode = true;
		var featureA = Row(table, "Feature A");
		table.SetCheckState(featureA, true);

		table.ClearSelection();

		Assert.True(featureA.IsChecked);
	}

	[Fact]
	public void MovingTheCursorDoesNotCheckAnything()
	{
		var table = Backlog();
		table.CheckboxMode = true;

		table.SelectedRowIndex = 1;

		Assert.Empty(table.GetCheckedRows());
	}

	[Fact]
	public void MultiSelectingDoesNotCheckAnything()
	{
		var table = Backlog();
		table.CheckboxMode = true;

		table.ToggleRowSelection(0);

		Assert.Empty(table.GetCheckedRows());
	}

	/// <summary>The flat table still ties the two together; only the tree separates them.</summary>
	[Fact]
	public void AFlatTableStillTiesCheckedToSelection()
	{
		var table = new TableControl { CheckboxMode = true };
		table.AddColumn("A");
		table.AddRow(new TableRow("one"));

		table.ToggleRowSelection(0);

		Assert.True(table.Rows[0].IsChecked);
	}

	#endregion

	#region Task 6 - what the queries report

	[Fact]
	public void CheckedLeavesExcludesParents()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Story A1"), true);

		var leaves = table.GetCheckedLeaves();

		Assert.Contains(Row(table, "Task A1a"), leaves);
		Assert.DoesNotContain(Row(table, "Story A1"), leaves);
	}

	[Fact]
	public void CheckedRowsStillIncludesParents()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Story A1"), true);

		Assert.Contains(Row(table, "Story A1"), table.GetCheckedRows());
	}

	[Fact]
	public void APlainRootCountsAsALeaf()
	{
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Title");
		table.AddRootRow("Feature A").AddChild("Story A1");
		var plain = new TableRow("Loose end");
		table.AddRow(plain);

		table.SetCheckState(plain, true);

		Assert.Contains(plain, table.GetCheckedLeaves());
	}

	/// <summary>
	/// A check is an instruction, not a view state: collapsing a group to see the rest of the list
	/// does not retract what was asked for.
	/// </summary>
	[Fact]
	public void CollapsingAGroupKeepsItsChecks()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Story A1"), true);

		Row(table, "Feature A").IsExpanded = false;

		Assert.Contains(Row(table, "Task A1a"), table.GetCheckedLeaves());
	}

	[Fact]
	public void AnIndeterminateParentIsNotReportedAsChecked()
	{
		var table = Backlog();
		table.CascadeChecks = true;

		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.DoesNotContain(Row(table, "Story A1"), table.GetCheckedRows());
		Assert.DoesNotContain(Row(table, "Story A1"), table.GetCheckedLeaves());
	}

	#endregion

	#region Task 7 - the glyphs

	[Fact]
	public void TheGlyphsAreAllTheCheckboxColumnWidth()
	{
		Assert.Equal(ControlDefaults.TableCheckboxColumnWidth, ControlDefaults.TableCheckboxChecked.Length);
		Assert.Equal(ControlDefaults.TableCheckboxColumnWidth, ControlDefaults.TableCheckboxUnchecked.Length);
		Assert.Equal(ControlDefaults.TableCheckboxColumnWidth, ControlDefaults.TableCheckboxIndeterminate.Length);
		Assert.Equal(ControlDefaults.TableCheckboxColumnWidth, ControlDefaults.TableCheckboxHidden.Length);
	}

	[Fact]
	public void AnIndeterminateParentPaintsItsOwnGlyph()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.CheckboxMode = true;
		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.Equal(
			ControlDefaults.TableCheckboxIndeterminate,
			table.CheckboxMarkupFor(Row(table, "Story A1")));
	}

	[Fact]
	public void AHiddenCheckboxPaintsBlankAtFullWidth()
	{
		var table = Backlog();
		table.CheckboxMode = true;
		table.ShowParentCheckboxes = false;

		Assert.Equal(
			ControlDefaults.TableCheckboxHidden,
			table.CheckboxMarkupFor(Row(table, "Feature A")));
	}

	[Fact]
	public void ACheckedLeafPaintsTheCheckedGlyph()
	{
		var table = Backlog();
		table.CheckboxMode = true;
		table.SetCheckState(Row(table, "Task A1a"), true);

		Assert.Equal(
			ControlDefaults.TableCheckboxChecked,
			table.CheckboxMarkupFor(Row(table, "Task A1a")));
	}

	#endregion

	#region Task 8 - the real input path

	/// <summary>A tree in checkbox mode, focused in a real window, driven by real key dispatch.</summary>
	private static (ConsoleWindowSystem System, TreeTableControl Table) FocusedBacklog(
		bool cascade = false, bool showParents = true)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var table = Backlog();
		table.CheckboxMode = true;
		table.CascadeChecks = cascade;
		table.ShowParentCheckboxes = showParents;

		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(table, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		return (system, table);
	}

	private static void Press(ConsoleWindowSystem system, ConsoleKey key, char ch = '\0')
	{
		system.InputStateService.EnqueueKey(new ConsoleKeyInfo(ch, key, false, false, false));
		system.Input.ProcessInput();
	}

	[Fact]
	public void SpaceChecksTheFocusedRow()
	{
		var (system, table) = FocusedBacklog();
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(Row(table, "Feature A")));
	}

	/// <summary>
	/// Space means one thing at every depth. Expanding stays on the arrows, which already do it.
	/// </summary>
	[Fact]
	public void SpaceDoesNotExpandInCheckboxMode()
	{
		var (system, table) = FocusedBacklog();
		var featureA = Row(table, "Feature A");
		featureA.IsExpanded = false;
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.False(featureA.IsExpanded);
	}

	[Fact]
	public void RightStillExpandsInCheckboxMode()
	{
		var (system, table) = FocusedBacklog();
		var featureA = Row(table, "Feature A");
		featureA.IsExpanded = false;
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.RightArrow);

		Assert.True(featureA.IsExpanded);
	}

	[Fact]
	public void SpaceOnAHiddenCheckboxRowChecksNothing()
	{
		var (system, table) = FocusedBacklog(showParents: false);
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.Equal(TableCheckState.Unchecked, table.GetCheckState(Row(table, "Feature A")));
	}

	[Fact]
	public void SpaceDoesNotMultiSelect()
	{
		var (system, table) = FocusedBacklog();
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.Empty(table.GetSelectedRows());
	}

	[Fact]
	public void SpaceCascadesWhenCascadeIsOn()
	{
		var (system, table) = FocusedBacklog(cascade: true);
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.True(Row(table, "Task A1a").IsChecked);
	}

	#endregion

	#region Check events

	[Fact]
	public void CheckingARowRaisesCheckChanged()
	{
		var table = Backlog();
		TableRow? reported = null;
		table.CheckChanged += (_, row) => reported = row;

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.Same(Row(table, "Feature A"), reported);
	}

	[Fact]
	public void CheckingTheSameStateTwiceRaisesOnce()
	{
		var table = Backlog();
		int raised = 0;
		table.CheckChanged += (_, _) => raised++;

		table.SetCheckState(Row(table, "Feature A"), true);
		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.Equal(1, raised);
	}

	/// <summary>A cascade reports every row it changed, so a count can be rebuilt from it.</summary>
	[Fact]
	public void ACascadeReportsEachRowItChanged()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		var changed = new List<TableRow>();
		table.CheckChanged += (_, row) => changed.Add(row);

		table.SetCheckState(Row(table, "Story A1"), true);

		Assert.Contains(Row(table, "Task A1a"), changed);
		Assert.Contains(Row(table, "Feature A"), changed);
	}

	#endregion

	#region Edge cases found in review

	/// <summary>
	/// Removing the one unchecked child should settle the parent, not leave it indeterminate
	/// about a row that no longer exists.
	/// </summary>
	[Fact]
	public void RemovingTheOddChildOutSettlesTheParent()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Task A1a"), true);
		var storyA1 = Row(table, "Story A1");
		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(storyA1));

		storyA1.RemoveChild(Row(table, "Task A1b"));

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(storyA1));
	}

	/// <summary>Adding an unchecked child under a checked parent makes it indeterminate.</summary>
	[Fact]
	public void AddingAnUncheckedChildUnsettlesACheckedParent()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		var storyA1 = Row(table, "Story A1");
		table.SetCheckState(storyA1, true);

		storyA1.AddChild("Task A1c");

		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(storyA1));
	}

	/// <summary>A handler that checks another row mid-cascade must not corrupt the result.</summary>
	[Fact]
	public void AHandlerThatChecksAnotherRowDoesNotCorruptTheCascade()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		bool reentered = false;
		table.CheckChanged += (_, _) =>
		{
			if (reentered) return;
			reentered = true;
			table.SetCheckState(Row(table, "Story B1"), true);
		};

		table.SetCheckState(Row(table, "Feature A"), true);

		Assert.True(Row(table, "Task A1a").IsChecked);
		Assert.Equal(TableCheckState.Checked, table.GetCheckState(Row(table, "Feature A")));
	}

	#endregion

	#region More edge cases

	/// <summary>ClearChildren removes several at once; the parent must still settle.</summary>
	[Fact]
	public void ClearingChildrenSettlesTheParent()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Task A1a"), true);
		var storyA1 = Row(table, "Story A1");

		storyA1.ClearChildren();

		Assert.NotEqual(TableCheckState.Indeterminate, table.GetCheckState(storyA1));
	}

	/// <summary>
	/// Turning cascade on after rows are already checked must not leave parents stale, since
	/// nothing recomputed them while it was off.
	/// </summary>
	[Fact]
	public void TurningCascadeOnSettlesWhatIsAlreadyChecked()
	{
		var table = Backlog();
		table.SetCheckState(Row(table, "Task A1a"), true);

		table.CascadeChecks = true;

		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(Row(table, "Story A1")));
	}

	/// <summary>A row detached from the table falls back to its own field rather than throwing.</summary>
	[Fact]
	public void ADetachedRowStillAnswersIsChecked()
	{
		var orphan = new TreeTableRow("Nowhere");

		orphan.IsChecked = true;

		Assert.True(orphan.IsChecked);
	}

	#endregion

	#region Structural paths found by review

	/// <summary>
	/// The inherited flat RemoveRow(index) reaches the same structural change as
	/// TreeTableRow.RemoveChild, so it must settle the parent the same way.
	/// </summary>
	[Fact]
	public void RemovingByIndexSettlesTheParent()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		table.SetCheckState(Row(table, "Task A1a"), true);
		var storyA1 = Row(table, "Story A1");
		int taskA1bIndex = table.Rows.ToList().FindIndex(r => r.Cells[0] == "Task A1b");

		table.RemoveRow(taskA1bIndex);

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(storyA1));
	}

	/// <summary>Moving a checked subtree must settle both the parent it left and the one it joined.</summary>
	[Fact]
	public void MovingASubtreeSettlesBothParents()
	{
		var table = Backlog();
		table.CascadeChecks = true;
		var storyA1 = Row(table, "Story A1");
		table.SetCheckState(storyA1, true);
		var featureB = Row(table, "Feature B");

		table.MoveRow(storyA1, featureB, 0);

		Assert.Equal(TableCheckState.Indeterminate, table.GetCheckState(featureB));
	}

	#endregion

	#region A tree WITHOUT checkbox mode is unchanged

	/// <summary>
	/// The tree as it was: no checkbox mode. Space must still expand and collapse, which is what
	/// it did before checkboxes existed.
	/// </summary>
	[Fact]
	public void WithoutCheckboxModeSpaceStillExpands()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var table = Backlog();   // no CheckboxMode, no MultiSelect
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(table, FocusReason.Programmatic);
		system.Render.UpdateDisplay();

		var featureA = Row(table, "Feature A");
		featureA.IsExpanded = false;
		table.SelectedRowIndex = 0;

		Press(system, ConsoleKey.Spacebar, ' ');

		Assert.True(featureA.IsExpanded);
	}

	[Fact]
	public void WithoutCascadeStructuralChangesDoNotTouchState()
	{
		var table = Backlog();   // cascade off, as it defaults
		var storyA1 = Row(table, "Story A1");
		table.SetCheckState(storyA1, true);

		storyA1.AddChild("Task A1c");

		Assert.Equal(TableCheckState.Checked, table.GetCheckState(storyA1));
	}

	[Fact]
	public void WithoutCheckboxModeNothingIsChecked()
	{
		var table = Backlog();

		table.ToggleRowSelection(0);

		Assert.Empty(table.GetCheckedRows());
	}

	#endregion
}
