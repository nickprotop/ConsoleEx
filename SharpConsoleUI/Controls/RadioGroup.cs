// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Non-visual coordination object for a set of <see cref="RadioControl{T}"/> — the single source
	/// of truth for which value is selected. Radios reference the group; the group enforces single-selection.
	/// </summary>
	/// <typeparam name="T">The value type each radio represents.</typeparam>
	public sealed class RadioGroup<T>
	{
		private readonly List<RadioControl<T>> _members = new();
		private T? _selectedValue;
		private bool _hasSelection;

		/// <summary>
		/// Gets or sets the currently selected value, or <c>default(T)</c>/none when nothing is selected.
		/// Setting it updates the selection (honoring <see cref="Required"/>), repaints affected members,
		/// and fires <see cref="SelectionChanged"/> once.
		/// </summary>
		public T? SelectedValue
		{
			get => _selectedValue;
			set => SetSelected(value, hasSelection: true);
		}

		/// <summary>Gets a value indicating whether a value is currently selected.</summary>
		public bool HasSelection => _hasSelection;

		/// <summary>
		/// Gets or sets a value indicating whether clicking the already-selected radio clears the selection.
		/// Only takes effect when <see cref="Required"/> is false (Required wins).
		/// </summary>
		public bool AllowDeselect { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the group cannot return to "none" once a value is
		/// selected (deselect and <see cref="Clear"/> become no-ops while a selection exists). Does NOT
		/// force an initial selection.
		/// </summary>
		public bool Required { get; set; } = false;

		/// <summary>Gets the member whose <see cref="RadioControl{T}.Value"/> equals the current selection, or null.</summary>
		public RadioControl<T>? SelectedRadio
		{
			get
			{
				if (!_hasSelection) return null;
				foreach (var m in _members)
					if (EqualityComparer<T>.Default.Equals(m.Value, _selectedValue!)) return m;
				return null;
			}
		}

		/// <summary>Gets the radios registered to this group, in registration order.</summary>
		public IReadOnlyList<RadioControl<T>> Members => _members;

		/// <summary>
		/// Clears the selection to none. No-op when <see cref="Required"/> is true and a selection exists.
		/// </summary>
		public void Clear()
		{
			if (Required && _hasSelection) return;
			SetSelected(default, hasSelection: false);
		}

		/// <summary>Occurs when the selected value changes.</summary>
		public event EventHandler<T?>? SelectionChanged;

		/// <summary>Async counterpart of <see cref="SelectionChanged"/>.</summary>
		public event Core.AsyncEventHandler<T?>? SelectionChangedAsync;

		internal void Register(RadioControl<T> radio)
		{
			if (!_members.Contains(radio)) _members.Add(radio);
		}

		internal void Unregister(RadioControl<T> radio) => _members.Remove(radio);

		/// <summary>Called by a radio when the user activates it (click / Space / Enter / Select()).</summary>
		internal void RequestSelect(T value) => SetSelected(value, hasSelection: true);

		/// <summary>Called by a radio when the user activates the ALREADY-selected radio.</summary>
		internal void RequestToggle(RadioControl<T> radio)
		{
			bool isSelected = _hasSelection && EqualityComparer<T>.Default.Equals(radio.Value, _selectedValue!);
			if (isSelected)
			{
				if (Required) return;                 // Required wins over AllowDeselect
				if (AllowDeselect) SetSelected(default, hasSelection: false);
				// AllowDeselect false → no-op (classic radio)
			}
			else
			{
				SetSelected(radio.Value, hasSelection: true);
			}
		}

		private void SetSelected(T? value, bool hasSelection)
		{
			bool sameValue = _hasSelection == hasSelection &&
				(!hasSelection || EqualityComparer<T>.Default.Equals(_selectedValue!, value!));
			if (sameValue) return;

			// Capture the members whose Checked state flips (old + new), then repaint only those.
			var old = SelectedRadio;
			_selectedValue = value;
			_hasSelection = hasSelection;
			var @new = SelectedRadio;

			old?.RepaintFromGroup();
			@new?.RepaintFromGroup();

			Core.AsyncEvent.Raise(SelectionChanged, SelectionChangedAsync, this,
				_hasSelection ? _selectedValue : default,
				_members.Count > 0 ? _members[0].GetLogService() : null);
		}
	}
}
