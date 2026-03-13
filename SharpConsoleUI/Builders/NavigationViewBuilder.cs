// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for NavigationView controls.
/// </summary>
public sealed class NavigationViewBuilder : IControlBuilder<NavigationView>
{
	private readonly NavigationView _control = new();
	private readonly List<(NavigationItem item, Action<ScrollablePanelControl>? content)> _pendingItems = new();
	private int _initialSelectedIndex = 0;

	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;

	#region Item Methods

	/// <summary>
	/// Adds a navigation item with optional content factory.
	/// </summary>
	public NavigationViewBuilder AddItem(string text, string? icon = null,
		string? subtitle = null, Action<ScrollablePanelControl>? content = null)
	{
		_pendingItems.Add((new NavigationItem(text, icon, subtitle), content));
		return this;
	}

	/// <summary>
	/// Adds a navigation item with optional content factory.
	/// </summary>
	public NavigationViewBuilder AddItem(NavigationItem item, Action<ScrollablePanelControl>? content = null)
	{
		_pendingItems.Add((item, content));
		return this;
	}

	/// <summary>
	/// Sets the initially selected item index.
	/// </summary>
	public NavigationViewBuilder WithSelectedIndex(int index)
	{
		_initialSelectedIndex = index;
		return this;
	}

	#endregion

	#region Nav Pane Config

	/// <summary>
	/// Sets the width of the navigation pane.
	/// </summary>
	public NavigationViewBuilder WithNavWidth(int width)
	{
		_control.NavPaneWidth = width;
		return this;
	}

	/// <summary>
	/// Sets the pane header markup text.
	/// </summary>
	public NavigationViewBuilder WithPaneHeader(string markup)
	{
		_control.PaneHeader = markup;
		return this;
	}

	/// <summary>
	/// Sets the colors for the selected navigation item.
	/// </summary>
	public NavigationViewBuilder WithSelectedColors(Color foreground, Color background)
	{
		_control.SelectedItemForeground = foreground;
		_control.SelectedItemBackground = background;
		return this;
	}

	/// <summary>
	/// Sets the selection indicator character.
	/// </summary>
	public NavigationViewBuilder WithSelectionIndicator(char indicator)
	{
		_control.SelectionIndicator = indicator;
		return this;
	}

	#endregion

	#region Content Pane Config

	/// <summary>
	/// Sets the content panel border style.
	/// </summary>
	public NavigationViewBuilder WithContentBorder(BorderStyle style)
	{
		_control.ContentBorderStyle = style;
		return this;
	}

	/// <summary>
	/// Sets the content panel border color.
	/// </summary>
	public NavigationViewBuilder WithContentBorderColor(Color color)
	{
		_control.ContentBorderColor = color;
		return this;
	}

	/// <summary>
	/// Sets the content panel background color.
	/// </summary>
	public NavigationViewBuilder WithContentBackground(Color color)
	{
		_control.ContentBackgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the content panel padding.
	/// </summary>
	public NavigationViewBuilder WithContentPadding(int left, int top, int right, int bottom)
	{
		_control.ContentPadding = new Padding(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets whether to show the content header.
	/// </summary>
	public NavigationViewBuilder WithContentHeader(bool show)
	{
		_control.ShowContentHeader = show;
		return this;
	}

	#endregion

	#region Events

	/// <summary>
	/// Attaches a handler for the SelectedItemChanged event.
	/// </summary>
	public NavigationViewBuilder OnSelectedItemChanged(EventHandler<NavigationItemChangedEventArgs> handler)
	{
		_control.SelectedItemChanged += handler;
		return this;
	}

	/// <summary>
	/// Attaches a handler for the SelectedItemChanging event.
	/// </summary>
	public NavigationViewBuilder OnSelectedItemChanging(EventHandler<NavigationItemChangingEventArgs> handler)
	{
		_control.SelectedItemChanging += handler;
		return this;
	}

	#endregion

	#region Standard Properties

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public NavigationViewBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public NavigationViewBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Makes the control fill the available vertical space.
	/// </summary>
	public NavigationViewBuilder Fill()
	{
		_verticalAlignment = VerticalAlignment.Fill;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public NavigationViewBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public NavigationViewBuilder WithMargin(Margin margin)
	{
		_margin = margin;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public NavigationViewBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name.
	/// </summary>
	public NavigationViewBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public NavigationViewBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public NavigationViewBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	public NavigationViewBuilder WithWidth(int width)
	{
		_control.Width = width;
		return this;
	}

	#endregion

	/// <summary>
	/// Builds the NavigationView control.
	/// </summary>
	public NavigationView Build()
	{
		_control.HorizontalAlignment = _alignment;
		_control.VerticalAlignment = _verticalAlignment;
		_control.Margin = _margin;
		_control.Visible = _visible;
		_control.Name = _name;
		_control.Tag = _tag;
		_control.StickyPosition = _stickyPosition;

		// Add items with content
		foreach (var (item, content) in _pendingItems)
		{
			_control.AddItem(item);
			if (content != null)
				_control.SetItemContent(item, content);
		}

		// Set initial selection
		if (_initialSelectedIndex >= 0 && _initialSelectedIndex < _pendingItems.Count)
		{
			_control.SelectedIndex = _initialSelectedIndex;
		}

		BindingHelper.ApplyDeferredBindings(this, _control);
		return _control;
	}

	/// <summary>
	/// Implicit conversion to NavigationView.
	/// </summary>
	public static implicit operator NavigationView(NavigationViewBuilder builder) => builder.Build();
}
