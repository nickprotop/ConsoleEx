// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Core;
using Spectre.Console;
using System;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A clickable button control that supports keyboard and mouse interaction.
	/// </summary>
	public class ButtonControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		private Alignment _alignment = Alignment.Left;
		private readonly ThreadSafeCache<string> _contentCache;
		private bool _enabled = true;
		private bool _focused;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _text = "Button";
		private bool _visible = true;
		private int? _width;

		/// <summary>
		/// Initializes a new instance of the ButtonControl class with default settings.
		/// </summary>
		public ButtonControl()
		{
			_contentCache = this.CreateThreadSafeCache<string>();
		}

		/// <summary>
		/// Gets the actual rendered width of the button in characters.
		/// </summary>
		public int? ActualWidth => _contentCache.Content == null ? null : AnsiConsoleHelper.StripAnsiStringLength(_contentCache.Content);

		/// <inheritdoc/>
		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _focused;
			set
			{
				_focused = value;
				_contentCache.Invalidate(InvalidationReason.FocusChanged);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets whether the button is enabled and can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => _enabled;
			set
			{
				_enabled = value;
				_contentCache.Invalidate(InvalidationReason.StateChanged);
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				this.SafeInvalidate(InvalidationReason.PropertyChanged);
			}
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text displayed on the button.
		/// </summary>
		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				_contentCache.Invalidate(InvalidationReason.ContentChanged);
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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
					_contentCache.Invalidate(InvalidationReason.SizeChanged);
					Container?.Invalidate(true);
				}
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
			_contentCache.Dispose();
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			var content = RenderContent(int.MaxValue, int.MaxValue);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_contentCache.Invalidate(InvalidationReason.All);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (key.Key == ConsoleKey.Enter)
			{
				// Trigger the click event
				TriggerClick(new MouseEventArgs(
					new List<MouseFlags> { MouseFlags.Button1Clicked },
					new System.Drawing.Point(0, 0), // No specific position for keyboard
					new System.Drawing.Point(0, 0),
					new System.Drawing.Point(0, 0)
				));
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

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
				if (cached != null) return new List<string> { cached };
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			// Use thread-safe cache with lazy rendering
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight).FirstOrDefault() ?? string.Empty) switch
			{
				string content => new List<string> { content },
				_ => new List<string>()
			};
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{

			Color backgroundColor = Container?.BackgroundColor ?? Color.Black;
			Color foregroundColor = Container?.ForegroundColor ?? Color.White;

			Color windowBackground = Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			Color windowForeground = Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;

			if (Container?.GetConsoleWindowSystem?.Theme != null)
			{
				if (_enabled == false)
				{
					foregroundColor = Container.GetConsoleWindowSystem.Theme.ButtonDisabledForegroundColor;
					backgroundColor = Container.GetConsoleWindowSystem.Theme.ButtonDisabledBackgroundColor;
				}
				else
				{
					if (_focused)
					{
						{
							foregroundColor = Container.GetConsoleWindowSystem.Theme.ButtonFocusedForegroundColor;
							backgroundColor = Container.GetConsoleWindowSystem.Theme.ButtonFocusedBackgroundColor;
						}
					}
					else
					{
						foregroundColor = Container.GetConsoleWindowSystem.Theme.ButtonForegroundColor;
						backgroundColor = Container.GetConsoleWindowSystem.Theme.ButtonBackgroundColor;
					}
				}
			}

			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";

			// Calculate button width with minimum of 4 to ensure room for brackets
			int rawButtonWidth = _width ?? (_alignment == Alignment.Stretch ? (availableWidth ?? 20) : AnsiConsoleHelper.StripSpectreLength(text) + 4);
			int buttonWidth = Math.Max(4, rawButtonWidth); // Minimum width of 4 for "[ ]"
			int maxTextLength = buttonWidth - 4; // Account for brackets and padding

			if (maxTextLength > 0 && AnsiConsoleHelper.StripSpectreLength(text) > maxTextLength)
			{
				// Use TruncateSpectre to safely truncate text with Spectre markup
				int truncateLength = Math.Max(0, maxTextLength - 3);
				text = truncateLength > 0
					? AnsiConsoleHelper.TruncateSpectre(text, truncateLength) + "..."
					: "...".Substring(0, Math.Max(0, maxTextLength)); // Handle very small widths
			}
			else if (maxTextLength <= 0)
			{
				// Button is too small for any text
				text = string.Empty;
			}

			int padding = (buttonWidth - AnsiConsoleHelper.StripSpectreLength(text) - 2) / 2;
			if (padding < 0) padding = 0; // Ensure padding is not negative

			// Create the final string with [ at the start and ] at the end
			string finalButtonText = $"[{new string(' ', padding)}{text}{new string(' ', padding)}]";

			// Ensure the buttonText fits within the buttonWidth using visible-length-aware padding
			int visibleLength = AnsiConsoleHelper.StripSpectreLength(finalButtonText);
			if (visibleLength < buttonWidth)
			{
				// Use manual padding based on visible length, not string length
				finalButtonText = finalButtonText + new string(' ', buttonWidth - visibleLength);
			}

			// Check if finalButtonText is of the desired width
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) < buttonWidth)
			{
				finalButtonText = finalButtonText.Insert(0, new string(' ', buttonWidth - AnsiConsoleHelper.StripSpectreLength(finalButtonText)));
			}

			int targetWidth = availableWidth ?? 80;

			List<string> renderedAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{finalButtonText}", buttonWidth, availableHeight, false, backgroundColor, foregroundColor);

			// Apply alignment padding
			for (int i = 0; i < renderedAnsi.Count; i++)
			{
				int lineWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedAnsi[i]);
				if (lineWidth < targetWidth)
				{
					int totalPadding = targetWidth - lineWidth;
					switch (_alignment)
					{
						case Alignment.Center:
							int leftPad = totalPadding / 2;
							int rightPad = totalPadding - leftPad;
							renderedAnsi[i] = AnsiConsoleHelper.AnsiEmptySpace(leftPad, windowBackground) + renderedAnsi[i] + AnsiConsoleHelper.AnsiEmptySpace(rightPad, windowBackground);
							break;
						case Alignment.Right:
							renderedAnsi[i] = AnsiConsoleHelper.AnsiEmptySpace(totalPadding, windowBackground) + renderedAnsi[i];
							break;
						default: // Left or Stretch
							renderedAnsi[i] = renderedAnsi[i] + AnsiConsoleHelper.AnsiEmptySpace(totalPadding, windowBackground);
							break;
					}
				}

				// Apply left margin
				if (_margin.Left > 0)
				{
					renderedAnsi[i] = AnsiConsoleHelper.AnsiEmptySpace(_margin.Left, windowBackground) + renderedAnsi[i];
				}

				// Apply right margin
				if (_margin.Right > 0)
				{
					renderedAnsi[i] = renderedAnsi[i] + AnsiConsoleHelper.AnsiEmptySpace(_margin.Right, windowBackground);
				}
			}

			// Add top margin
			if (_margin.Top > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedAnsi.FirstOrDefault() ?? string.Empty);
				for (int j = 0; j < _margin.Top; j++)
				{
					renderedAnsi.Insert(0, AnsiConsoleHelper.AnsiEmptySpace(finalWidth, windowBackground));
				}
			}

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedAnsi.FirstOrDefault() ?? string.Empty);
				for (int j = 0; j < _margin.Bottom; j++)
				{
					renderedAnsi.Add(AnsiConsoleHelper.AnsiEmptySpace(finalWidth, windowBackground));
				}
			}

			return renderedAnsi;
		}


		// IMouseAwareControl implementation
		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Event fired when the button is clicked (by mouse or keyboard).
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Event fired when the button is clicked (convenience event that provides the button as parameter).
		/// </summary>
		public event EventHandler<ButtonControl>? Click;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Handle mouse clicks
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				TriggerClick(args);
				args.Handled = true;
				return true;
			}

			// Handle mouse movement (for future hover effects)
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}

			return false;
		}

		/// <summary>
		/// Triggers the click event from either mouse or keyboard input
		/// </summary>
		private void TriggerClick(MouseEventArgs args)
		{
			// Fire the mouse click event
			MouseClick?.Invoke(this, args);
			
			// Fire the convenience click event
			Click?.Invoke(this, this);
		}

		// IFocusableControl implementation
		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var hadFocus = HasFocus;
			HasFocus = focus;
			
			if (focus && !hadFocus)
			{
				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else if (!focus && hadFocus)
			{
				LostFocus?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}