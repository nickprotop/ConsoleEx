// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="RadioControl{T}"/>.
/// </summary>
/// <typeparam name="T">The value type this radio represents.</typeparam>
public sealed class RadioBuilder<T> : IControlBuilder<RadioControl<T>>
{
	private readonly RadioGroup<T> _group;
	private readonly T _value;
	private string _label;
	private ColorRole _role = ColorRole.Default;
	private ThemeMode? _colorRoleMode;
	private bool _outline;
	private string? _selectedCharacter;
	private string? _unselectedCharacter;
	private bool _wrap = true;
	private bool _selected;
	private string? _name;
	private Margin _margin = new(0, 0, 0, 0);
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;

	/// <summary>
	/// Initializes a new <see cref="RadioBuilder{T}"/> for the given group and value.
	/// </summary>
	/// <param name="group">The coordinating group.</param>
	/// <param name="value">The value this radio represents.</param>
	/// <param name="label">The text label displayed next to the radio.</param>
	public RadioBuilder(RadioGroup<T> group, T value, string label = "")
	{
		_group = group;
		_value = value;
		_label = label;
	}

	/// <summary>Sets the label text displayed next to the radio.</summary>
	/// <param name="label">The label text.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithLabel(string label)
	{
		_label = label;
		return this;
	}

	/// <summary>Sets the control's semantic colour role.</summary>
	/// <param name="role">The semantic role determining the radio's colours.</param>
	/// <param name="mode">Optional <see cref="ThemeMode"/> override. When null, the active theme's mode is used.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithColorRole(ColorRole role, ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return this;
	}

	/// <summary>Renders the radio in outline style (role colour on text, surface fill).</summary>
	/// <param name="outline">Whether to use outline style.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> Outline(bool outline = true)
	{
		_outline = outline;
		return this;
	}

	/// <summary>Sets the character displayed when this radio is selected (default "●").</summary>
	/// <param name="character">The selected character.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithSelectedCharacter(string character)
	{
		_selectedCharacter = character;
		return this;
	}

	/// <summary>Sets the character displayed when this radio is unselected (default "○").</summary>
	/// <param name="character">The unselected character.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithUnselectedCharacter(string character)
	{
		_unselectedCharacter = character;
		return this;
	}

	/// <summary>Enables or disables label wrapping across lines.</summary>
	/// <param name="wrap">Whether to wrap the label.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> Wrap(bool wrap = true)
	{
		_wrap = wrap;
		return this;
	}

	/// <summary>
	/// Marks this radio as the initially selected option. In <see cref="Build"/>, the group's
	/// <see cref="RadioGroup{T}.SelectedValue"/> is set to this radio's value.
	/// </summary>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> Selected()
	{
		_selected = true;
		return this;
	}

	/// <summary>Sets the control name for programmatic lookup.</summary>
	/// <param name="name">The control name.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>Sets the margin around the control.</summary>
	/// <param name="left">Left margin.</param>
	/// <param name="top">Top margin.</param>
	/// <param name="right">Right margin.</param>
	/// <param name="bottom">Bottom margin.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>Sets a uniform margin on all sides.</summary>
	/// <param name="margin">The margin value.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>Sets the horizontal alignment of the radio within its allocated space.</summary>
	/// <param name="alignment">The horizontal alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>Sets the vertical alignment of the radio within its allocated space.</summary>
	/// <param name="alignment">The vertical alignment.</param>
	/// <returns>The builder for chaining.</returns>
	public RadioBuilder<T> WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>Builds and returns a configured <see cref="RadioControl{T}"/>.</summary>
	public RadioControl<T> Build()
	{
		var radio = new RadioControl<T>(_group, _value, _label)
		{
			ColorRole = _role,
			ColorRoleMode = _colorRoleMode,
			Outline = _outline,
			Wrap = _wrap,
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Name = _name
		};

		if (_selectedCharacter != null)
			radio.SelectedCharacter = _selectedCharacter;
		if (_unselectedCharacter != null)
			radio.UnselectedCharacter = _unselectedCharacter;

		if (_selected)
		{
			// If another member already registered itself as the selection, last-registered wins.
			// Emit a Debug log so a double-.Selected() misconfiguration is diagnosable (spec §3).
			if (_group.HasSelection &&
				!EqualityComparer<T>.Default.Equals(_group.SelectedValue!, _value))
			{
				var log = _group.SelectedRadio?.GetLogService()
					?? _group.Members.FirstOrDefault()?.GetLogService();
				log?.LogDebug(
					$"RadioBuilder: '.Selected()' on value '{_value}' overrides the group's existing " +
					$"selection '{_group.SelectedValue}' (last-registered wins).",
					category: "Interaction");
			}

			_group.SelectedValue = _value;
		}

		BindingHelper.ApplyDeferredBindings(this, radio);
		return radio;
	}

	/// <summary>Implicit conversion to <see cref="RadioControl{T}"/>.</summary>
	public static implicit operator RadioControl<T>(RadioBuilder<T> builder) => builder.Build();
}
