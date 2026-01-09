// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for button controls
/// </summary>
public sealed class ButtonBuilder
{
    private string _text = "Button";
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _enabled = true;
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;
    private EventHandler<ButtonControl>? _clickHandler;

    /// <summary>
    /// Sets the button text
    /// </summary>
    /// <param name="text">The button text</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithText(string text)
    {
        _text = text ?? "Button";
        return this;
    }

    /// <summary>
    /// Sets the button alignment
    /// </summary>
    /// <param name="alignment">The alignment</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Centers the button
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Centered()
    {
        _alignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>
    /// Sets the button margin
    /// </summary>
    /// <param name="left">Left margin</param>
    /// <param name="top">Top margin</param>
    /// <param name="right">Right margin</param>
    /// <param name="bottom">Bottom margin</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    /// <param name="margin">The margin value for all sides</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the enabled state
    /// </summary>
    /// <param name="enabled">Whether the button is enabled</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Enabled(bool enabled = true)
    {
        _enabled = enabled;
        return this;
    }

    /// <summary>
    /// Disables the button
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Disabled()
    {
        _enabled = false;
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    /// <param name="visible">Whether the button is visible</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the button width
    /// </summary>
    /// <param name="width">The button width</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    /// <param name="name">The control name</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    /// <param name="tag">The tag object</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the click event handler
    /// </summary>
    /// <param name="handler">The click handler</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder OnClick(EventHandler<ButtonControl> handler)
    {
        _clickHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the click event handler with action
    /// </summary>
    /// <param name="action">The click action</param>
    /// <returns>The builder for chaining</returns>
    public ButtonBuilder OnClick(Action action)
    {
        _clickHandler = (_, _) => action();
        return this;
    }

    /// <summary>
    /// Builds the button control
    /// </summary>
    /// <returns>The configured button control</returns>
    public ButtonControl Build()
    {
        var button = new ButtonControl
        {
            Text = _text,
            HorizontalAlignment = _alignment,
            Margin = _margin,
            IsEnabled = _enabled,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        if (_clickHandler != null)
        {
            button.Click += _clickHandler;
        }

        return button;
    }

    /// <summary>
    /// Implicit conversion to ButtonControl
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <returns>The built button control</returns>
    public static implicit operator ButtonControl(ButtonBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for markup controls
/// </summary>
public sealed class MarkupBuilder
{
    private readonly List<string> _lines = new();
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;

    /// <summary>
    /// Adds a line of markup text
    /// </summary>
    /// <param name="markup">The markup text</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddLine(string markup)
    {
        _lines.Add(markup ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Adds multiple lines of markup text
    /// </summary>
    /// <param name="markupLines">The markup lines</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddLines(params string[] markupLines)
    {
        foreach (var line in markupLines)
        {
            AddLine(line);
        }
        return this;
    }

    /// <summary>
    /// Adds an empty line
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder AddEmptyLine()
    {
        _lines.Add(string.Empty);
        return this;
    }

    /// <summary>
    /// Clears all lines
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Clear()
    {
        _lines.Clear();
        return this;
    }

    /// <summary>
    /// Sets the alignment
    /// </summary>
    /// <param name="alignment">The alignment</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Centers the content
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Centered()
    {
        _alignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    /// <param name="left">Left margin</param>
    /// <param name="top">Top margin</param>
    /// <param name="right">Right margin</param>
    /// <param name="bottom">Bottom margin</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    /// <param name="margin">The margin value for all sides</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    /// <param name="visible">Whether the control is visible</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    /// <param name="width">The control width</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    /// <param name="name">The control name</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    /// <param name="tag">The tag object</param>
    /// <returns>The builder for chaining</returns>
    public MarkupBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Builds the markup control
    /// </summary>
    /// <returns>The configured markup control</returns>
    public MarkupControl Build()
    {
        var markup = new MarkupControl(_lines.ToList())
        {
            HorizontalAlignment = _alignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        return markup;
    }

    /// <summary>
    /// Implicit conversion to MarkupControl
    /// </summary>
    /// <param name="builder">The builder</param>
    /// <returns>The built markup control</returns>
    public static implicit operator MarkupControl(MarkupBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for list controls
/// </summary>
public sealed class ListBuilder
{
    private readonly List<ListItem> _items = new();
    private string _title = "List";
    private int? _maxVisibleItems;
    private bool _isSelectable = true;
    private bool _autoAdjustWidth = false;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;
    private EventHandler<ListItem>? _itemActivatedHandler;
    private EventHandler<int>? _selectionChangedHandler;
    private EventHandler<ListItem?>? _selectedItemChangedHandler;

    /// <summary>
    /// Sets the list title
    /// </summary>
    public ListBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Adds an item to the list
    /// </summary>
    public ListBuilder AddItem(string text, object? tag = null)
    {
        _items.Add(new ListItem(text) { Tag = tag });
        return this;
    }

    /// <summary>
    /// Adds an item with icon to the list
    /// </summary>
    public ListBuilder AddItem(string text, string icon, Color? iconColor = null, object? tag = null)
    {
        _items.Add(new ListItem(text, icon, iconColor) { Tag = tag });
        return this;
    }

    /// <summary>
    /// Adds a ListItem to the list
    /// </summary>
    public ListBuilder AddItem(ListItem item)
    {
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds multiple items to the list
    /// </summary>
    public ListBuilder AddItems(params string[] items)
    {
        foreach (var item in items)
            _items.Add(new ListItem(item));
        return this;
    }

    /// <summary>
    /// Adds multiple ListItems to the list
    /// </summary>
    public ListBuilder AddItems(IEnumerable<ListItem> items)
    {
        _items.AddRange(items);
        return this;
    }

    /// <summary>
    /// Sets the maximum number of visible items
    /// </summary>
    public ListBuilder MaxVisibleItems(int count)
    {
        _maxVisibleItems = count;
        return this;
    }

    /// <summary>
    /// Sets whether items are selectable
    /// </summary>
    public ListBuilder Selectable(bool selectable = true)
    {
        _isSelectable = selectable;
        return this;
    }

    /// <summary>
    /// Enables auto-adjust width based on content
    /// </summary>
    public ListBuilder AutoAdjustWidth(bool autoAdjust = true)
    {
        _autoAdjustWidth = autoAdjust;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment
    /// </summary>
    public ListBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the vertical alignment
    /// </summary>
    public ListBuilder WithVerticalAlignment(VerticalAlignment alignment)
    {
        _verticalAlignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    public ListBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    public ListBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    public ListBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    public ListBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    public ListBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    public ListBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the item activated event handler (Enter key or double-click)
    /// </summary>
    public ListBuilder OnItemActivated(EventHandler<ListItem> handler)
    {
        _itemActivatedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the item activated event handler with simple action
    /// </summary>
    public ListBuilder OnItemActivated(Action<ListItem> handler)
    {
        _itemActivatedHandler = (_, item) => handler(item);
        return this;
    }

    /// <summary>
    /// Sets the selection changed event handler (index-based)
    /// </summary>
    public ListBuilder OnSelectionChanged(EventHandler<int> handler)
    {
        _selectionChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the selection changed event handler with simple action
    /// </summary>
    public ListBuilder OnSelectionChanged(Action<int> handler)
    {
        _selectionChangedHandler = (_, idx) => handler(idx);
        return this;
    }

    /// <summary>
    /// Sets the selected item changed event handler
    /// </summary>
    public ListBuilder OnSelectedItemChanged(EventHandler<ListItem?> handler)
    {
        _selectedItemChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the selected item changed event handler with simple action
    /// </summary>
    public ListBuilder OnSelectedItemChanged(Action<ListItem?> handler)
    {
        _selectedItemChangedHandler = (_, item) => handler(item);
        return this;
    }

    /// <summary>
    /// Builds the list control
    /// </summary>
    public ListControl Build()
    {
        var list = new ListControl(_title)
        {
            MaxVisibleItems = _maxVisibleItems,
            IsSelectable = _isSelectable,
            AutoAdjustWidth = _autoAdjustWidth,
            HorizontalAlignment = _alignment,
            VerticalAlignment = _verticalAlignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        foreach (var item in _items)
            list.AddItem(item);

        if (_itemActivatedHandler != null)
            list.ItemActivated += _itemActivatedHandler;
        if (_selectionChangedHandler != null)
            list.SelectedIndexChanged += _selectionChangedHandler;
        if (_selectedItemChangedHandler != null)
            list.SelectedItemChanged += _selectedItemChangedHandler;

        return list;
    }

    /// <summary>
    /// Implicit conversion to ListControl
    /// </summary>
    public static implicit operator ListControl(ListBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for checkbox controls
/// </summary>
public sealed class CheckboxBuilder
{
    private string _label = "Checkbox";
    private bool _isChecked = false;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;
    private EventHandler<bool>? _checkedChangedHandler;

    /// <summary>
    /// Sets the checkbox label
    /// </summary>
    public CheckboxBuilder WithLabel(string label)
    {
        _label = label;
        return this;
    }

    /// <summary>
    /// Sets the checked state
    /// </summary>
    public CheckboxBuilder Checked(bool isChecked = true)
    {
        _isChecked = isChecked;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment
    /// </summary>
    public CheckboxBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    public CheckboxBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    public CheckboxBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    public CheckboxBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    public CheckboxBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    public CheckboxBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    public CheckboxBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the checked changed event handler
    /// </summary>
    public CheckboxBuilder OnCheckedChanged(EventHandler<bool> handler)
    {
        _checkedChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the checked changed event handler with simple action
    /// </summary>
    public CheckboxBuilder OnCheckedChanged(Action<bool> handler)
    {
        _checkedChangedHandler = (_, isChecked) => handler(isChecked);
        return this;
    }

    /// <summary>
    /// Builds the checkbox control
    /// </summary>
    public CheckboxControl Build()
    {
        var checkbox = new CheckboxControl(_label, _isChecked)
        {
            HorizontalAlignment = _alignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        if (_checkedChangedHandler != null)
            checkbox.CheckedChanged += _checkedChangedHandler;

        return checkbox;
    }

    /// <summary>
    /// Implicit conversion to CheckboxControl
    /// </summary>
    public static implicit operator CheckboxControl(CheckboxBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for dropdown controls
/// </summary>
public sealed class DropdownBuilder
{
    private readonly List<DropdownItem> _items = new();
    private string _prompt = "Select...";
    private int _selectedIndex = -1;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;
    private EventHandler<int>? _selectionChangedHandler;
    private EventHandler<DropdownItem?>? _selectedItemChangedHandler;

    /// <summary>
    /// Sets the dropdown prompt text
    /// </summary>
    public DropdownBuilder WithPrompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    /// <summary>
    /// Adds an item to the dropdown
    /// </summary>
    public DropdownBuilder AddItem(string text, string? value = null, Color? color = null)
    {
        _items.Add(new DropdownItem(text, value ?? text, color));
        return this;
    }

    /// <summary>
    /// Adds a DropdownItem to the dropdown
    /// </summary>
    public DropdownBuilder AddItem(DropdownItem item)
    {
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds multiple items to the dropdown
    /// </summary>
    public DropdownBuilder AddItems(params string[] items)
    {
        foreach (var item in items)
            _items.Add(new DropdownItem(item, item, null));
        return this;
    }

    /// <summary>
    /// Adds multiple DropdownItems to the dropdown
    /// </summary>
    public DropdownBuilder AddItems(IEnumerable<DropdownItem> items)
    {
        _items.AddRange(items);
        return this;
    }

    /// <summary>
    /// Sets the initially selected index
    /// </summary>
    public DropdownBuilder SelectedIndex(int index)
    {
        _selectedIndex = index;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment
    /// </summary>
    public DropdownBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    public DropdownBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    public DropdownBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    public DropdownBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    public DropdownBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    public DropdownBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    public DropdownBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the selection changed event handler (index-based)
    /// </summary>
    public DropdownBuilder OnSelectionChanged(EventHandler<int> handler)
    {
        _selectionChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the selection changed event handler with simple action
    /// </summary>
    public DropdownBuilder OnSelectionChanged(Action<int> handler)
    {
        _selectionChangedHandler = (_, idx) => handler(idx);
        return this;
    }

    /// <summary>
    /// Sets the selected item changed event handler
    /// </summary>
    public DropdownBuilder OnSelectedItemChanged(EventHandler<DropdownItem?> handler)
    {
        _selectedItemChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the selected item changed event handler with simple action
    /// </summary>
    public DropdownBuilder OnSelectedItemChanged(Action<DropdownItem?> handler)
    {
        _selectedItemChangedHandler = (_, item) => handler(item);
        return this;
    }

    /// <summary>
    /// Builds the dropdown control
    /// </summary>
    public DropdownControl Build()
    {
        var dropdown = new DropdownControl(_prompt)
        {
            HorizontalAlignment = _alignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        foreach (var item in _items)
            dropdown.AddItem(item);

        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            dropdown.SelectedIndex = _selectedIndex;

        if (_selectionChangedHandler != null)
            dropdown.SelectedIndexChanged += _selectionChangedHandler;
        if (_selectedItemChangedHandler != null)
            dropdown.SelectedItemChanged += _selectedItemChangedHandler;

        return dropdown;
    }

    /// <summary>
    /// Implicit conversion to DropdownControl
    /// </summary>
    public static implicit operator DropdownControl(DropdownBuilder builder) => builder.Build();
}

/// <summary>
/// Fluent builder for prompt controls
/// </summary>
public sealed class PromptBuilder
{
    private string _prompt = "> ";
    private string _initialInput = "";
    private bool _unfocusOnEnter = true;
    private HorizontalAlignment _alignment = HorizontalAlignment.Left;
    private Margin _margin = new(0, 0, 0, 0);
    private bool _visible = true;
    private int? _width;
    private string? _name;
    private object? _tag;
    private EventHandler<string>? _enteredHandler;
    private EventHandler<string>? _inputChangedHandler;

    /// <summary>
    /// Sets the prompt text (displayed before the input area)
    /// </summary>
    public PromptBuilder WithPrompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    /// <summary>
    /// Sets the initial input value
    /// </summary>
    public PromptBuilder WithInput(string input)
    {
        _initialInput = input;
        return this;
    }

    /// <summary>
    /// Sets whether the control loses focus when Enter is pressed
    /// </summary>
    public PromptBuilder UnfocusOnEnter(bool unfocus = true)
    {
        _unfocusOnEnter = unfocus;
        return this;
    }

    /// <summary>
    /// Sets the horizontal alignment
    /// </summary>
    public PromptBuilder WithAlignment(HorizontalAlignment alignment)
    {
        _alignment = alignment;
        return this;
    }

    /// <summary>
    /// Sets the margin
    /// </summary>
    public PromptBuilder WithMargin(int left, int top, int right, int bottom)
    {
        _margin = new Margin(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// Sets uniform margin
    /// </summary>
    public PromptBuilder WithMargin(int margin)
    {
        _margin = new Margin(margin, margin, margin, margin);
        return this;
    }

    /// <summary>
    /// Sets the visibility
    /// </summary>
    public PromptBuilder Visible(bool visible = true)
    {
        _visible = visible;
        return this;
    }

    /// <summary>
    /// Sets the width
    /// </summary>
    public PromptBuilder WithWidth(int width)
    {
        _width = width;
        return this;
    }

    /// <summary>
    /// Sets the control name for lookup
    /// </summary>
    public PromptBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets a tag object
    /// </summary>
    public PromptBuilder WithTag(object tag)
    {
        _tag = tag;
        return this;
    }

    /// <summary>
    /// Sets the entered event handler (fired when Enter is pressed)
    /// </summary>
    public PromptBuilder OnEntered(EventHandler<string> handler)
    {
        _enteredHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the entered event handler with simple action
    /// </summary>
    public PromptBuilder OnEntered(Action<string> handler)
    {
        _enteredHandler = (_, text) => handler(text);
        return this;
    }

    /// <summary>
    /// Sets the input changed event handler (fired when text changes)
    /// </summary>
    public PromptBuilder OnInputChanged(EventHandler<string> handler)
    {
        _inputChangedHandler = handler;
        return this;
    }

    /// <summary>
    /// Sets the input changed event handler with simple action
    /// </summary>
    public PromptBuilder OnInputChanged(Action<string> handler)
    {
        _inputChangedHandler = (_, text) => handler(text);
        return this;
    }

    /// <summary>
    /// Builds the prompt control
    /// </summary>
    public PromptControl Build()
    {
        var prompt = new PromptControl
        {
            Prompt = _prompt,
            UnfocusOnEnter = _unfocusOnEnter,
            HorizontalAlignment = _alignment,
            Margin = _margin,
            Visible = _visible,
            Width = _width,
            Name = _name,
            Tag = _tag
        };

        if (!string.IsNullOrEmpty(_initialInput))
            prompt.SetInput(_initialInput);

        if (_enteredHandler != null)
            prompt.Entered += _enteredHandler;
        if (_inputChangedHandler != null)
            prompt.InputChanged += _inputChangedHandler;

        return prompt;
    }

    /// <summary>
    /// Implicit conversion to PromptControl
    /// </summary>
    public static implicit operator PromptControl(PromptBuilder builder) => builder.Build();
}

/// <summary>
/// Static factory class for creating control builders
/// </summary>
public static class Controls
{
    /// <summary>
    /// Creates a new button builder
    /// </summary>
    /// <param name="text">The initial button text</param>
    /// <returns>A new button builder</returns>
    public static ButtonBuilder Button(string text = "Button") => new ButtonBuilder().WithText(text);

    /// <summary>
    /// Creates a new markup builder
    /// </summary>
    /// <param name="initialLine">The initial line of markup</param>
    /// <returns>A new markup builder</returns>
    public static MarkupBuilder Markup(string? initialLine = null)
    {
        var builder = new MarkupBuilder();
        if (initialLine != null)
        {
            builder.AddLine(initialLine);
        }
        return builder;
    }

    /// <summary>
    /// Creates a new rule control
    /// </summary>
    /// <param name="title">The rule title</param>
    /// <returns>A configured rule control</returns>
    public static RuleControl Rule(string? title = null)
    {
        var rule = new RuleControl();
        if (title != null)
        {
            rule.Title = title;
        }
        return rule;
    }

    /// <summary>
    /// Creates a horizontal separator rule
    /// </summary>
    /// <returns>A configured rule control</returns>
    public static RuleControl Separator() => new RuleControl { Title = string.Empty };

    /// <summary>
    /// Creates a text label (markup without formatting)
    /// </summary>
    /// <param name="text">The label text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Label(string text) => new MarkupControl(new List<string> { text });

    /// <summary>
    /// Creates a header text (bold and colored)
    /// </summary>
    /// <param name="text">The header text</param>
    /// <param name="color">The header color (default: yellow)</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Header(string text, string color = "yellow") =>
        new MarkupControl(new List<string> { $"[bold {color}]{text}[/]" });

    /// <summary>
    /// Creates an info message (blue color)
    /// </summary>
    /// <param name="text">The info text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Info(string text) =>
        new MarkupControl(new List<string> { $"[blue]{text}[/]" });

    /// <summary>
    /// Creates a warning message (orange color)
    /// </summary>
    /// <param name="text">The warning text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Warning(string text) =>
        new MarkupControl(new List<string> { $"[orange3]{text}[/]" });

    /// <summary>
    /// Creates an error message (red color)
    /// </summary>
    /// <param name="text">The error text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Error(string text) =>
        new MarkupControl(new List<string> { $"[red]{text}[/]" });

    /// <summary>
    /// Creates a success message (green color)
    /// </summary>
    /// <param name="text">The success text</param>
    /// <returns>A configured markup control</returns>
    public static MarkupControl Success(string text) =>
        new MarkupControl(new List<string> { $"[green]{text}[/]" });

    /// <summary>
    /// Creates a vertical separator control
    /// </summary>
    /// <returns>A configured separator control</returns>
    public static SeparatorControl VerticalSeparator() => new SeparatorControl();

    /// <summary>
    /// Creates a vertical separator control with horizontal margin
    /// </summary>
    /// <param name="horizontalMargin">The margin on left and right sides</param>
    /// <returns>A configured separator control</returns>
    public static SeparatorControl VerticalSeparator(int horizontalMargin) =>
        new SeparatorControl { Margin = new Margin(horizontalMargin, 0, horizontalMargin, 0) };

    /// <summary>
    /// Creates a new toolbar builder
    /// </summary>
    /// <returns>A new toolbar builder</returns>
    public static ToolbarBuilder Toolbar() => new ToolbarBuilder();

    /// <summary>
    /// Creates a new list builder
    /// </summary>
    /// <param name="title">The initial list title</param>
    /// <returns>A new list builder</returns>
    public static ListBuilder List(string? title = null)
    {
        var builder = new ListBuilder();
        if (title != null)
            builder.WithTitle(title);
        return builder;
    }

    /// <summary>
    /// Creates a new checkbox builder
    /// </summary>
    /// <param name="label">The checkbox label</param>
    /// <returns>A new checkbox builder</returns>
    public static CheckboxBuilder Checkbox(string label) => new CheckboxBuilder().WithLabel(label);

    /// <summary>
    /// Creates a new dropdown builder
    /// </summary>
    /// <param name="prompt">The dropdown prompt text</param>
    /// <returns>A new dropdown builder</returns>
    public static DropdownBuilder Dropdown(string? prompt = null)
    {
        var builder = new DropdownBuilder();
        if (prompt != null)
            builder.WithPrompt(prompt);
        return builder;
    }

    /// <summary>
    /// Creates a new prompt builder
    /// </summary>
    /// <param name="prompt">The prompt text</param>
    /// <returns>A new prompt builder</returns>
    public static PromptBuilder Prompt(string prompt = "> ") => new PromptBuilder().WithPrompt(prompt);
}