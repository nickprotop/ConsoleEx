// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A container control that holds child controls vertically within a column of a <see cref="HorizontalGridControl"/>.
	/// Supports layout constraints, focus management, and dynamic content sizing.
	/// </summary>
	public class ColumnContainer : IContainer, IInteractiveControl, IFocusableControl, IMouseAwareControl, ILayoutAware, IDOMPaintable, IContainerControl
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Fill;
		private Color? _backgroundColorValue;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private IContainer? _container;
		private List<IWindowControl> _contents = new List<IWindowControl>();
		private Color? _foregroundColorValue;
		private bool _hasFocus;
		private HorizontalGridControl _horizontalGridContent;
		private bool _isDirty;
		private bool _isEnabled = true;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Double-click detection
		private DateTime _lastClickTime = DateTime.MinValue;
		private Point _lastClickPosition = Point.Empty;
		private int _doubleClickThresholdMs = Configuration.ControlDefaults.DefaultDoubleClickThresholdMs;
		private bool _doubleClickEnabled = true;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="ColumnContainer"/> class.
		/// </summary>
		/// <param name="horizontalGridContent">The parent horizontal grid that contains this column.</param>
		public ColumnContainer(HorizontalGridControl horizontalGridContent)
		{
			_horizontalGridContent = horizontalGridContent;
			_consoleWindowSystem = horizontalGridContent.Container?.GetConsoleWindowSystem;
		}

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			// Inherit background color from parent HorizontalGridControl's container (the Window),
			// then fall back to theme
			get => _backgroundColorValue ?? _horizontalGridContent?.Container?.BackgroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set { _backgroundColorValue = value; Container?.Invalidate(true); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			// Inherit foreground color from parent HorizontalGridControl's container (the Window),
			// then fall back to theme
			get => _foregroundColorValue ?? _horizontalGridContent?.Container?.ForegroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set { _foregroundColorValue = value; Container?.Invalidate(true); }
		}

		private bool _propagatingWindowSystem = false;

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem
		{
			get => _consoleWindowSystem;
			set
			{
				// Avoid redundant work if value unchanged
				if (_consoleWindowSystem == value)
					return;

				_consoleWindowSystem = value;

				// Propagate to nested HorizontalGridControls if not already propagating (prevents re-entrancy)
				if (!_propagatingWindowSystem)
				{
					_propagatingWindowSystem = true;
					try
					{
						foreach (IWindowControl control in _contents)
						{
							if (control is HorizontalGridControl nestedGrid)
							{
								// Update nested grid's columns
								foreach (var column in nestedGrid.Columns)
								{
									column.GetConsoleWindowSystem = value;
								}
							}
						}
					}
					finally
					{
						_propagatingWindowSystem = false;
					}
				}

				// Invalidate contents
				foreach (IWindowControl control in _contents)
				{
					control.Invalidate();
				}
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the parent horizontal grid control.
		/// </summary>
		public HorizontalGridControl HorizontalGridContent
		{
			get => _horizontalGridContent;
			set
			{
				_horizontalGridContent = value;
				_consoleWindowSystem = value.Container?.GetConsoleWindowSystem;

				_horizontalGridContent.Invalidate();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this column needs to be re-rendered.
		/// </summary>
		public bool IsDirty
		{
			get => _isDirty;
			set
			{
				_isDirty = value;
			}
		}

		/// <inheritdoc/>
	public int? Width
	{
		get => _width;
		set
		{
			var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
			if (_width != validatedValue)
			{
				_width = validatedValue;
				Invalidate(true);
			}
		}
	}

		/// <summary>
		/// Gets or sets whether double-click events are enabled for white space clicks.
		/// Default: true.
		/// </summary>
		public bool DoubleClickEnabled
		{
			get => _doubleClickEnabled;
			set => _doubleClickEnabled = value;
		}

		/// <summary>
		/// Gets or sets the double-click threshold in milliseconds.
		/// Two clicks within this time window are considered a double-click.
		/// Default: 500ms, minimum: 100ms.
		/// </summary>
		public int DoubleClickThresholdMs
		{
			get => _doubleClickThresholdMs;
			set => _doubleClickThresholdMs = Math.Max(100, value);
		}

		private int? _minWidth;
		private int? _maxWidth;
		private double _flexFactor = 1.0;

		/// <summary>
		/// Gets or sets the minimum width constraint for flexible columns. Null means no minimum, defaults to 1 during layout.
		/// </summary>
		public int? MinWidth
		{
			get => _minWidth;
			set
			{
				if (_minWidth != value)
				{
					_minWidth = value;
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the maximum width constraint for flexible columns. Null means unlimited.
		/// </summary>
		public int? MaxWidth
		{
			get => _maxWidth;
			set
			{
				if (_maxWidth != value)
				{
					_maxWidth = value;
					Invalidate(true);
				}
			}
		}

		/// <summary>
		/// Gets or sets the flex factor for proportional sizing when Width is null. A value of 1.0 means equal share.
		/// </summary>
		public double FlexFactor
		{
			get => _flexFactor;
			set
			{
				if (_flexFactor != value)
				{
					_flexFactor = Math.Max(0, value);
					Invalidate(true);
				}
			}
		}

		#region ILayoutAware Implementation

		/// <inheritdoc/>
		public LayoutRequirements GetLayoutRequirements()
		{
			if (_width.HasValue)
			{
				// Fixed width column
				return LayoutRequirements.Fixed(_width.Value, _horizontalAlignment);
			}
			else
			{
				// Calculate minimum width from content if not explicitly set
				int? effectiveMinWidth = _minWidth;
				if (!effectiveMinWidth.HasValue && _contents.Count > 0)
				{
					// Get the maximum required width from all content controls
					int maxContentWidth = 0;
					foreach (var content in _contents)
					{
						// Check if content has an explicit Width requirement
						if (content.Width.HasValue)
						{
							maxContentWidth = Math.Max(maxContentWidth, content.Width.Value);
						}
					}
					if (maxContentWidth > 0)
					{
						effectiveMinWidth = maxContentWidth;
					}
				}

				// Flexible column with optional constraints
				return new LayoutRequirements
				{
					MinWidth = effectiveMinWidth,
					MaxWidth = _maxWidth,
					HorizontalAlignment = _horizontalAlignment,
					FlexFactor = _flexFactor
				};
			}
		}

		/// <inheritdoc/>
		public void OnLayoutAllocated(LayoutAllocation allocation)
		{
			// Layout allocation is now handled by the DOM layout system
		}

		#endregion

		/// <inheritdoc/>
		public int? ContentWidth => GetContentWidth();

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set
			{
				_horizontalAlignment = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set
			{
				_verticalAlignment = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container ?? _horizontalGridContent?.Container;
			set
			{
				_container = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set
			{
				_visible = value;
				Invalidate(true);
			}
		}

		/// <summary>
		/// Gets the list of child controls in this column.
		/// </summary>
		public IReadOnlyList<IWindowControl> Contents => _contents;

		/// <summary>
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			return _contents;
		}

		/// <summary>
		/// Adds a child control to this column.
		/// </summary>
		/// <param name="content">The control to add.</param>
		public void AddContent(IWindowControl content)
		{
			content.Container = this;
			_contents.Add(content);

			// Update MinWidth if content has explicit Width and it's larger than current MinWidth
			// This ensures the column gets enough space during layout distribution
			if (content.Width.HasValue && (!_minWidth.HasValue || content.Width.Value > _minWidth.Value))
			{
				_minWidth = content.Width.Value;
			}

			// Force DOM rebuild for runtime addition
			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

			Invalidate(true);
		}

		/// <summary>
		/// Gets the actual rendered width of this column based on content.
		/// </summary>
		/// <returns>The maximum width required by content, or null if no content.</returns>
		public int? GetContentWidth()
		{
			// If explicit width is set, return that (includes margins already accounted for in Width)
			if (_width.HasValue)
				return _width.Value;

			if (_contents.Count == 0) return _margin.Left + _margin.Right;

			// Calculate width from visible content controls
			int maxWidth = 0;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				int contentWidth = content.ContentWidth ?? content.Width ?? 0;
				maxWidth = Math.Max(maxWidth, contentWidth);
			}
			return maxWidth + _margin.Left + _margin.Right;
		}

		/// <summary>
		/// Gets all interactive controls contained in this column.
		/// </summary>
		/// <returns>A list of interactive controls.</returns>
		public List<IInteractiveControl> GetInteractiveContents()
		{
			List<IInteractiveControl> interactiveContents = new List<IInteractiveControl>();
			foreach (var content in _contents)
			{
				if (content is IInteractiveControl interactiveContent)
				{
					interactiveContents.Add(interactiveContent);
				}
			}
			return interactiveContents;
		}

	private static readonly ThreadLocal<HashSet<ColumnContainer>> _invalidatingContainers = new(() => new HashSet<ColumnContainer>());

	/// <inheritdoc/>
	public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
	{
		_isDirty = true;

		// Prevent infinite recursion by tracking if this container is already being invalidated
		if (_invalidatingContainers.Value!.Contains(this))
		{
			return;
		}

		// Only invalidate parent grid if we're not being called from it
		// This prevents infinite recursion in invalidation chains
		if (callerControl != _horizontalGridContent && _horizontalGridContent != null)
		{
			_invalidatingContainers.Value!.Add(this);
			try
			{
				_horizontalGridContent.Invalidate();
			}
			finally
			{
				_invalidatingContainers.Value!.Remove(this);
			}
		}
	}

	/// <inheritdoc/>
	public int? GetVisibleHeightForControl(IWindowControl control)
	{
		// ColumnContainer doesn't clip its children, so delegate to parent
		// Navigate through HorizontalGridControl to reach the Window
		// Note: _container may not be set, but _horizontalGridContent.Container should point to Window
		var parentContainer = _horizontalGridContent?.Container;
		return parentContainer?.GetVisibleHeightForControl(control);
	}

	/// <summary>
	/// Invalidates only the child controls within this column without triggering parent invalidation.
	/// </summary>
	/// <param name="callerControl">Optional caller control to exclude from invalidation.</param>
	public void InvalidateOnlyColumnContents(IWindowControl? callerControl = null)
		{
			_isDirty = true;

			// Prevent infinite recursion by tracking if this container is already being invalidated
			if (_invalidatingContainers.Value!.Contains(this))
			{
				return;
			}
			
			_invalidatingContainers.Value!.Add(this);
			try
			{
				foreach (var content in _contents)
				{
					// Prevent infinite recursion by not invalidating:
					// 1. The horizontal grid content if it's the caller
					// 2. The caller control passed as parameter
					if (content != _horizontalGridContent && content != callerControl)
					{
						content.Invalidate();
					}
				}
			}
			finally
			{
				_invalidatingContainers.Value!.Remove(this);
			}
		}

		/// <summary>
		/// Removes a child control from this column and disposes it.
		/// </summary>
		/// <param name="content">The control to remove.</param>
		public void RemoveContent(IWindowControl content)
		{
			if (_contents.Remove(content))
			{
				content.Container = null;
				content.Dispose();

				// Force DOM rebuild for runtime removal
				(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

				Invalidate(true);
			}
		}

		/// <summary>
		/// Removes all child controls from this column.
		/// </summary>
		public void ClearContents()
		{
			foreach (var content in _contents)
			{
				content.Container = null;
				content.Dispose();
			}
			_contents.Clear();

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate(true);
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;

				// Fire focus events
				if (value && !hadFocus)
				{
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{

			// ColumnContainer doesn't process keys directly, delegate to focused content
			var focusedContent = GetInteractiveContents().FirstOrDefault(c => c.HasFocus);


			var result = focusedContent?.ProcessKey(key) ?? false;


			return result;
		}

		/// <inheritdoc/>
		/// <summary>
		/// ColumnContainer is a layout container and should not be directly focusable.
		/// Focus should go to the controls within this column instead.
		/// </summary>
		public bool CanReceiveFocus => false;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int totalHeight = _margin.Top + _margin.Bottom;
			int maxWidth = 0;
			int fillChildCount = 0;

			// First pass: sum non-fill children
			foreach (var content in _contents.Where(c => c.Visible))
			{
				if (content.VerticalAlignment == VerticalAlignment.Fill)
				{
					fillChildCount++;
					// For fill children without constraints, use a reasonable default height
					// This prevents them from reporting 0 or full content height
					totalHeight += 10; // Default reasonable height for fill children
				}
				else
				{
					var size = content.GetLogicalContentSize();
					totalHeight += size.Height;
					maxWidth = Math.Max(maxWidth, size.Width);
				}
			}

			// Get width from all children
			foreach (var content in _contents.Where(c => c.Visible))
			{
				var size = content.GetLogicalContentSize();
				maxWidth = Math.Max(maxWidth, size.Width);
			}

			return new System.Drawing.Size(maxWidth + _margin.Left + _margin.Right, totalHeight);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Invalidate(true);
		}

		/// <summary>
		/// Finds the control at the specified position within the column.
		/// </summary>
		/// <param name="position">Position relative to the column.</param>
		/// <returns>The control at the position, or null if no control found.</returns>
		public IInteractiveControl? GetControlAtPosition(Point position)
		{
			int currentY = _margin.Top;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				var size = content.GetLogicalContentSize();
				int contentHeight = size.Height;

				// Check if the position is within this control's bounds
				if (position.Y >= currentY && position.Y < currentY + contentHeight)
				{
					if (content is IInteractiveControl interactiveControl)
					{
						return interactiveControl;
					}
				}

				currentY += contentHeight;
			}

			return null;
		}

		/// <summary>
		/// Checks if the column contains the specified control.
		/// </summary>
		/// <param name="control">The control to check for.</param>
		/// <returns>True if the control is in this column; otherwise, false.</returns>
		public bool ContainsControl(IInteractiveControl control)
		{
			return control is IWindowControl windowControl && _contents.Contains(windowControl);
		}

		/// <summary>
		/// Calculates the position relative to a specific control within the column.
		/// </summary>
		/// <param name="control">The target control.</param>
		/// <param name="columnPosition">Position relative to the column.</param>
		/// <returns>Position relative to the control.</returns>
		public Point GetControlRelativePosition(IInteractiveControl control, Point columnPosition)
		{
			int currentY = _margin.Top;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				if (content == control)
				{
					return new Point(columnPosition.X - _margin.Left, columnPosition.Y - currentY);
				}

				var size = content.GetLogicalContentSize();
				currentY += size.Height;
			}

			return columnPosition; // Fallback if control not found
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
        public LayoutSize MeasureDOM(LayoutConstraints constraints)
        {
            int maxWidth = 0;

            // FIX: If we have an explicit width (set by splitter), use it to calculate content width
            // This ensures children measure with the correct width constraints
            int contentMaxWidth;
            if (_width.HasValue)
            {
                // Use explicit width minus margins for child constraints
                contentMaxWidth = Math.Max(0, _width.Value - _margin.Left - _margin.Right);
            }
            else
            {
                // Use constraint width minus margins (original behavior)
                contentMaxWidth = constraints.MaxWidth - _margin.Left - _margin.Right;
            }

            // Use two-pass measurement like VerticalStackLayout to properly handle Fill children
            // First pass: measure non-Fill children to determine fixed height
            int fixedHeight = _margin.Top + _margin.Bottom;
            int fillCount = 0;
            var childSizes = new Dictionary<IWindowControl, LayoutSize>();

            foreach (var content in _contents.Where(c => c.Visible))
            {
                if (content.VerticalAlignment == VerticalAlignment.Fill)
                {
                    fillCount++;
                }
                else
                {
                    // Measure non-fill children with remaining available height
                    if (content is IDOMPaintable paintable)
                    {
                        var childConstraints = new LayoutConstraints(
                            constraints.MinWidth > 0 ? Math.Max(0, constraints.MinWidth - _margin.Left - _margin.Right) : 0,
                            contentMaxWidth,
                            0,
                            Math.Max(0, constraints.MaxHeight - fixedHeight)
                        );

                        var childSize = paintable.MeasureDOM(childConstraints);
                        childSizes[content] = childSize;
                        fixedHeight += childSize.Height;
                        maxWidth = Math.Max(maxWidth, childSize.Width);
                    }
                    else
                    {
                        var size = content.GetLogicalContentSize();
                        var layoutSize = new LayoutSize(size.Width, size.Height);
                        childSizes[content] = layoutSize;
                        fixedHeight += layoutSize.Height;
                        maxWidth = Math.Max(maxWidth, layoutSize.Width);
                    }
                }
            }

            // Second pass: measure Fill children with remaining space divided among them
            int remainingHeight = Math.Max(0, constraints.MaxHeight - fixedHeight);
            int fillHeight = fillCount > 0 ? remainingHeight / fillCount : 0;

            foreach (var content in _contents.Where(c => c.Visible))
            {
                if (content.VerticalAlignment == VerticalAlignment.Fill)
                {
                    if (content is IDOMPaintable paintable)
                    {
                        var childConstraints = new LayoutConstraints(
                            constraints.MinWidth > 0 ? Math.Max(0, constraints.MinWidth - _margin.Left - _margin.Right) : 0,
                            contentMaxWidth,
                            0,
                            fillHeight
                        );

                        var childSize = paintable.MeasureDOM(childConstraints);
                        childSizes[content] = childSize;
                        maxWidth = Math.Max(maxWidth, childSize.Width);
                    }
                    else
                    {
                        var size = content.GetLogicalContentSize();
                        var layoutSize = new LayoutSize(size.Width, size.Height);
                        childSizes[content] = layoutSize;
                        maxWidth = Math.Max(maxWidth, layoutSize.Width);
                    }
                }
            }

            // Calculate total height
            int totalHeight = _margin.Top + _margin.Bottom;
            foreach (var size in childSizes.Values)
            {
                totalHeight += size.Height;
            }

            int finalWidth = maxWidth + _margin.Left + _margin.Right;

            // If we have an explicit width, use it
            if (_width.HasValue)
            {
                finalWidth = _width.Value;
            }

            return new LayoutSize(
                Math.Clamp(finalWidth, constraints.MinWidth, constraints.MaxWidth),
                Math.Clamp(totalHeight, constraints.MinHeight, constraints.MaxHeight)
            );
        }


		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			// NOTE: Container controls should NOT paint their children here.
			// Children are painted by the DOM tree's child LayoutNodes.
			// This method only paints the container's own content (background, margins).

			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;

			// Fill the entire bounds with background color
			// This provides the background for the container and any margins
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			_isDirty = false;
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled && Visible;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled && Visible;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!Visible || !IsEnabled)
				return false;

			bool childHandled = false;

			// Find which child control was clicked
			int currentY = _margin.Top;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				var size = content.GetLogicalContentSize();
				int contentHeight = size.Height;

				// Check if the mouse position is within this control's bounds
				if (args.Position.Y >= currentY && args.Position.Y < currentY + contentHeight)
				{
					// Forward to IMouseAwareControl if applicable
					if (content is IMouseAwareControl mouseAware)
					{
						// Adjust position relative to the child control
						var childPosition = new Point(args.Position.X - _margin.Left, args.Position.Y - currentY);
						var relativeArgs = args.WithPosition(childPosition);

						if (mouseAware.ProcessMouseEvent(relativeArgs))
						{
							args.Handled = true;
							childHandled = true;
						}
					}
					break;
				}

				currentY += contentHeight;
			}

			// Handle white space clicks (no child handled the event)
			if (!childHandled)
			{
				// Parent already validated click is within column bounds
				// Just exclude margin areas
				if (args.Position.X >= _margin.Left && args.Position.Y >= _margin.Top)
				{
					// Adjust position to be relative to content area (exclude margins)
					var contentPosition = new Point(args.Position.X - _margin.Left, args.Position.Y - _margin.Top);
					var contentArgs = args.WithPosition(contentPosition);

					// Handle right-click
					if (args.HasFlag(MouseFlags.Button3Clicked))
					{
						MouseRightClick?.Invoke(this, contentArgs);
						return true;
					}

					// Handle double-click detection (two methods like ListControl)

					// Method 1: Direct flag detection from driver (preferred method)
					if (args.HasFlag(MouseFlags.Button1DoubleClicked) && _doubleClickEnabled)
					{
						// Reset tracking state since driver handled the gesture
						_lastClickTime = DateTime.MinValue;
						_lastClickPosition = Point.Empty;

						MouseDoubleClick?.Invoke(this, contentArgs);
						return true;
					}

					// Method 2: Manual timer-based detection (fallback)
					if (args.HasFlag(MouseFlags.Button1Clicked))
					{
						// Detect double-click
						var now = DateTime.UtcNow;
						var timeSince = (now - _lastClickTime).TotalMilliseconds;
						bool isDoubleClick = _doubleClickEnabled &&
											 args.Position == _lastClickPosition &&
											 timeSince <= _doubleClickThresholdMs;

						_lastClickTime = now;
						_lastClickPosition = args.Position;

						// Mutually exclusive: Fire either MouseDoubleClick OR MouseClick
						if (isDoubleClick)
						{
							MouseDoubleClick?.Invoke(this, contentArgs);
						}
						else
						{
							MouseClick?.Invoke(this, contentArgs);
						}

						return true;
					}
				}
			}

			return childHandled;
		}

		#endregion

		/// <inheritdoc/>
		public void Dispose()
		{
			foreach (var content in _contents)
			{
				content.Dispose();
			}
			_contents.Clear();
		}
	}
}