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
	public class ColumnContainer : IContainer, IInteractiveControl, IFocusableControl
	{
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColor;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private IContainer? _container;
		private List<IWIndowControl> _contents = new List<IWIndowControl>();
		private Color? _foregroundColor;
		private bool _hasFocus;
		private HorizontalGridControl _horizontalGridContent;
		private bool _isDirty;
		private bool _isEnabled = true;
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
		{ get { return _backgroundColor ?? _consoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black; } set { _backgroundColor = value; this.SafeInvalidate(InvalidationReason.PropertyChanged); } }

		public Color ForegroundColor
		{ get { return _foregroundColor ?? _consoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White; } set { _foregroundColor = value; this.SafeInvalidate(InvalidationReason.PropertyChanged); } }

		public ConsoleWindowSystem? GetConsoleWindowSystem
		{ get => _consoleWindowSystem; set { _consoleWindowSystem = value; foreach (IWIndowControl control in _contents) { control.Invalidate(); }; this.SafeInvalidate(InvalidationReason.PropertyChanged); } }

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
				_width = value;
				Invalidate(true);
			}
		}
		
		// IWIndowControl implementation
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

		public void AddContent(IWIndowControl content)
		{
			content.Container = this;
			_contents.Add(content);
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

		public void Invalidate(bool redrawAll, IWIndowControl? callerControl = null)
		{
			_isDirty = true;
			_contentCache.Invalidate(InvalidationReason.ChildInvalidated);
			_horizontalGridContent.Invalidate();
		}

		public void InvalidateOnlyColumnContents()
		{
			_isDirty = true;
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
			foreach (var content in _contents)
			{
				content.Invalidate();
			}
		}

		public void RemoveContent(IWIndowControl content)
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
			// Use thread-safe cache with lazy rendering
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContent = new List<string>();

			foreach (var content in _contents)
			{
				content.Invalidate();
			}

			// Render each content and collect the lines
			foreach (var content in _contents)
			{
				var contentRendered = content.RenderContent(_width ?? availableWidth, availableHeight);
				renderedContent.AddRange(contentRendered);
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
		
		// Additional IWIndowControl methods
		public System.Drawing.Size GetLogicalContentSize()
		{
			var content = RenderContent(int.MaxValue, int.MaxValue);
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
				var renderedContent = content.RenderContent(_width, null);
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
			return control is IWIndowControl windowControl && _contents.Contains(windowControl);
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
				
				var renderedContent = content.RenderContent(_width, null);
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