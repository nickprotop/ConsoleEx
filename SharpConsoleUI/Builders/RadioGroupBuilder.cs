// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="RadioGroup{T}"/> — the non-visual coordination object that owns
/// the single-selection invariant for a set of <see cref="RadioControl{T}"/> instances.
/// </summary>
/// <typeparam name="T">The value type each radio in the group represents.</typeparam>
public sealed class RadioGroupBuilder<T>
{
	private bool _allowDeselect;
	private bool _required;
	private bool _hasInitialValue;
	private T? _selectedValue;
	private Action<T?>? _selectionChangedHandler;

	/// <summary>
	/// Allows clicking the already-selected radio to clear the selection.
	/// Only takes effect when <see cref="Required"/> is false (Required wins).
	/// </summary>
	/// <param name="allow">Whether to allow deselecting.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioGroupBuilder<T> AllowDeselect(bool allow = true)
	{
		_allowDeselect = allow;
		return this;
	}

	/// <summary>
	/// Prevents the group from returning to "none" once a value is selected.
	/// </summary>
	/// <param name="required">Whether selection is required.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioGroupBuilder<T> Required(bool required = true)
	{
		_required = required;
		return this;
	}

	/// <summary>
	/// Sets the initial selected value of the group.
	/// </summary>
	/// <param name="value">The value to pre-select.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioGroupBuilder<T> WithSelectedValue(T value)
	{
		_selectedValue = value;
		_hasInitialValue = true;
		return this;
	}

	/// <summary>
	/// Subscribes to the <see cref="RadioGroup{T}.SelectionChanged"/> event in <see cref="Build"/>.
	/// </summary>
	/// <param name="handler">The action to invoke when the selection changes.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioGroupBuilder<T> OnSelectionChanged(Action<T?> handler)
	{
		_selectionChangedHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds and returns a configured <see cref="RadioGroup{T}"/>.
	/// </summary>
	public RadioGroup<T> Build()
	{
		var group = new RadioGroup<T>
		{
			AllowDeselect = _allowDeselect,
			Required = _required
		};

		if (_selectionChangedHandler != null)
			group.SelectionChanged += (_, value) => _selectionChangedHandler(value);

		if (_hasInitialValue)
			group.SelectedValue = _selectedValue;

		return group;
	}
}
