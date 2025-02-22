using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Contents
{
	public class ButtonContent : IWIndowContent, IInteractiveContent
	{
		private Alignment _alignment = Alignment.Left;
		private string? _cachedContent;
		private bool _enabled = true;
		private bool _focused;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string _text = "Button";
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

		public Action<ButtonContent>? OnClick { get; set; }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

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
			string finalButtonText = $"[[{new string(' ', padding)}{text}{new string(' ', padding)}]]";

			// Ensure the buttonText fits within the buttonWidth
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) < buttonWidth - 2)
			{
				finalButtonText = finalButtonText.PadRight(buttonWidth - 2);
			}

			// Check if finalButtonText is of the desired width
			if (AnsiConsoleHelper.StripSpectreLength(finalButtonText) != buttonWidth)
			{
				finalButtonText = finalButtonText.Insert(2, new string(' ', buttonWidth - AnsiConsoleHelper.StripSpectreLength(finalButtonText)));
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
	}
}