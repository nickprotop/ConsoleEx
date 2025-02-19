using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Contents
{
	public class ButtonContent : IWIndowContent, IInteractiveContent
	{
		private string? _cachedContent;
		private bool _enabled = true;
		private bool _focused;
		private Alignment _justify = Alignment.Left;
		private string _text = "Button";
		private int? _width;
		public int? ActualWidth => _cachedContent == null ? null : AnsiConsoleHelper.StripAnsiStringLength(_cachedContent);

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _cachedContent = null; Container?.Invalidate(); } }

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
				Container?.Invalidate();
			}
		}

		public Action<ButtonContent>? OnClick { get; set; }

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

		public int? Width
		{ get => _width; set { _width = value; _cachedContent = null; Container?.Invalidate(); } }

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

			int buttonWidth = _width ?? availableWidth ?? 20; // Default width if not specified
			string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
			int maxTextLength = buttonWidth - 4; // Account for brackets and padding

			if (AnsiConsoleHelper.StripSpectreLength(text) > maxTextLength)
			{
				text = text.Substring(0, maxTextLength - 3) + "...";
			}

			int padding = (buttonWidth - AnsiConsoleHelper.StripSpectreLength(text) - 2) / 2;
			if (padding < 0) padding = 0; // Ensure padding is not negative

			string buttonText = $"{new string(' ', padding)}{text}{new string(' ', padding)}";

			// Ensure the buttonText fits within the buttonWidth
			if (AnsiConsoleHelper.StripSpectreLength(buttonText) < buttonWidth - 2)
			{
				buttonText = buttonText.PadRight(buttonWidth - 2);
			}

			// Create the final string with [ at the start and ] at the end
			string finalButtonText = $"[[{buttonText}]]";

			// Check if finalButtonText is of the desired width
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) != buttonWidth)
			{
				finalButtonText = finalButtonText.Insert(2, new string(' ', buttonWidth - AnsiConsoleHelper.StripSpectreLength(finalButtonText)));
			}

			int maxContentWidth = _width ?? availableWidth ?? 80;

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			List<string> renderedAnsi = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{finalButtonText}", buttonWidth, availableHeight, false, backgroundColor, foregroundColor);

			for (int i = 0; i < renderedAnsi.Count; i++)
			{
				renderedAnsi[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedAnsi[i];
			}

			_cachedContent = renderedAnsi.First();
			return renderedAnsi;
		}
	}
}