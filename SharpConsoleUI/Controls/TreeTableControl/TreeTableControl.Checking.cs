// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls;

/// <summary>
/// Checkbox state for a table whose rows nest.
/// </summary>
/// <remarks>
/// <para>
/// State lives here, keyed by row, rather than on the row itself. A tree legitimately holds plain
/// <see cref="TableRow"/> objects as childless roots, and those have nowhere to put it; a field on
/// <see cref="TreeTableRow"/> would leave them with a checkbox on screen that silently does
/// nothing.
/// </para>
/// <para>
/// It is also separate from selection, which the flat table ties checking to. Checking answers
/// "which rows do I act on", selection answers "where is the cursor", and a tree needs to ask the
/// first without moving the second.
/// </para>
/// </remarks>
public partial class TreeTableControl
{
	/// <inheritdoc/>
	/// <remarks>
	/// False: a tree keeps check state of its own, so the selection APIs must not write to it.
	/// Without this, <see cref="TableControl.ClearSelection"/> and a plain click on any
	/// non-checkbox cell would both wipe every check the user had made.
	/// </remarks>
	protected override bool ChecksFollowSelection => false;

	private readonly Dictionary<TableRow, TableCheckState> _checkStates =
		new(ReferenceEqualityComparer.Instance);

	/// <summary>Raised for each row whose check state changed.</summary>
	/// <remarks>
	/// Checking is separate from selection here, so the selection events say nothing about it. A
	/// cascade raises this once per row it actually changed, which is what lets a caller keep a
	/// count in step without walking the tree.
	/// </remarks>
	public event EventHandler<TableRow>? CheckChanged;

	private bool _cascadeChecks;

	/// <summary>
	/// Whether checking a row also checks the rows nested under it, and whether a row shows the
	/// state computed from them. Default false, which leaves every checkbox independent.
	/// </summary>
	public bool CascadeChecks
	{
		get => _cascadeChecks;
		set
		{
			if (_cascadeChecks == value) return;
			_cascadeChecks = value;
			OnPropertyChanged();

			// Nothing recomputed parents while this was off, so rows checked beforehand would
			// leave their ancestors reporting a state their children contradict.
			if (value) SettleEveryParent();

			Invalidate(Invalidation.Repaint);
		}
	}

	private bool _showParentCheckboxes = true;

	/// <summary>
	/// Whether rows that have children show a checkbox. Default true. When false, such a row keeps
	/// every one of its columns and only the checkbox cell is blank, so the hierarchy reads as
	/// structure and the leaves are the targets.
	/// </summary>
	/// <remarks>
	/// Hiding affects painting and hit-testing only. A hidden checkbox can still be set from code,
	/// still appears in <see cref="TableControl.GetCheckedRows"/> when checked, and still takes
	/// part in the cascade.
	/// </remarks>
	public bool ShowParentCheckboxes
	{
		get => _showParentCheckboxes;
		set
		{
			if (_showParentCheckboxes == value) return;
			_showParentCheckboxes = value;
			OnPropertyChanged();
			Invalidate(Invalidation.Repaint);
		}
	}

	/// <summary>Whether a row draws a checkbox the user can click.</summary>
	/// <param name="row">The row to ask about.</param>
	/// <remarks>
	/// A row's own <see cref="TreeTableRow.ShowCheckbox"/> decides when it is set; otherwise a row
	/// with children follows <see cref="ShowParentCheckboxes"/> and a leaf always shows one. A
	/// plain <see cref="TableRow"/> is a childless root, so it always shows one.
	/// </remarks>
	public bool IsCheckboxVisible(TableRow row)
	{
		if (row is null) throw new ArgumentNullException(nameof(row));

		if (row is not TreeTableRow treeRow) return true;
		if (treeRow.ShowCheckbox is { } explicitChoice) return explicitChoice;

		return treeRow.ChildList.Count == 0 || _showParentCheckboxes;
	}

