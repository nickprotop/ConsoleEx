// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Core;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public class CheckboxControl : IWIndowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider
	{
		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColorValue;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private bool _checked = false;
		private Color? _checkmarkColorValue;
		private Color? _disabledBackgroundColorValue;
		private Color? _disabledForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus = false;
		private bool _invalidated = true;
		private bool _isEnabled = true;
		private string _label = "Checkbox";
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		// Constructor with optional label and state
		public CheckboxControl(string label = "Checkbox", bool isChecked = false)
		{
			_label = label;
			_checked = isChecked;
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		// Events
		public event EventHandler<bool>? CheckedChanged;
		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;

		public int? ActualWidth
		{
			get
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
		}

		// Properties
		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); } }

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public bool Checked
		{
			get => _checked;
			set
			{
				if (_checked != value)
				{
					_checked = value;
					_contentCache.Invalidate(InvalidationReason.PropertyChanged);
					Container?.Invalidate(true);
					CheckedChanged?.Invoke(this, _checked);
				}
			}
		}

		public Color CheckmarkColor
		{
			get => _checkmarkColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.Cyan1;
			set
			{
				_checkmarkColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container { get; set; }

		public Color DisabledBackgroundColor
		{
			get => _disabledBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
			set
			{
				_disabledBackgroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Color DisabledForegroundColor
		{
			get => _disabledForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			set
			{
				_disabledForegroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus != value)
				{
					_hasFocus = value;
					_contentCache.Invalidate(InvalidationReason.PropertyChanged);
					Container?.Invalidate(true);
					
					if (value)
						GotFocus?.Invoke(this, EventArgs.Empty);
					else
						LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		public bool CanReceiveFocus => IsEnabled;

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public string Label
		{
			get => _label;
			set
			{
				_label = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

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

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public void Dispose()
		{
			Container = null;
			_contentCache.Dispose();
		}

		// ILogicalCursorProvider implementation
		public Point? GetLogicalCursorPosition()
		{
			return null; // Checkboxes don't have a visible cursor
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// Use reasonable maximum dimensions instead of int.MaxValue
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		public void SetLogicalCursorPosition(Point position)
		{
			// Checkboxes don't have cursor positioning
		}

		public void Invalidate()
		{
			_invalidated = true;
			_contentCache.Invalidate(InvalidationReason.All);
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			// Toggle checkbox state when Space or Enter is pressed
			if (key.Key == ConsoleKey.Spacebar || key.Key == ConsoleKey.Enter)
			{
				Checked = !Checked; // Use property setter to trigger event
				return true;
			}

			return false;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			// Use thread-safe cache with lazy rendering
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContentList = new List<string>();

			// Determine colors based on state
			Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			Color windowForeground = Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;

			// Determine colors based on enabled/focused state
			if (!_isEnabled)
			{
				backgroundColor = DisabledBackgroundColor;
				foregroundColor = DisabledForegroundColor;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			// Calculate effective width
			int checkboxWidth = _width ?? (_alignment == Alignment.Strecth ? (availableWidth ?? 40) : 0);

			// Calculate the minimum width needed
			int minWidth = AnsiConsoleHelper.StripSpectreLength($"[{(_checked ? "X" : " ")}] {_label}") + 2;

			// For non-stretch alignments, adjust width if needed or use auto-size
			if (_alignment != Alignment.Strecth && _width == null)
			{
				checkboxWidth = minWidth;
			}
			else
			{
				// Ensure the width is at least the minimum needed
				checkboxWidth = Math.Max(checkboxWidth, minWidth);
			}

			// Calculate padding for alignment
			int paddingLeft = 0;
			if (_alignment == Alignment.Center && availableWidth.HasValue)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth.Value, checkboxWidth);
			}
			else if (_alignment == Alignment.Right && availableWidth.HasValue)
			{
				paddingLeft = availableWidth.Value - checkboxWidth;
			}

			// Create the checkbox content with checkmark and label
			string checkmark = _checked ? "X" : " ";
			string checkboxContent;

			// Show focus indicators when focused
			if (_hasFocus)
			{
				checkboxContent = $">[{checkmark}] {_label}<";
			}
			else
			{
				checkboxContent = $" [{checkmark}] {_label} ";
			}

			// Format the checkmark with color if checked
			if (_checked)
			{
				checkboxContent = checkboxContent.Replace(checkmark, $"[{CheckmarkColor.ToMarkup()}]{checkmark}[/]");
			}

			// Ensure the content fits within the checkbox width
			if (AnsiConsoleHelper.StripSpectreLength(checkboxContent) < checkboxWidth)
			{
				checkboxContent = checkboxContent.PadRight(checkboxWidth);
			}

			// Render the checkbox content
			List<string> renderedContent = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
				checkboxContent,
				checkboxWidth,
				1,
				false,
				backgroundColor,
				foregroundColor
			);

			// Apply padding and margins
			for (int i = 0; i < renderedContent.Count; i++)
			{
				// Add alignment padding
				if (paddingLeft > 0)
				{
					renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', paddingLeft),
						paddingLeft,
						1,
						false,
						Container?.BackgroundColor,
						null
					).FirstOrDefault() + renderedContent[i];
				}

				// Add left margin
				renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Left),
					_margin.Left,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault() + renderedContent[i];

				// Add right margin
				renderedContent[i] = renderedContent[i] + AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					new string(' ', _margin.Right),
					_margin.Right,
					1,
					false,
					Container?.BackgroundColor,
					null
				).FirstOrDefault();
			}

			// Add top margin
			if (_margin.Top > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent.FirstOrDefault() ?? string.Empty);
				var topMargin = Enumerable.Repeat(
					AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', finalWidth),
						finalWidth,
						1,
						false,
						windowBackground,
						windowForeground
					).FirstOrDefault() ?? string.Empty,
					_margin.Top
				).ToList();

				renderedContentList.InsertRange(0, topMargin);
			}

			// Add the checkbox content
			renderedContentList.AddRange(renderedContent);

			// Add bottom margin
			if (_margin.Bottom > 0)
			{
				int finalWidth = AnsiConsoleHelper.StripAnsiStringLength(renderedContent.FirstOrDefault() ?? string.Empty);
				var bottomMargin = Enumerable.Repeat(
					AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
						new string(' ', finalWidth),
						finalWidth,
						1,
						false,
						windowBackground,
						windowForeground
					).FirstOrDefault() ?? string.Empty,
					_margin.Bottom
				).ToList();

				renderedContentList.AddRange(bottomMargin);
			}

			_invalidated = false;
			return renderedContentList;
		}

		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		// Toggle method for convenience
		public void Toggle()
		{
			Checked = !Checked;
		}
	}
}