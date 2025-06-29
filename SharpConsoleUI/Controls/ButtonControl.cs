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
using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	public class ButtonControl : IWIndowControl, IInteractiveControl, IMouseAwareControl, IFocusableControl
	{
		private Alignment _alignment = Alignment.Left;
		private string? _cachedContent;
		private bool _enabled = true;
		private bool _focused;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private Action<ButtonControl>? _onClick;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _text = "Button";
		private bool _visible = true;
		private int? _width;
		public int? ActualWidth => _cachedContent == null ? null : AnsiConsoleHelper.StripAnsiStringLength(_cachedContent);

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _cachedContent = null; Container?.Invalidate(true); } }

		public IContainer? Container { get; set; }

		public bool HasFocus
		{
			get => _focused;
			set
			{
				_cachedContent = null;
				_focused = value;
			}
		}

		public bool IsEnabled
		{
			get => _enabled;
			set
			{
				_cachedContent = null;
				_enabled = value;
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _cachedContent = null; Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool Visible
		{ get => _visible; set { _visible = value; _cachedContent = null; Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(true); } }

		public void Dispose()
		{
			Container = null;
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			return null;
		}

		public void Invalidate()
		{
			_cachedContent = null;
		}

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

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (_cachedContent != null)
			{
				return new List<string>() { _cachedContent };
			}

			Color backgroundColor = Container?.BackgroundColor ?? Color.Black;
			Color foregroundColor = Container?.ForegroundColor ?? Color.White;

			Color windowBackground = Container?.GetConsoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black;
			Color windowForeground = Container?.GetConsoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White;

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

			int buttonWidth = _width ?? (_alignment == Alignment.Strecth ? (availableWidth ?? 20) : AnsiConsoleHelper.StripSpectreLength(text) + 4);
			int maxTextLength = buttonWidth - 4; // Account for brackets and padding

			if (AnsiConsoleHelper.StripSpectreLength(text) > maxTextLength)
			{
				text = text.Substring(0, maxTextLength - 3) + "...";
			}

			int padding = (buttonWidth - AnsiConsoleHelper.StripSpectreLength(text) - 2) / 2;
			if (padding < 0) padding = 0; // Ensure padding is not negative

			// Create the final string with [ at the start and ] at the end
			string finalButtonText = $"[{new string(' ', padding)}{text}{new string(' ', padding)}]";

			// Ensure the buttonText fits within the buttonWidth
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) < buttonWidth)
			{
				finalButtonText = finalButtonText.PadRight(buttonWidth);
			}

			// Check if finalButtonText is of the desired width
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) < buttonWidth)
			{
				finalButtonText = finalButtonText.Insert(0, new string(' ', buttonWidth - AnsiConsoleHelper.StripSpectreLength(finalButtonText)));
			}

			int maxContentWidth = _width ?? (_alignment == Alignment.Strecth ? (availableWidth ?? 20) : AnsiConsoleHelper.StripSpectreLength(finalButtonText));

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			List<string> renderedAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{finalButtonText}", buttonWidth, availableHeight, false, backgroundColor, foregroundColor);

			for (int i = 0; i < renderedAnsi.Count; i++)
			{
				renderedAnsi[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedAnsi[i];

				renderedAnsi[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', _margin.Left)}", _margin.Left, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedAnsi[i];
				renderedAnsi[i] = renderedAnsi[i] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', _margin.Right)}", _margin.Right, 1, false, Container?.BackgroundColor, null).FirstOrDefault();
			}

			int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedAnsi.FirstOrDefault() ?? string.Empty);

			if (_margin.Top > 0)
			{
				renderedAnsi.InsertRange(0, Enumerable.Repeat($"{AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(new string(' ', finalWidth), finalWidth, 1, false, windowBackground, windowForeground).FirstOrDefault()}", _margin.Top));
			}

			if (_margin.Bottom > 0)
			{
				renderedAnsi.InsertRange(0, Enumerable.Repeat($"{AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(new string(' ', finalWidth), finalWidth, 1, false, windowBackground, windowForeground).FirstOrDefault()}", _margin.Bottom));
			}

			_cachedContent = renderedAnsi.First();
			return renderedAnsi;
		}

		public void SetFocus(bool focus, bool backward)
		{
			HasFocus = focus;
		}

		// IMouseAwareControl implementation
		public bool WantsMouseEvents => IsEnabled;
		public bool CanFocusWithMouse => IsEnabled;

		/// <summary>
		/// Event fired when the button is clicked (by mouse or keyboard)
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>
		/// Convenience property for simple action-based click handling
		/// </summary>
		public Action<ButtonControl>? OnClick
		{
			get => _onClick;
			set
			{
				if (_onClick != null)
				{
					// Remove previous handler
					MouseClick -= OnClickHandler;
				}
				
				_onClick = value;
				
				if (_onClick != null)
				{
					// Add new handler
					MouseClick += OnClickHandler;
				}
			}
		}

		private void OnClickHandler(object? sender, MouseEventArgs args)
		{
			_onClick?.Invoke(this);
		}
		public event EventHandler<MouseEventArgs>? MouseEnter;
		public event EventHandler<MouseEventArgs>? MouseLeave;
		public event EventHandler<MouseEventArgs>? MouseMove;

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
			// Fire the unified click event
			MouseClick?.Invoke(this, args);
		}

		// IFocusableControl implementation
		public bool CanReceiveFocus => IsEnabled;

		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;

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