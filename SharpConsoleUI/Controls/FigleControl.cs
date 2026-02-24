// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// FIGlet text size based on embedded fonts.
	/// </summary>
	public enum FigletSize
	{
		/// <summary>Small font (~4 lines height)</summary>
		Small,

		/// <summary>Default/Standard font (~6 lines height) - Spectre's default FIGlet font</summary>
		Default,

		/// <summary>Large/Banner font (~8 lines height)</summary>
		Large,

		/// <summary>Custom font provided by user</summary>
		Custom
	}

	/// <summary>
	/// A control that renders text using FIGlet ASCII art fonts.
	/// Wraps the Spectre.Console FigletText component for large decorative text display.
	/// </summary>
	public class FigleControl : BaseControl
	{
		private Color? _color;
		private string? _text;
		private bool _rightPadded = true;
		private FigletSize _size = FigletSize.Default;
		private FigletFont? _customFont;
		private string? _fontPath;

		/// <summary>
		/// Initializes a new instance of the <see cref="FigleControl"/> class.
		/// </summary>
		public FigleControl()
		{
		}

		/// <inheritdoc/>
		public override int? ContentWidth
		{
			get
			{
				if (string.IsNullOrEmpty(_text)) return Margin.Left + Margin.Right;

				// Calculate width by rendering to get actual FIGlet dimensions
				FigletText figletText = new FigletText(GetFont(), _text);
				var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, Width ?? 80, null, bgColor);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the color of the FIGlet text.
		/// </summary>
		public Color? Color
		{ get => _color; set { _color = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the text to render as FIGlet ASCII art.
		/// </summary>
		public string? Text
		{ get => _text; set { _text = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets whether the right side should be padded.
		/// </summary>
		public bool RightPadded
		{
			get => _rightPadded;
			set => PropertySetterHelper.SetProperty(ref _rightPadded, value, Container);
		}

		/// <summary>
		/// Gets or sets the FIGlet text size (uses embedded fonts).
		/// </summary>
		public FigletSize Size
		{
			get => _size;
			set => PropertySetterHelper.SetEnumProperty(ref _size, value, Container);
		}

		/// <summary>
		/// Gets or sets a custom FigletFont. Takes precedence over Size.
		/// </summary>
		public FigletFont? CustomFont
		{
			get => _customFont;
			set => PropertySetterHelper.SetProperty(ref _customFont, value, Container);
		}

		/// <summary>
		/// Gets or sets the name of a custom .flf font file (without extension).
		/// The font must be located in the 'fonts' directory relative to the application base directory.
		/// Takes precedence over Size but lower than CustomFont.
		/// </summary>
		public string? FontPath
		{
			get => _fontPath;
			set => PropertySetterHelper.SetProperty(ref _fontPath, value, Container);
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (string.IsNullOrEmpty(_text))
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			// Reuse ContentWidth for width calculation
			int width = ContentWidth ?? 0;

			// Calculate height by rendering
			FigletText figletText = new FigletText(GetFont(), _text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, Width ?? 80, null, bgColor);
			int height = content.Count + Margin.Top + Margin.Bottom;

			return new System.Drawing.Size(width, height);
		}

		/// <summary>
		/// Sets the color of the FIGlet text.
		/// </summary>
		/// <param name="color">The color to apply to the text.</param>
		public void SetColor(Color color)
		{
			_color = color;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the text to render as FIGlet ASCII art.
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetText(string text)
		{
			_text = text;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Gets the FigletFont to use based on Size, FontPath, or CustomFont.
		/// </summary>
		private FigletFont GetFont()
		{
			// Priority: CustomFont > FontPath > Size-based > Default
			if (_customFont != null)
				return _customFont;

			if (!string.IsNullOrEmpty(_fontPath))
			{
				try
				{
					// Security: Validate font path to prevent path traversal attacks
					// Check for suspicious patterns in the input
					string normalizedInput = _fontPath.Replace('\\', '/');
					if (normalizedInput.Contains("../") || normalizedInput.Contains("..\\") ||
					    Path.IsPathFullyQualified(_fontPath))
					{
						throw new ArgumentException($"Invalid font path: path traversal detected in '{_fontPath}'");
					}

					string fontsDir = Path.Combine(AppContext.BaseDirectory, "fonts");
					string fontFileName = _fontPath.EndsWith(".flf") ? _fontPath : _fontPath + ".flf";
					string safePath = Path.GetFullPath(Path.Combine(fontsDir, fontFileName));

					// Ensure the resolved path is within the fonts directory
					string normalizedFontsDir = Path.GetFullPath(fontsDir);
					if (!safePath.StartsWith(normalizedFontsDir + Path.DirectorySeparatorChar) &&
					    safePath != normalizedFontsDir)
					{
						throw new ArgumentException($"Invalid font path: path traversal detected in '{_fontPath}'");
					}

					if (File.Exists(safePath))
					{
						using var stream = File.OpenRead(safePath);
						return FigletFont.Load(stream);
					}
				}
				catch (ArgumentException)
				{
					// Re-throw security exceptions
					throw;
				}
				catch
				{
					// Fall through to size-based for other errors
				}
			}

			return GetFontForSize(_size);
		}

		/// <summary>
		/// Loads embedded font based on size.
		/// </summary>
		private static FigletFont GetFontForSize(FigletSize size)
		{
			return size switch
			{
				FigletSize.Small => LoadEmbeddedFont("small.flf"),
				FigletSize.Default => LoadEmbeddedFont("standard.flf"),
				FigletSize.Large => LoadEmbeddedFont("banner.flf"),
				FigletSize.Custom => FigletFont.Default,
				_ => FigletFont.Default
			};
		}

		/// <summary>
		/// Loads embedded font resource.
		/// </summary>
		private static FigletFont LoadEmbeddedFont(string fileName)
		{
			var assembly = typeof(FigleControl).Assembly;
			var resourceName = $"SharpConsoleUI.Resources.Fonts.{fileName}";

			try
			{
				using var stream = assembly.GetManifestResourceStream(resourceName);
				if (stream != null)
					return FigletFont.Load(stream);
			}
			catch
			{
				// Fall back to default if embedded font not found
			}

			return FigletFont.Default;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			if (string.IsNullOrEmpty(_text))
			{
				return new LayoutSize(
					Math.Clamp(Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
				);
			}

			// For Figlet text, we need to render to get the size
			FigletText figletText = new FigletText(GetFont(), _text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			int targetWidth = Width ?? constraints.MaxWidth - Margin.Left - Margin.Right;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, targetWidth, null, bgColor);

			int maxWidth = content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line));
			int width = maxWidth + Margin.Left + Margin.Right;
			int height = content.Count + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = _color ?? Container?.ForegroundColor ?? defaultFg;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			if (!string.IsNullOrEmpty(_text))
			{
				// Render the FIGlet text
				FigletText figletText = new FigletText(GetFont(), _text);
				figletText.Color = fgColor;
				figletText.Justification = HorizontalAlignment switch
				{
					HorizontalAlignment.Left => Justify.Left,
					HorizontalAlignment.Center => Justify.Center,
					HorizontalAlignment.Right => Justify.Right,
					_ => Justify.Left
				};

				int figletWidth = Width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, figletWidth, null, bgColor);

				int figletHeight = renderedContent.Count;
				int availableHeight = bounds.Height - Margin.Top - Margin.Bottom;

				for (int i = 0; i < Math.Min(figletHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (Margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, Margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Parse and write the FIGlet line (Spectre handles justification)
						// Only write non-space characters to avoid overwriting shadow
						var plainText = AnsiConsoleHelper.StripAnsi(renderedContent[i]);
						for (int charIdx = 0; charIdx < plainText.Length; charIdx++)
						{
							char ch = plainText[charIdx];
							if (ch != ' ')
							{
								int x = startX + charIdx;
								if (x >= clipRect.X && x < clipRect.Right)
								{
									buffer.SetCell(x, paintY, ch, fgColor, bgColor);
								}
							}
						}

						// Fill right margin
						if (Margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after FIGlet content
				for (int y = startY + figletHeight; y < bounds.Bottom - Margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			for (int y = bounds.Bottom - Margin.Bottom; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}
		}

		#endregion
	}
}