	/// <summary>Reads a row's check state. A row never checked reads as unchecked.</summary>
	/// <param name="row">The row to read.</param>
	public TableCheckState GetCheckState(TableRow row)
	{
		if (row is null) throw new ArgumentNullException(nameof(row));

		lock (_checkStates)
		{
			return _checkStates.TryGetValue(row, out var state) ? state : TableCheckState.Unchecked;
		}
	}

	/// <summary>Checks or unchecks a row.</summary>
	/// <param name="row">The row to change.</param>
	/// <param name="isChecked">True to check it, false to uncheck it.</param>
	/// <remarks>
	/// Takes a bool rather than a <see cref="TableCheckState"/> on purpose: indeterminate is
	/// computed from the rows underneath, so there is no meaningful way for a caller to assign it.
	/// </remarks>
	public void SetCheckState(TableRow row, bool isChecked)
	{
		if (row is null) throw new ArgumentNullException(nameof(row));

		var state = isChecked ? TableCheckState.Checked : TableCheckState.Unchecked;

		if (!_cascadeChecks || row is not TreeTableRow treeRow)
		{
			Store(row, state);
			return;
		}

		// Down first, so every descendant agrees with what was just asked for, then up, so each
		// ancestor reports what its own children now say. Eager rather than deferred: these trees
		// are small, and a state that is only correct after the next paint is a state a caller
		// cannot read.
		ApplyToSubtree(treeRow, state);
		RecomputeAncestors(treeRow.Parent);
	}

	/// <summary>Writes one state across a row and everything nested under it.</summary>
	private void ApplyToSubtree(TreeTableRow row, TableCheckState state)
	{
		Store(row, state);
		foreach (var child in row.ChildList)
			ApplyToSubtree(child, state);
	}

	/// <summary>
	/// Settles a row and the rows above it after its children changed, so a parent never reports a
	/// state its own children contradict. Does nothing unless the tree cascades, where a row's
	/// state is its own rather than derived.
	/// </summary>
	/// <param name="parent">The row whose children changed, or null when there is none.</param>
	internal void SettleAncestorsAfterStructureChange(TreeTableRow? parent)
	{
		if (!_cascadeChecks || parent is null) return;

		RecomputeAncestors(parent);
	}

	/// <summary>Settles every row that has children, deepest first.</summary>
	private void SettleEveryParent()
	{
		foreach (var root in RootRows)
			SettleBranch(root as TreeTableRow);
	}

	/// <summary>Settles a row after its own children, so a parent sees them already settled.</summary>
	private void SettleBranch(TreeTableRow? row)
	{
		if (row is null || row.ChildList.Count == 0) return;

		foreach (var child in row.ChildList)
			SettleBranch(child);

		Store(row, StateFromChildren(row));
	}

	/// <summary>Walks up from a row, settling each ancestor against its own children.</summary>
	private void RecomputeAncestors(TreeTableRow? row)
	{
		for (var current = row; current is not null; current = current.Parent)
			Store(current, StateFromChildren(current));
	}

	/// <summary>
	/// What a row's own children say: checked when they all are, unchecked when none are,
	/// indeterminate otherwise. A row with no children keeps whatever it already holds, which is
	/// why a leaf can never be indeterminate.
	/// </summary>
	private TableCheckState StateFromChildren(TreeTableRow row)
	{
		// A row with no children keeps whatever it holds, except indeterminate: that state only
		// ever meant "the rows under this disagree", so a row with none left cannot stay in it.
		// Unchecked rather than checked, because nothing under it was ever confirmed.
		if (row.ChildList.Count == 0)
		{
			var own = GetCheckState(row);
			return own == TableCheckState.Indeterminate ? TableCheckState.Unchecked : own;
		}

		bool anyChecked = false;
		bool anyUnchecked = false;

		foreach (var child in row.ChildList)
		{
			switch (GetCheckState(child))
			{
				case TableCheckState.Indeterminate:
					return TableCheckState.Indeterminate;
				case TableCheckState.Checked:
					anyChecked = true;
					break;
				default:
					anyUnchecked = true;
					break;
			}

			if (anyChecked && anyUnchecked) return TableCheckState.Indeterminate;
		}

		return anyChecked ? TableCheckState.Checked : TableCheckState.Unchecked;
	}

