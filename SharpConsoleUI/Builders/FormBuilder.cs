// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="FormControl"/> instances.
/// Holds a live <see cref="FormControl"/> and delegates all Add* / section / row / button
/// methods to it, returning <c>this</c> for chaining. <see cref="Build"/> returns the
/// underlying form; the implicit operator allows passing a builder wherever a
/// <see cref="FormControl"/> is expected.
/// </summary>
public sealed class FormBuilder : IControlBuilder<FormControl>
{
	private readonly FormControl _form = new();
	private Action<IReadOnlyDictionary<string, string?>>? _onSubmit;
	private Action? _onCancel;
	private string? _name;

	// -----------------------------------------------------------------------
	// Field adders — delegate straight to FormControl
	// -----------------------------------------------------------------------

	/// <summary>
	/// Adds a single-line text field backed by a <see cref="PromptControl"/>.
	/// </summary>
	public FormBuilder AddText(
		string name,
		string label,
		string initial = "",
		Func<string?, string?>? validate = null,
		bool required = false,
		string? hint = null)
	{
		_form.AddText(name, label, initial, validate, required, hint);
		return this;
	}

	/// <summary>
	/// Adds a multi-line text field backed by a <see cref="MultilineEditControl"/>.
	/// </summary>
	public FormBuilder AddMultilineEdit(string name, string label, string initial = "", int height = 3, string? hint = null)
	{
		_form.AddMultilineEdit(name, label, initial, height, hint);
		return this;
	}

	/// <summary>
	/// Adds a boolean field backed by a <see cref="CheckboxControl"/>.
	/// The value is <c>"true"</c> or <c>"false"</c>.
	/// </summary>
	public FormBuilder AddCheckbox(string name, string label, bool initial = false, string? hint = null)
	{
		_form.AddCheckbox(name, label, initial, hint);
		return this;
	}

	/// <summary>
	/// Adds a single-select field backed by a <see cref="DropdownControl"/>.
	/// </summary>
	public FormBuilder AddDropdown(string name, string label, IEnumerable<string> options, string? initial = null, string? hint = null)
	{
		_form.AddDropdown(name, label, options, initial, hint);
		return this;
	}

	/// <summary>
	/// Adds a typed radio-group field.
	/// </summary>
	public FormBuilder AddRadio<T>(string name, string label, IEnumerable<(T Value, string Label)> options, string? hint = null)
	{
		_form.AddRadio(name, label, options, hint);
		return this;
	}

	/// <summary>
	/// Adds a string radio-group field where each option string is both value and display label.
	/// </summary>
	public FormBuilder AddRadio(string name, string label, params string[] options)
	{
		_form.AddRadio(name, label, options);
		return this;
	}

	/// <summary>
	/// Adds a numeric slider field.
	/// </summary>
	public FormBuilder AddSlider(string name, string label, double min, double max, double initial, string? hint = null)
	{
		_form.AddSlider(name, label, min, max, initial, hint);
		return this;
	}

	/// <summary>
	/// Adds a field with a caller-supplied editor and value getter.
	/// </summary>
	public FormBuilder AddField(
		string name,
		string label,
		IWindowControl editor,
		Func<string?> valueGetter,
		Func<string?, string?>? validate = null,
		bool required = false,
		string? hint = null)
	{
		_form.AddField(name, label, editor, valueGetter, validate, required, hint);
		return this;
	}

	// -----------------------------------------------------------------------
	// Section / row / buttons
	// -----------------------------------------------------------------------

	/// <summary>
	/// Starts a collapsible (or plain) field group as a full-width header row.
	/// Pass <c>null</c> to end the current section.
	/// </summary>
	public FormBuilder AddSection(string? title, bool collapsible = false, bool startCollapsed = false)
	{
		_form.AddSection(title, collapsible, startCollapsed);
		return this;
	}

	/// <summary>
	/// Adds several fields onto a single grid row, packed side by side.
	/// </summary>
	public FormBuilder AddRow(params Action<FormControl>[] fieldAdders)
	{
		_form.AddRow(fieldAdders);
		return this;
	}

	/// <summary>
	/// Adds a final, full-width button row (OK + optional Cancel).
	/// </summary>
	public FormBuilder WithButtons(string ok = "OK", string cancel = "Cancel", bool showCancel = true)
	{
		_form.WithButtons(ok, cancel, showCancel);
		return this;
	}

	// -----------------------------------------------------------------------
	// Layout / display
	// -----------------------------------------------------------------------

	/// <summary>
	/// Sets the grid column gap.
	/// </summary>
	public FormBuilder WithColumnGap(int gap)
	{
		_form.ColumnGap = gap;
		return this;
	}

	/// <summary>
	/// Sets the grid row gap.
	/// </summary>
	public FormBuilder WithRowGap(int gap)
	{
		_form.RowGap = gap;
		return this;
	}

	/// <summary>
	/// Sets the control's <see cref="SharpConsoleUI.Controls.BaseControl.Name"/> for lookup.
	/// </summary>
	public FormBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	// -----------------------------------------------------------------------
	// Event wiring
	// -----------------------------------------------------------------------

	/// <summary>
	/// Registers a callback to invoke when the form is submitted successfully.
	/// Subscribed to <see cref="FormControl.Submitted"/> immediately.
	/// </summary>
	public FormBuilder OnSubmit(Action<IReadOnlyDictionary<string, string?>> handler)
	{
		_onSubmit = handler;
		return this;
	}

	/// <summary>
	/// Registers a callback to invoke when the form is cancelled.
	/// Subscribed to <see cref="FormControl.Cancelled"/> immediately.
	/// </summary>
	public FormBuilder OnCancel(Action handler)
	{
		_onCancel = handler;
		return this;
	}

	// -----------------------------------------------------------------------
	// Build
	// -----------------------------------------------------------------------

	/// <summary>
	/// Wires any registered event handlers, applies deferred bindings, and returns the
	/// underlying <see cref="FormControl"/>.
	/// </summary>
	public FormControl Build()
	{
		if (_name != null)
			_form.Name = _name;

		if (_onSubmit != null)
			_form.Submitted += (_, values) => _onSubmit(values);

		if (_onCancel != null)
			_form.Cancelled += (_, _) => _onCancel();

		BindingHelper.ApplyDeferredBindings(this, _form);
		return _form;
	}

	/// <summary>
	/// Implicit conversion so a <see cref="FormBuilder"/> can be used wherever a
	/// <see cref="FormControl"/> is expected.
	/// </summary>
	public static implicit operator FormControl(FormBuilder builder) => builder.Build();
}
