namespace SharpConsoleUI.Panel;

/// <summary>
/// A single-row desktop bar with three zones (left, center, right).
/// Manages element lifecycle, layout, rendering, and mouse routing.
/// </summary>
public class Panel
{
    private readonly List<IPanelElement> _left = new();
    private readonly List<IPanelElement> _center = new();
    private readonly List<IPanelElement> _right = new();
    private List<(IPanelElement element, int x, int width)> _renderedLayout = new();

    private bool _visible = true;

    /// <summary>
    /// Gets or sets whether this panel is visible.
    /// Changing visibility triggers a full screen redraw to avoid ghost artifacts.
    /// </summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                IsDirty = true;
                // Desktop bounds change when visibility toggles — full screen clear needed
                WindowSystem?.ForceFullRedraw();
            }
        }
    }

    /// <summary>
    /// Gets or sets the panel background color. Falls back to theme if null.
    /// </summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the panel foreground color. Falls back to theme if null.
    /// </summary>
    public Color? ForegroundColor { get; set; }

    /// <summary>
    /// Gets whether this panel needs to be redrawn.
    /// </summary>
    public bool IsDirty { get; private set; } = true;

    /// <summary>
    /// Gets or sets the console window system this panel belongs to.
    /// Set by ConsoleWindowSystem when the panel is configured.
    /// </summary>
    internal ConsoleWindowSystem? WindowSystem { get; set; }

    /// <summary>
    /// Gets the height of this panel (1 if visible, 0 if hidden).
    /// </summary>
    public int Height => Visible ? 1 : 0;

    #region Element Management

    /// <summary>
    /// Adds elements to the left zone.
    /// </summary>
    /// <param name="elements">Elements to add.</param>
    public void AddLeft(params IPanelElement[] elements)
    {
        foreach (var element in elements)
        {
            _left.Add(element);
            element.OnAttached(this);
        }
        IsDirty = true;
    }

    /// <summary>
    /// Adds elements to the center zone.
    /// </summary>
    /// <param name="elements">Elements to add.</param>
    public void AddCenter(params IPanelElement[] elements)
    {
        foreach (var element in elements)
        {
            _center.Add(element);
            element.OnAttached(this);
        }
        IsDirty = true;
    }

    /// <summary>
    /// Adds elements to the right zone.
    /// </summary>
    /// <param name="elements">Elements to add.</param>
    public void AddRight(params IPanelElement[] elements)
    {
        foreach (var element in elements)
        {
            _right.Add(element);
            element.OnAttached(this);
        }
        IsDirty = true;
    }

    /// <summary>
    /// Removes an element by name from any zone.
    /// </summary>
    /// <param name="name">The name of the element to remove.</param>
    /// <returns>True if the element was found and removed.</returns>
    public bool Remove(string name)
    {
        return RemoveFrom(_left, name) || RemoveFrom(_center, name) || RemoveFrom(_right, name);
    }

    /// <summary>
    /// Removes a specific element from any zone.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <returns>True if the element was found and removed.</returns>
    public bool Remove(IPanelElement element)
    {
        bool removed = _left.Remove(element) || _center.Remove(element) || _right.Remove(element);
        if (removed)
        {
            element.OnDetached();
            (element as IDisposable)?.Dispose();
            IsDirty = true;
        }
        return removed;
    }

    /// <summary>
    /// Finds an element by name and casts to the specified type.
    /// </summary>
    /// <typeparam name="T">The expected element type.</typeparam>
    /// <param name="name">The element name.</param>
    /// <returns>The element, or null if not found or wrong type.</returns>
    public T? FindElement<T>(string name) where T : class, IPanelElement
    {
        return FindInList<T>(_left, name)
            ?? FindInList<T>(_center, name)
            ?? FindInList<T>(_right, name);
    }

    /// <summary>
    /// Gets the rendered bounds (x, width) of the first element of the specified type.
    /// Returns null if no such element exists or has not been rendered yet.
    /// </summary>
    /// <typeparam name="T">The element type to find.</typeparam>
    /// <returns>The x position and width of the element, or null.</returns>
    public (int x, int width)? GetElementBounds<T>() where T : class, IPanelElement
    {
        foreach (var (element, x, width) in _renderedLayout)
        {
            if (element is T)
                return (x, width);
        }
        return null;
    }

    /// <summary>
    /// Returns true if any zone contains an element of the specified type.
    /// </summary>
    /// <typeparam name="T">The element type to check for.</typeparam>
    /// <returns>True if at least one element of type T exists.</returns>
    public bool HasElement<T>() where T : class, IPanelElement
    {
        return _left.OfType<T>().Any()
            || _center.OfType<T>().Any()
            || _right.OfType<T>().Any();
    }

    /// <summary>
    /// Returns all elements of the specified type across all zones.
    /// </summary>
    /// <typeparam name="T">The element type to find.</typeparam>
    /// <returns>All matching elements.</returns>
    public IEnumerable<T> FindAllElements<T>() where T : class, IPanelElement
    {
        foreach (var e in _left) if (e is T t) yield return t;
        foreach (var e in _center) if (e is T t) yield return t;
        foreach (var e in _right) if (e is T t) yield return t;
    }

    /// <summary>
    /// Clears all elements from all zones.
    /// </summary>
    public void ClearAll()
    {
        ClearList(_left);
        ClearList(_center);
        ClearList(_right);
    }

    /// <summary>
    /// Clears all elements from the left zone.
    /// </summary>
    public void ClearLeft() => ClearList(_left);

    /// <summary>
    /// Clears all elements from the center zone.
    /// </summary>
    public void ClearCenter() => ClearList(_center);

    /// <summary>
    /// Clears all elements from the right zone.
    /// </summary>
    public void ClearRight() => ClearList(_right);

    /// <summary>
    /// Marks this panel as needing to be redrawn. Called by elements via Invalidate().
    /// </summary>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    #endregion

    #region Layout Engine

    /// <summary>
    /// Renders this panel into the character buffer at the specified row.
    /// </summary>
    /// <param name="buffer">The character buffer to render into.</param>
    /// <param name="y">The row to render at.</param>
    /// <param name="panelWidth">The total width of the panel.</param>
    /// <param name="themeFg">The theme foreground color (used if panel has no color set).</param>
    /// <param name="themeBg">The theme background color (used if panel has no color set).</param>
    public void Render(Layout.CharacterBuffer buffer, int y, int panelWidth, Color themeFg, Color themeBg)
    {
        var fg = ForegroundColor ?? themeFg;
        var bg = BackgroundColor ?? themeBg;

        // Fill entire row with background
        for (int x = 0; x < panelWidth; x++)
            buffer.SetNarrowCell(x, y, ' ', fg, bg);

        // Collect visible elements per zone
        var leftVisible = GetVisible(_left);
        var centerVisible = GetVisible(_center);
        var rightVisible = GetVisible(_right);

        // Calculate layout
        var layout = CalculateLayout(leftVisible, centerVisible, rightVisible, panelWidth);

        // Render each element and store positions for mouse hit testing
        _renderedLayout.Clear();
        foreach (var (element, x, width) in layout)
        {
            if (width > 0)
            {
                element.Render(buffer, x, y, width, fg, bg);
                _renderedLayout.Add((element, x, width));
            }
        }

        IsDirty = false;
    }

    /// <summary>
    /// Routes a mouse event to the element at the specified x position.
    /// </summary>
    /// <param name="args">The mouse event arguments.</param>
    /// <returns>True if an element handled the event.</returns>
    public bool ProcessMouseEvent(Events.MouseEventArgs args)
    {
        int mouseX = args.Position.X;
        foreach (var (element, x, width) in _renderedLayout)
        {
            if (mouseX >= x && mouseX < x + width)
            {
                return element.ProcessMouseEvent(args, x, width);
            }
        }
        return false;
    }

    /// <summary>
    /// Creates a new PanelBuilder for fluent panel construction.
    /// </summary>
    /// <returns>A new PanelBuilder instance.</returns>
    public static PanelBuilder Builder() => new();

    #endregion

    #region Private Helpers

    private static List<IPanelElement> GetVisible(List<IPanelElement> elements)
    {
        var result = new List<IPanelElement>();
        foreach (var e in elements)
        {
            if (e.Visible)
                result.Add(e);
        }
        return result;
    }

    private static List<(IPanelElement element, int x, int width)> CalculateLayout(
        List<IPanelElement> leftVisible,
        List<IPanelElement> centerVisible,
        List<IPanelElement> rightVisible,
        int panelWidth)
    {
        var result = new List<(IPanelElement element, int x, int width)>();

        // Measure fixed widths for each zone
        // Build zone measurements
        var leftEntries = new List<(IPanelElement element, int measuredWidth, bool isFlex)>();
        var centerEntries = new List<(IPanelElement element, int measuredWidth, bool isFlex)>();
        var rightEntries = new List<(IPanelElement element, int measuredWidth, bool isFlex)>();

        foreach (var e in leftVisible)
        {
            bool isFlex = e.FlexGrow > 0;
            int measured = isFlex ? 0 : (e.FixedWidth ?? e.MeasureWidth());
            leftEntries.Add((e, measured, isFlex));
        }
        foreach (var e in centerVisible)
        {
            bool isFlex = e.FlexGrow > 0;
            int measured = isFlex ? 0 : (e.FixedWidth ?? e.MeasureWidth());
            centerEntries.Add((e, measured, isFlex));
        }
        foreach (var e in rightVisible)
        {
            bool isFlex = e.FlexGrow > 0;
            int measured = isFlex ? 0 : (e.FixedWidth ?? e.MeasureWidth());
            rightEntries.Add((e, measured, isFlex));
        }

        // Calculate total fixed widths
        int leftFixed = SumFixed(leftEntries);
        int centerFixed = SumFixed(centerEntries);
        int rightFixed = SumFixed(rightEntries);
        int totalFixed = leftFixed + centerFixed + rightFixed;

        // Remaining width for flex elements
        int remaining = Math.Max(0, panelWidth - totalFixed);

        // Collect all flex elements with their grow factors
        var flexElements = new List<(IPanelElement element, int flexGrow)>();
        foreach (var (e, _, isFlex) in leftEntries)
            if (isFlex) flexElements.Add((e, e.FlexGrow));
        foreach (var (e, _, isFlex) in centerEntries)
            if (isFlex) flexElements.Add((e, e.FlexGrow));
        foreach (var (e, _, isFlex) in rightEntries)
            if (isFlex) flexElements.Add((e, e.FlexGrow));

        // Distribute remaining width to flex elements with min/max redistribution
        var flexWidths = DistributeFlexWidths(flexElements, remaining);

        // Layout zones: left from x=0, right from x=panelWidth-rightWidth, center in between
        int GetElementWidth((IPanelElement element, int measuredWidth, bool isFlex) entry)
        {
            if (entry.isFlex)
                return flexWidths.GetValueOrDefault(entry.element, 0);
            return entry.measuredWidth;
        }

        // Left zone: starts at 0
        int x = 0;
        foreach (var entry in leftEntries)
        {
            int w = GetElementWidth(entry);
            result.Add((entry.element, x, w));
            x += w;
        }

        // Right zone: measure total width, start from right edge
        int rightTotal = 0;
        foreach (var entry in rightEntries)
            rightTotal += GetElementWidth(entry);

        int rightStart = panelWidth - rightTotal;

        // Center zone: centered between left end and right start
        int centerStart = x;
        int centerAvailable = rightStart - centerStart;
        int centerTotal = 0;
        foreach (var entry in centerEntries)
            centerTotal += GetElementWidth(entry);

        int centerOffset = Math.Max(0, (centerAvailable - centerTotal) / 2);
        int centerX = centerStart + centerOffset;
        foreach (var entry in centerEntries)
        {
            int w = GetElementWidth(entry);
            result.Add((entry.element, centerX, w));
            centerX += w;
        }

        // Right zone
        int rx = rightStart;
        foreach (var entry in rightEntries)
        {
            int w = GetElementWidth(entry);
            result.Add((entry.element, rx, w));
            rx += w;
        }

        return result;
    }

    private static Dictionary<IPanelElement, int> DistributeFlexWidths(
        List<(IPanelElement element, int flexGrow)> flexElements, int remaining)
    {
        var widths = new Dictionary<IPanelElement, int>();
        if (flexElements.Count == 0 || remaining <= 0)
            return widths;

        var active = new List<(IPanelElement element, int flexGrow)>(flexElements);
        int budget = remaining;

        // Multi-pass: redistribute excess/deficit from clamped elements
        for (int pass = 0; pass < 3 && active.Count > 0; pass++)
        {
            int totalGrow = 0;
            foreach (var (_, grow) in active)
                totalGrow += grow;
            if (totalGrow <= 0) break;

            bool anyClamped = false;
            var clampedSet = new HashSet<IPanelElement>();
            int distributed = 0;

            for (int i = 0; i < active.Count; i++)
            {
                var (element, grow) = active[i];
                int w = (i == active.Count - 1) ? budget - distributed : budget * grow / totalGrow;

                int clamped = w;
                if (element.MinWidth.HasValue)
                    clamped = Math.Max(clamped, element.MinWidth.Value);
                if (element.MaxWidth.HasValue)
                    clamped = Math.Min(clamped, element.MaxWidth.Value);

                if (clamped != w)
                {
                    anyClamped = true;
                    clampedSet.Add(element);
                }

                widths[element] = clamped;
                distributed += clamped;
            }

            if (!anyClamped) break;

            // Recalculate budget excluding clamped elements
            int clampedTotal = 0;
            foreach (var kv in widths)
                if (clampedSet.Contains(kv.Key))
                    clampedTotal += kv.Value;

            budget = remaining - clampedTotal;
            active.RemoveAll(e => clampedSet.Contains(e.element));
        }

        return widths;
    }

    private static int SumFixed(List<(IPanelElement element, int measuredWidth, bool isFlex)> entries)
    {
        int sum = 0;
        foreach (var (_, width, isFlex) in entries)
            if (!isFlex) sum += width;
        return sum;
    }

    private bool RemoveFrom(List<IPanelElement> list, string name)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Name == name)
            {
                var element = list[i];
                element.OnDetached();
                (element as IDisposable)?.Dispose();
                list.RemoveAt(i);
                IsDirty = true;
                return true;
            }
        }
        return false;
    }

    private static T? FindInList<T>(List<IPanelElement> list, string name) where T : class, IPanelElement
    {
        foreach (var e in list)
        {
            if (e.Name == name && e is T typed)
                return typed;
        }
        return null;
    }

    private void ClearList(List<IPanelElement> list)
    {
        foreach (var e in list)
        {
            e.OnDetached();
            (e as IDisposable)?.Dispose();
        }
        list.Clear();
        IsDirty = true;
    }

    #endregion
}