	/// <summary>The checked rows that have no rows nested under them.</summary>
	/// <returns>Checked leaf rows, in no particular order.</returns>
	/// <remarks>
	/// <para>
	/// What a batch caller wants. <see cref="TableControl.GetCheckedRows"/> reports every checked
	/// row, group rows included, and those usually carry no record of their own - a caller
	/// filtering them out by tag type gets silence when it checks a group.
	/// </para>
	/// <para>
	/// Includes rows that are collapsed or filtered out of view. A check is an instruction, not a
	/// view state: a user who checks a group and then collapses it still means those rows. A
	/// caller showing a count should take it from here, so the number does not change when the
	/// user folds something away.
	/// </para>
	/// </remarks>
	public List<TableRow> GetCheckedLeaves()
	{
		// SyncRoot first, then _checkStates: the same order every other path takes, so adding a
		// lock here cannot introduce a cycle. Without it a caller on another thread can read
		// ChildList while a structural change is part-way through mutating it.
		lock (SyncRoot)
			lock (_checkStates)
			{
				return _checkStates
					.Where(entry => entry.Value == TableCheckState.Checked && IsLeaf(entry.Key))
					.Select(entry => entry.Key)
					.ToList();
			}
	}

	/// <summary>Whether a row has nothing nested under it. A plain row never has.</summary>
	/// <remarks>
	/// Reads ChildList, which structural changes mutate under the table's own lock, so callers
	/// take that lock first. The library's rule is that the hierarchy is the UI thread's, but
	/// these read as query APIs that a worker may well call.
	/// </remarks>
	private static bool IsLeaf(TableRow row) =>
		row is not TreeTableRow treeRow || treeRow.ChildList.Count == 0;

	/// <inheritdoc/>
	protected override string GetCheckboxMarkup(TableRow? row, bool isChecked)
	{
		if (row is null) return base.GetCheckboxMarkup(row, isChecked);
		if (!IsCheckboxVisible(row)) return Configuration.ControlDefaults.TableCheckboxHidden;

		return GetCheckState(row) switch
		{
			TableCheckState.Checked => Configuration.ControlDefaults.TableCheckboxChecked,
			TableCheckState.Indeterminate => Configuration.ControlDefaults.TableCheckboxIndeterminate,
			_ => Configuration.ControlDefaults.TableCheckboxUnchecked,
		};
	}

	/// <summary>The checkbox cell a row would paint. For tests.</summary>
	internal string CheckboxMarkupFor(TableRow row) =>
		GetCheckboxMarkup(row, GetCheckState(row) == TableCheckState.Checked);

	/// <summary>Writes one row's state and repaints it.</summary>
	private void Store(TableRow row, TableCheckState state)
	{
		bool changed;

		lock (_checkStates)
		{
			changed = GetStateLocked(row) != state;
			if (state == TableCheckState.Unchecked) _checkStates.Remove(row);
			else _checkStates[row] = state;
		}

		if (!changed) return;

		OnRowDisplayChanged(row, false, Invalidation.Repaint);
		CheckChanged?.Invoke(this, row);
	}

	/// <summary>Reads a state while the caller already holds the lock.</summary>
	private TableCheckState GetStateLocked(TableRow row) =>
		_checkStates.TryGetValue(row, out var state) ? state : TableCheckState.Unchecked;

	/// <summary>Forgets a row's state, for a row leaving the tree.</summary>
	private void ForgetCheckState(TableRow row)
	{
		lock (_checkStates)
		{
			_checkStates.Remove(row);
		}
	}
}
