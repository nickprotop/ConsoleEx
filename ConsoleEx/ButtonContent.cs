using Spectre.Console;

namespace ConsoleEx
{
	public class ButtonContent : IWIndowContent, IInteractiveContent
	{
		private int? _width;
		private string _text = "Button";
		private string? _cachedContent;
		private bool _focused;
		private bool _enabled = true;
		private Alignment _justify = Alignment.Left;

		public Action<ButtonContent>? OnClick { get; set; }

		public IContainer? Container { get; set; }

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(); } }

		public bool IsEnabled
		{
			get => _enabled;
			set
			{
				_cachedContent = null;
				_enabled = value;
				Container?.Invalidate();
			}
		}

		public void Invalidate()
		{
			_cachedContent = null;
		}

		public bool HasFocus
		{
			get => _focused;
			set
			{
				_cachedContent = null;
				_focused = value;
			}
		}

		public string Text
		{
			get => _text;
			set
			{
				_text = value;
				_cachedContent = null;
				Container?.Invalidate();
			}
		}

		public Alignment Alignment { get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(); } }

		public int? ActualWidth => _cachedContent == null ? null : AnsiConsoleExtensions.StripAnsiStringLength(_cachedContent);

		public void Dispose()
		{
			Container = null;
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			return null;
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (key.Key == ConsoleKey.Enter)
			{
				OnClick?.Invoke(this);
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

			int buttonWidth = _width ?? (availableWidth ?? 20); // Default width if not specified
			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
			int maxTextLength = buttonWidth - 4; // Account for brackets and padding

			if (AnsiConsoleExtensions.StripSpectreLength(text) > maxTextLength)
			{
				text = text.Substring(0, maxTextLength - 3) + "...";
			}

			int padding = (buttonWidth - AnsiConsoleExtensions.StripSpectreLength(text) - 2) / 2;
			if (padding < 0) padding = 0; // Ensure padding is not negative

			string buttonText = $"{new string(' ', padding)}{text}{new string(' ', padding)}";

			// Ensure the buttonText fits within the buttonWidth
			if (AnsiConsoleExtensions.StripSpectreLength(buttonText) < buttonWidth - 2)
			{
				buttonText = buttonText.PadRight(buttonWidth - 2);
			}

			// Create the final string with [ at the start and ] at the end
			string finalButtonText = $"[[{buttonText}]]";

			// Check if finalButtonText is of the desired width
			if (AnsiConsoleExtensions.StripSpectreLength(finalButtonText) != buttonWidth)
			{
				finalButtonText = finalButtonText.Insert(2, new string(' ', buttonWidth - AnsiConsoleExtensions.StripSpectreLength(finalButtonText)));
			}

			List<string> renderedAnsi = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(finalButtonText, buttonWidth, availableHeight, false, backgroundColor, foregroundColor);
			_cachedContent = renderedAnsi.First();
			return renderedAnsi;
		}
	}
}
