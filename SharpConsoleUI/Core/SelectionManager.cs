// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Event payload describing a change to a window's active text selection.
	/// </summary>
	/// <param name="Active">The control that owns the active selection, or <c>null</c> if the selection was cleared.</param>
	/// <param name="SelectedText">The currently selected plain text, or <c>null</c> if nothing is selected.</param>
	public record SelectionChangedEventArgs(ISelectableControl? Active, string? SelectedText);

	/// <summary>
	/// Manages text selection within a single <see cref="Window"/>. This is the single source of
	/// truth for which <see cref="ISelectableControl"/> owns the active selection. Only one control
	/// may have a selection at a time: <see cref="SetActiveSelection"/> clears the previously active
	/// control's selection before adopting the new one. Mirrors the per-window <see cref="FocusManager"/>.
	/// </summary>
	/// <remarks>
	/// Re-entrancy: a control's <see cref="ISelectableControl.ClearSelection"/> must NOT call back into
	/// <see cref="SetActiveSelection"/>. It is only expected to reset the control's own state and invalidate.
	/// </remarks>
	public class SelectionManager
	{
		private readonly Window _window;

		/// <summary>Gets the control that currently owns the active selection, or <c>null</c>.</summary>
		public ISelectableControl? ActiveSelection { get; private set; }

		/// <summary>Gets whether any control in the window currently has a non-empty selection.</summary>
		public bool HasSelection => ActiveSelection?.HasSelection == true;

		/// <summary>Fired whenever the active selection changes (adopted, updated, or cleared).</summary>
		public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

		/// <summary>Initializes a new <see cref="SelectionManager"/> for the given window.</summary>
		public SelectionManager(Window window)
		{
			_window = window;
		}

		/// <summary>
		/// Makes <paramref name="control"/> the owner of the active selection. If a different control
		/// previously owned the selection, its selection is cleared first (single-selection invariant).
		/// Controls call this when they begin or extend a selection.
		/// </summary>
		public void SetActiveSelection(ISelectableControl control)
		{
			if (control == null) return;

			if (!ReferenceEquals(ActiveSelection, control))
			{
				var previous = ActiveSelection;
				previous?.ClearSelection();
				(previous as IWindowControl)?.Container?.Invalidate(Invalidation.Relayout);
				ActiveSelection = control;
			}

			RaiseChanged();
		}

		/// <summary>Gets the active selection's plain text, or <c>null</c> if nothing is selected.</summary>
		public string? GetSelectedText() => HasSelection ? ActiveSelection!.GetSelectedText() : null;

		/// <summary>Clears the active selection, if any.</summary>
		public void ClearSelection()
		{
			var previous = ActiveSelection;
			if (previous == null) return;

			previous.ClearSelection();
			(previous as IWindowControl)?.Container?.Invalidate(Invalidation.Relayout);
			ActiveSelection = null;
			RaiseChanged();
		}

		private void RaiseChanged()
		{
			SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(ActiveSelection, GetSelectedText()));
		}
	}
}
