// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Core;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public class ColumnContainer : IContainer, IInteractiveControl, IFocusableControl, ILayoutAware
	{
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColorValue;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private IContainer? _container;
		private List<IWindowControl> _contents = new List<IWindowControl>();
		private Color? _foregroundColorValue;
		private bool _hasFocus;
		private HorizontalGridControl _horizontalGridContent;
		private bool _isDirty;
		private bool _isEnabled = true;
		private int? _lastRenderWidth;
		private int? _lastRenderHeight;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		public ColumnContainer(HorizontalGridControl horizontalGridContent)
		{
			_horizontalGridContent = horizontalGridContent;
			_consoleWindowSystem = horizontalGridContent.Container?.GetConsoleWindowSystem;
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		public Color BackgroundColor
		{
			// Inherit background color from parent HorizontalGridControl's container (the Window),
			// then fall back to theme
			get => _backgroundColorValue ?? _horizontalGridContent?.Container?.BackgroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set { _backgroundColorValue = value; this.SafeInvalidate(InvalidationReason.PropertyChanged); }
		}

		public Color ForegroundColor
		{
			// Inherit foreground color from parent HorizontalGridControl's container (the Window),
			// then fall back to theme
			get => _foregroundColorValue ?? _horizontalGridContent?.Container?.ForegroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set { _foregroundColorValue = value; this.SafeInvalidate(InvalidationReason.PropertyChanged); }
		}

		private bool _propagatingWindowSystem = false;

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
				this.SafeInvalidate(InvalidationReason.PropertyChanged);
			}
		}

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

		public bool IsDirty
		{
			get => _isDirty;
			set
			{
				_isDirty = value;
			}
		}

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

				// Notify layout service of requirements change
				NotifyLayoutRequirementsChanged();
			}
		}
	}

		private int? _minWidth;
		private int? _maxWidth;
		private double _flexFactor = 1.0;

		/// <summary>
		/// Minimum width constraint for flexible columns (null = no minimum, defaults to 1 during layout)
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
					NotifyLayoutRequirementsChanged();
				}
			}
		}

		/// <summary>
		/// Maximum width constraint for flexible columns (null = unlimited)
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
					NotifyLayoutRequirementsChanged();
				}
			}
		}

		/// <summary>
		/// Flex factor for proportional sizing when Width is null (1.0 = equal share)
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
					NotifyLayoutRequirementsChanged();
				}
			}
		}

		#region ILayoutAware Implementation

		/// <summary>
		/// Gets the layout requirements for this column
		/// </summary>
		public LayoutRequirements GetLayoutRequirements()
		{
			if (_width.HasValue)
			{
				// Fixed width column
				return LayoutRequirements.Fixed(_width.Value, _alignment);
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
					HorizontalAlignment = _alignment,
					FlexFactor = _flexFactor
				};
			}
		}

		/// <summary>
		/// Called when layout allocation is set for this column
		/// </summary>
		public void OnLayoutAllocated(LayoutAllocation allocation)
		{
			// Store the allocated dimensions for use during rendering
			// The RenderContent method will use _lastRenderWidth which gets updated during render
			// This is informational - the actual width comes from the RenderContent parameter
		}

		private void NotifyLayoutRequirementsChanged()
		{
			var layoutService = GetConsoleWindowSystem?.LayoutStateService;
			if (layoutService != null)
			{
				layoutService.UpdateRequirements(this, GetLayoutRequirements(), LayoutChangeReason.RequirementsChange);
			}
		}

		#endregion

		// IWindowControl implementation
		public int? ActualWidth => GetActualWidth();
		
		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				Invalidate(true);
			}
		}
		
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Invalidate(true);
			}
		}
		
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				Invalidate(true);
			}
		}
		
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Invalidate(true);
			}
		}
		
		public object? Tag { get; set; }
		
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
		/// Gets the list of child controls in this column
		/// </summary>
		public IReadOnlyList<IWindowControl> Contents => _contents;

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

			Invalidate(true);
		}

		public int? GetActualWidth()
		{
			var cachedContent = _contentCache.Content;
			if (cachedContent == null) return null;

			int maxLength = 0;
			foreach (var line in cachedContent)
			{
				int length = AnsiConsoleHelper.StripAnsiStringLength(line);
				if (length > maxLength) maxLength = length;
			}
			return maxLength;
		}

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

	public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
	{
		_isDirty = true;
		_contentCache.Invalidate(InvalidationReason.ChildInvalidated);

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

	/// <summary>
	/// Gets the actual visible height for a control within this column.
	/// Delegates to parent container if available.
	/// </summary>
	public int? GetVisibleHeightForControl(IWindowControl control)
	{
		// ColumnContainer doesn't clip its children, so delegate to parent
		// Navigate through HorizontalGridControl to reach the Window
		// Note: _container may not be set, but _horizontalGridContent.Container should point to Window
		var parentContainer = _horizontalGridContent?.Container;
		return parentContainer?.GetVisibleHeightForControl(control);
	}

	public void InvalidateOnlyColumnContents(IWindowControl? callerControl = null)
		{
			_isDirty = true;
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
			
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

		public void RemoveContent(IWindowControl content)
		{
			if (_contents.Remove(content))
			{
				content.Container = null;
				content.Dispose();
				Invalidate(true);
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = GetConsoleWindowSystem?.LayoutStateService;

			// Smart invalidation: check if re-render is needed due to size change
			if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
			{
				// Dimensions changed - invalidate cache
				_contentCache.Invalidate(InvalidationReason.SizeChanged);
			}
			else
			{
				// Dimensions unchanged - return cached content if available
				var cached = _contentCache.Content;
				if (cached != null) return cached;
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			// Use thread-safe cache with lazy rendering
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContent = new List<string>();

			// Store the effective render dimensions for cache validation and hit-testing
			_lastRenderWidth = _width ?? availableWidth;
			_lastRenderHeight = availableHeight;
			int targetWidth = _lastRenderWidth ?? 0;

			foreach (var content in _contents)
			{
				content.Invalidate();
			}

			// Render each content and collect the lines
			foreach (var content in _contents)
			{
				var contentRendered = content.RenderContent(_lastRenderWidth, availableHeight);
				renderedContent.AddRange(contentRendered);
			}

			// CRITICAL: Ensure ALL lines in this column have the same width
			// This is required for proper horizontal alignment when columns are combined
			if (targetWidth > 0)
			{
				for (int i = 0; i < renderedContent.Count; i++)
				{
					int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent[i]);
					if (lineWidth < targetWidth)
					{
						// Pad the line to the target width
						renderedContent[i] = renderedContent[i] + AnsiConsoleHelper.AnsiEmptySpace(targetWidth - lineWidth, BackgroundColor);
					}
					else if (lineWidth > targetWidth)
					{
						// Truncate the line to the target width (preserving ANSI codes)
						renderedContent[i] = AnsiConsoleHelper.SubstringAnsi(renderedContent[i], 0, targetWidth);
					}
				}
			}

			_isDirty = false;
			return renderedContent;
		}
		
		// IInteractiveControl implementation
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
		
		public bool IsEnabled 
		{ 
			get => _isEnabled;
			set 
			{ 
				_isEnabled = value; 
				Invalidate(true); 
			} 
		}
		
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// ColumnContainer doesn't process keys directly, delegate to focused content
			var focusedContent = GetInteractiveContents().FirstOrDefault(c => c.HasFocus);
			return focusedContent?.ProcessKey(key) ?? false;
		}
		
		// IFocusableControl implementation
		public bool CanReceiveFocus => IsEnabled;
		
		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;
		
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}
		
		// Additional IWindowControl methods
		public System.Drawing.Size GetLogicalContentSize()
		{
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}
		
		public void Invalidate()
		{
			Invalidate(true);
		}
		
		/// <summary>
		/// Finds the control at the specified position within the column
		/// </summary>
		/// <param name="position">Position relative to the column</param>
		/// <returns>The control at the position, or null if no control found</returns>
		public IInteractiveControl? GetControlAtPosition(Point position)
		{
			// Force render to ensure we have current layout using thread-safe cache
			var cachedContent = _contentCache.Content;
			if (cachedContent == null)
			{
				// Use reasonable maximum dimensions instead of int.MaxValue to avoid memory issues
				cachedContent = RenderContent(10000, 10000);
				
				// Verify content was rendered
				if (cachedContent == null)
				{
					return null;
				}
			}

			int currentY = 0;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				// Use _lastRenderWidth to match the width used during actual rendering
				var renderedContent = content.RenderContent(_lastRenderWidth, null);
				int contentHeight = renderedContent.Count;

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
		/// Checks if the column contains the specified control
		/// </summary>
		/// <param name="control">The control to check for</param>
		/// <returns>True if the control is in this column</returns>
		public bool ContainsControl(IInteractiveControl control)
		{
			return control is IWindowControl windowControl && _contents.Contains(windowControl);
		}

		/// <summary>
		/// Calculates the position relative to a specific control within the column
		/// </summary>
		/// <param name="control">The target control</param>
		/// <param name="columnPosition">Position relative to the column</param>
		/// <returns>Position relative to the control</returns>
		public Point GetControlRelativePosition(IInteractiveControl control, Point columnPosition)
		{
			// Force render to ensure we have current layout using thread-safe cache
			var cachedContent = _contentCache.Content;
			if (cachedContent == null)
			{
				// Use reasonable maximum dimensions instead of int.MaxValue to avoid memory issues
				cachedContent = RenderContent(10000, 10000);
				
				// Verify content was rendered
				if (cachedContent == null)
				{
					return new Point(0, 0);
				}
			}

			int currentY = 0;
			foreach (var content in _contents.Where(c => c.Visible))
			{
				if (content == control)
				{
					return new Point(columnPosition.X, columnPosition.Y - currentY);
				}

				// Use _lastRenderWidth to match the width used during actual rendering
				var renderedContent = content.RenderContent(_lastRenderWidth, null);
				currentY += renderedContent.Count;
			}

			return columnPosition; // Fallback if control not found
		}

		public void Dispose()
		{
			_contents.Clear();
			_contentCache.Dispose();
		}
	}
}