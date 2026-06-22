// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating <see cref="CollapsiblePanel"/> instances.
/// </summary>
public sealed class CollapsiblePanelBuilder : IControlBuilder<CollapsiblePanel>
{
	private readonly CollapsiblePanel _panel = new();

	/// <summary>Sets the header title text.</summary>
	public CollapsiblePanelBuilder WithTitle(string title)
	{
		_panel.Title = title;
		return this;
	}

	/// <summary>Sets the panel margin (left, top, right, bottom).</summary>
	public CollapsiblePanelBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_panel.Margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>Sets a uniform margin on all sides.</summary>
	public CollapsiblePanelBuilder WithMargin(int margin)
	{
		_panel.Margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>Sets the panel margin.</summary>
	public CollapsiblePanelBuilder WithMargin(Margin margin)
	{
		_panel.Margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility
	/// </summary>
	/// <param name="visible">Whether the panel is visible</param>
	/// <returns>The builder for chaining</returns>
	public CollapsiblePanelBuilder Visible(bool visible = true)
	{
		_panel.Visible = visible;
		return this;
	}

	/// <summary>Starts the panel in the collapsed state.</summary>
	public CollapsiblePanelBuilder Collapsed()
	{
		_panel.IsExpanded = false;
		return this;
	}

	/// <summary>Starts the panel in the expanded state.</summary>
	public CollapsiblePanelBuilder Expanded()
	{
		_panel.IsExpanded = true;
		return this;
	}

	/// <summary>Sets the visual style used to render the header.</summary>
	public CollapsiblePanelBuilder WithHeaderStyle(CollapsibleHeaderStyle style)
	{
		_panel.HeaderStyle = style;
		return this;
	}

	/// <summary>Renders the header as a bordered box (title embedded in the top border).</summary>
	public CollapsiblePanelBuilder Bordered()
	{
		_panel.HeaderStyle = CollapsibleHeaderStyle.Bordered;
		return this;
	}

	/// <summary>Renders the header as a bordered box with rounded corners (╭╮╰╯).</summary>
	public CollapsiblePanelBuilder Rounded()
	{
		_panel.HeaderStyle = CollapsibleHeaderStyle.Rounded;
		return this;
	}

	/// <summary>Sets the horizontal alignment of the header content.</summary>
	public CollapsiblePanelBuilder WithHeaderAlignment(HorizontalAlignment alignment)
	{
		_panel.HeaderAlignment = alignment;
		return this;
	}

	/// <summary>Sets the indicator icon shown when the panel is expanded.</summary>
	public CollapsiblePanelBuilder WithExpandedIcon(string icon)
	{
		_panel.ExpandedIcon = icon;
		return this;
	}

	/// <summary>Sets the indicator icon shown when the panel is collapsed.</summary>
	public CollapsiblePanelBuilder WithCollapsedIcon(string icon)
	{
		_panel.CollapsedIcon = icon;
		return this;
	}

	/// <summary>Sets both the expanded and collapsed indicator icons.</summary>
	public CollapsiblePanelBuilder WithIcons(string expanded, string collapsed)
	{
		_panel.ExpandedIcon = expanded;
		_panel.CollapsedIcon = collapsed;
		return this;
	}

	/// <summary>Controls whether a separator is drawn under the header.</summary>
	public CollapsiblePanelBuilder WithHeaderSeparator(bool show = true)
	{
		_panel.ShowHeaderSeparator = show;
		return this;
	}

	/// <summary>Controls whether the panel can be collapsed/expanded by the user (default true).</summary>
	public CollapsiblePanelBuilder Collapsible(bool collapsible = true)
	{
		_panel.Collapsible = collapsible;
		return this;
	}

	/// <summary>Makes the panel a plain, non-collapsible container (locked expanded, pass-through focus).</summary>
	public CollapsiblePanelBuilder NonCollapsible()
	{
		_panel.Collapsible = false;
		return this;
	}

	/// <summary>Controls whether the header row is drawn (default true).</summary>
	public CollapsiblePanelBuilder ShowHeader(bool show = true)
	{
		_panel.ShowHeader = show;
		return this;
	}

	/// <summary>Hides the header row. A collapsible panel always shows its header regardless.</summary>
	public CollapsiblePanelBuilder HideHeader()
	{
		_panel.ShowHeader = false;
		return this;
	}

	/// <summary>Caps the visible body height to the specified number of rows.</summary>
	public CollapsiblePanelBuilder WithMaxContentHeight(int rows)
	{
		_panel.MaxContentHeight = rows;
		return this;
	}

	/// <summary>Sets how expand/collapse transitions are animated.</summary>
	public CollapsiblePanelBuilder WithAnimation(CollapsibleAnimationMode mode)
	{
		_panel.AnimationMode = mode;
		return this;
	}

	/// <summary>Enables height-based expand/collapse animation.</summary>
	public CollapsiblePanelBuilder Animated()
	{
		_panel.AnimationMode = CollapsibleAnimationMode.Height;
		return this;
	}

	/// <summary>Sets the fixed width of the panel.</summary>
	public CollapsiblePanelBuilder WithWidth(int width)
	{
		_panel.Width = width;
		return this;
	}

	/// <summary>Sets the border color.</summary>
	public CollapsiblePanelBuilder WithBorderColor(Color color)
	{
		_panel.BorderColor = color;
		return this;
	}

	/// <summary>Sets the background color.</summary>
	public CollapsiblePanelBuilder WithBackgroundColor(Color color)
	{
		_panel.BackgroundColor = color;
		return this;
	}

	/// <summary>Sets the foreground color.</summary>
	public CollapsiblePanelBuilder WithForegroundColor(Color color)
	{
		_panel.ForegroundColor = color;
		return this;
	}

	/// <summary>Sets the header foreground color used when the panel has keyboard focus.</summary>
	public CollapsiblePanelBuilder WithFocusedForegroundColor(Color color)
	{
		_panel.FocusedForegroundColor = color;
		return this;
	}

	/// <summary>Sets the header background color used when the panel has keyboard focus.</summary>
	public CollapsiblePanelBuilder WithFocusedBackgroundColor(Color color)
	{
		_panel.FocusedBackgroundColor = color;
		return this;
	}

	/// <summary>Sets both the header foreground and background colors used when the panel has keyboard focus.</summary>
	public CollapsiblePanelBuilder WithFocusedColors(Color foreground, Color background)
	{
		_panel.FocusedForegroundColor = foreground;
		_panel.FocusedBackgroundColor = background;
		return this;
	}

	/// <summary>Sets the control's semantic colour role, which tints the panel border/header chrome.</summary>
	/// <param name="role">The semantic role determining the panel's chrome colour.</param>
	/// <param name="mode">Optional <see cref="Themes.ThemeMode"/> override for dark/light role-colour derivation. When null, the active theme's mode is used.</param>
	public CollapsiblePanelBuilder WithColorRole(ColorRole role, ThemeMode? mode = null)
	{
		_panel.ColorRole = role;
		_panel.ColorRoleMode = mode;
		return this;
	}

	/// <summary>Renders the panel in outline style (lighter role chrome).</summary>
	/// <param name="outline">Whether to use outline style.</param>
	public CollapsiblePanelBuilder Outline(bool outline = true)
	{
		_panel.Outline = outline;
		return this;
	}

	/// <summary>Sets the control name for FindControl queries.</summary>
	public CollapsiblePanelBuilder WithName(string name)
	{
		_panel.Name = name;
		return this;
	}

	/// <summary>Adds a child control to the panel body.</summary>
	public CollapsiblePanelBuilder AddControl(IWindowControl control)
	{
		_panel.AddControl(control);
		return this;
	}

	/// <summary>Subscribes a handler to the <see cref="CollapsiblePanel.ExpandedChanged"/> event.</summary>
	public CollapsiblePanelBuilder OnExpandedChanged(EventHandler<bool> handler)
	{
		_panel.ExpandedChanged += handler;
		return this;
	}

	/// <summary>Builds the configured <see cref="CollapsiblePanel"/>.</summary>
	public CollapsiblePanel Build()
	{
		BindingHelper.ApplyDeferredBindings(this, _panel);
		return _panel;
	}

	/// <summary>Implicit conversion to <see cref="CollapsiblePanel"/>.</summary>
	public static implicit operator CollapsiblePanel(CollapsiblePanelBuilder builder) => builder.Build();
}
