// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Shared fluent builder base for <see cref="FlowControl"/> and its subclasses (e.g.
/// <see cref="WizardControl"/>). It is self-typed (<typeparamref name="TSelf"/>) so every fluent
/// method returns the concrete builder type, keeping the chain intact through inheritance; subclasses
/// only choose which control <see cref="Apply{T}"/> is given.
/// The control's own layout defaults (<see cref="VerticalAlignment.Fill"/> /
/// <see cref="HorizontalAlignment.Stretch"/>) are preserved unless explicitly overridden, since a
/// FlowControl must fill the slot it is placed in (overriding them with the usual Top/Left would
/// collapse the inline region to a single row).
/// </summary>
/// <typeparam name="TSelf">The concrete builder type (returned from every fluent method).</typeparam>
public abstract class FlowControlBuilderBase<TSelf>
	where TSelf : FlowControlBuilderBase<TSelf>
{
	private IWindowControl? _placeholder;
	private string? _name;
	private object? _tag;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private int? _width;
	private int? _height;
	// Default to the FlowControl identity (Fill/Stretch), NOT the BaseControl Top/Left.
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Stretch;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
	private StickyPosition _stickyPosition = StickyPosition.None;

	private TSelf Self => (TSelf)this;

	/// <summary>Sets the idle/done placeholder control shown when no flow is running.</summary>
	public TSelf WithPlaceholder(IWindowControl placeholder) { _placeholder = placeholder; return Self; }

	/// <summary>Sets the control name (for lookup via <c>FindControl</c>).</summary>
	public TSelf WithName(string name) { _name = name; return Self; }

	/// <summary>Sets the control tag for custom data storage.</summary>
	public TSelf WithTag(object tag) { _tag = tag; return Self; }

	/// <summary>Sets the control margin (left, top, right, bottom).</summary>
	public TSelf WithMargin(int left, int top, int right, int bottom) { _margin = new Margin(left, top, right, bottom); return Self; }

	/// <summary>Sets a uniform control margin.</summary>
	public TSelf WithMargin(int margin) { _margin = new Margin(margin, margin, margin, margin); return Self; }

	/// <summary>Sets the control margin.</summary>
	public TSelf WithMargin(Margin margin) { _margin = margin; return Self; }

	/// <summary>Sets whether the control is visible.</summary>
	public TSelf Visible(bool visible = true) { _visible = visible; return Self; }

	/// <summary>Sets an explicit width (columns); omit to fill the parent's width.</summary>
	public TSelf WithWidth(int width) { _width = width; return Self; }

	/// <summary>Sets an explicit height (rows); omit to fill the slot the parent allots.</summary>
	public TSelf WithHeight(int height) { _height = height; return Self; }

	/// <summary>
	/// Overrides the horizontal alignment (default <see cref="HorizontalAlignment.Stretch"/>). Changing
	/// this away from Stretch may collapse the inline region — prefer leaving the default.
	/// </summary>
	public TSelf WithHorizontalAlignment(HorizontalAlignment alignment) { _horizontalAlignment = alignment; return Self; }

	/// <summary>
	/// Overrides the vertical alignment (default <see cref="VerticalAlignment.Fill"/>). Changing this
	/// away from Fill may collapse the inline region — prefer leaving the default.
	/// </summary>
	public TSelf WithVerticalAlignment(VerticalAlignment alignment) { _verticalAlignment = alignment; return Self; }

	/// <summary>Sets the sticky position.</summary>
	public TSelf WithStickyPosition(StickyPosition position) { _stickyPosition = position; return Self; }

	/// <summary>Makes the control stick to the top of the window.</summary>
	public TSelf StickyTop() { _stickyPosition = StickyPosition.Top; return Self; }

	/// <summary>Makes the control stick to the bottom of the window.</summary>
	public TSelf StickyBottom() { _stickyPosition = StickyPosition.Bottom; return Self; }

	/// <summary>
	/// Applies the builder's configuration to <paramref name="control"/> and returns it. Subclasses call
	/// this from their <c>Build()</c> with the concrete <see cref="FlowControl"/> they construct.
	/// </summary>
	/// <typeparam name="T">The concrete <see cref="FlowControl"/> type to configure.</typeparam>
	/// <param name="control">The control to configure.</param>
	/// <returns>The configured control.</returns>
	protected T Apply<T>(T control) where T : FlowControl
	{
		if (_placeholder != null)
			control.Placeholder = _placeholder;
		control.Name = _name;
		control.Tag = _tag;
		control.Margin = _margin;
		control.Visible = _visible;
		control.Width = _width;
		control.Height = _height;
		control.HorizontalAlignment = _horizontalAlignment;
		control.VerticalAlignment = _verticalAlignment;
		control.StickyPosition = _stickyPosition;

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}
}

/// <summary>Fluent builder for <see cref="FlowControl"/>.</summary>
public sealed class FlowControlBuilder : FlowControlBuilderBase<FlowControlBuilder>, IControlBuilder<FlowControl>
{
	/// <summary>Builds the configured <see cref="FlowControl"/>.</summary>
	public FlowControl Build() => Apply(new FlowControl());
}
