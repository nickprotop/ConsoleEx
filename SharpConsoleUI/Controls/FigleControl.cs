// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// FIGlet text size based on embedded fonts.
	/// </summary>
	public enum FigletSize
	{
		/// <summary>Small font (~4 lines height)</summary>
		Small,

		/// <summary>Default/Standard font (~6 lines height) - default FIGlet font</summary>
		Default,

		/// <summary>Large/Banner font (~8 lines height)</summary>
		Large,

		/// <summary>Custom font provided by user</summary>
		Custom
	}

	/// <summary>
	/// A control that renders text using FIGlet ASCII art fonts.
	/// Uses a custom FIGlet parser for direct rendering without Spectre.Console dependency.
	/// </summary>
	public class FigleControl : BaseControl
	{
		private Color? _color;
		private string? _text;
		private bool _rightPadded = true;
		private FigletSize _size = FigletSize.Default;
		private FigletFont? _customFont;
		private string? _fontPath;
		private WrapMode _wrapMode = WrapMode.NoWrap;

		// Cache the loaded font to avoid re-parsing on every render
		private FigletFont? _cachedFont;
		private FigletSize _cachedFontSize;
		private string? _cachedFontPath;

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

				// When wrapping is enabled, the control fills available width
				if (_wrapMode != WrapMode.NoWrap)
					return null;

				var font = GetFont();
				var lines = FigletRenderer.Render(_text, font);
				int maxWidth = 0;
				foreach (var line in lines)
				{
					if (line.Length > maxWidth) maxWidth = line.Length;
				}
				return maxWidth + Margin.Left + Margin.Right;
			}
		}

		/// <summary>
		/// Gets or sets the color of the FIGlet text.
		/// </summary>
		public Color? Color
		{
			get => _color;
			set => SetProperty(ref _color, value);
		}

		/// <summary>
		/// Gets or sets the text to render as FIGlet ASCII art.
		/// </summary>
		public string? Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}

		/// <summary>
		/// Gets or sets whether the right side should be padded.
		/// </summary>
		public bool RightPadded
		{
			get => _rightPadded;
			set => SetProperty(ref _rightPadded, value);
		}

		/// <summary>
		/// Gets or sets the FIGlet text size (uses embedded fonts).
		/// </summary>
		public FigletSize Size
		{
			get => _size;
			set { if (SetProperty(ref _size, value)) InvalidateFontCache(); }
		}

		/// <summary>
		/// Gets or sets a custom FigletFont. Takes precedence over Size.
		/// </summary>
		public FigletFont? CustomFont
		{
			get => _customFont;
			set { if (SetProperty(ref _customFont, value)) InvalidateFontCache(); }
		}

		/// <summary>
		/// Gets or sets the name of a custom .flf font file (without extension).
		/// The font must be located in the 'fonts' directory relative to the application base directory.
		/// Takes precedence over Size but lower than CustomFont.
		/// </summary>
		public string? FontPath
		{
			get => _fontPath;
			set { if (SetProperty(ref _fontPath, value)) InvalidateFontCache(); }
		}

		/// <summary>
		/// Gets or sets the wrap mode for FIGlet text when it exceeds available width.
		/// </summary>
		public WrapMode WrapMode
		{
			get => _wrapMode;
			set => SetProperty(ref _wrapMode, value);
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (string.IsNullOrEmpty(_text))
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			int width = ContentWidth ?? 0;

			var font = GetFont();
			var lines = FigletRenderer.Render(_text, font);
			int height = lines.Count + Margin.Top + Margin.Bottom;

			return new System.Drawing.Size(width, height);
		}

		/// <summary>
		/// Sets the color of the FIGlet text.
		/// </summary>
		/// <param name="color">The color to apply to the text.</param>
		public void SetColor(Color color)
		{
			Color = color;
		}

		/// <summary>
		/// Sets the text to render as FIGlet ASCII art.
		/// </summary>
		/// <param name="text">The text to display.</param>
		public void SetText(string text)
		{
			Text = text;
		}

		private void InvalidateFontCache()
		{
			_cachedFont = null;
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
				// Check cache
				if (_cachedFont != null && _cachedFontPath == _fontPath)
					return _cachedFont;

				try
				{
					// Security: Validate font path to prevent path traversal attacks
					string normalizedInput = _fontPath.Replace('\\', '/');
					if (normalizedInput.Contains("../") || normalizedInput.Contains("..\\") ||
					    Path.IsPathFullyQualified(_fontPath))
					{
						throw new ArgumentException($"Invalid font path: path traversal detected in '{_fontPath}'");
					}

					string fontsDir = Path.Combine(AppContext.BaseDirectory, "fonts");
					string fontFileName = _fontPath.EndsWith(".flf") ? _fontPath : _fontPath + ".flf";
					string safePath = Path.GetFullPath(Path.Combine(fontsDir, fontFileName));

					string normalizedFontsDir = Path.GetFullPath(fontsDir);
					if (!safePath.StartsWith(normalizedFontsDir + Path.DirectorySeparatorChar) &&
					    safePath != normalizedFontsDir)
					{
						throw new ArgumentException($"Invalid font path: path traversal detected in '{_fontPath}'");
					}

					if (File.Exists(safePath))
					{
						using var stream = File.OpenRead(safePath);
						_cachedFont = FigletFont.Load(stream);
						_cachedFontPath = _fontPath;
						return _cachedFont;
					}
				}
				catch (ArgumentException)
				{
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
		private FigletFont GetFontForSize(FigletSize size)
		{
			// Check cache
			if (_cachedFont != null && _cachedFontSize == size && _cachedFontPath == null && _customFont == null)
				return _cachedFont;

			var font = size switch
			{
				FigletSize.Small => LoadEmbeddedFont("small.flf"),
				FigletSize.Default => LoadEmbeddedFont("standard.flf"),
				FigletSize.Large => LoadEmbeddedFont("banner.flf"),
				_ => LoadEmbeddedFont("standard.flf")
			};

			_cachedFont = font;
			_cachedFontSize = size;
			_cachedFontPath = null;
			return font;
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
				// Fall back to a minimal font if embedded font not found
			}

			// Create a minimal fallback font
			return CreateFallbackFont();
		}

		private static FigletFont CreateFallbackFont()
		{
			// Create a trivial 1-height font where each character is just itself
			var fontData = "flf2a$ 1 1 1 0 0\n";
			for (int c = 32; c <= 126; c++)
			{
				fontData += (char)c + "@@\n";
			}
			using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fontData));
			return FigletFont.Load(stream);
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

			var font = GetFont();
			int targetWidth = Width ?? constraints.MaxWidth - Margin.Left - Margin.Right;

			List<string> lines;
			if (_wrapMode != WrapMode.NoWrap)
				lines = FigletRenderer.RenderWrapped(_text, font, targetWidth, _wrapMode);
			else
				lines = FigletRenderer.Render(_text, font, targetWidth);

			int maxWidth = 0;
			foreach (var line in lines)
			{
				if (line.Length > maxWidth) maxWidth = line.Length;
			}

			int width = maxWidth + Margin.Left + Margin.Right;
			int height = lines.Count + Margin.Top + Margin.Bottom;

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
			bool preserveBg = Container?.HasGradientBackground ?? false;
			int targetWidth = bounds.Width - Margin.Left - Margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor, preserveBg);

			if (!string.IsNullOrEmpty(_text))
			{
				var font = GetFont();
				int figletWidth = Width ?? targetWidth;

				// Map HorizontalAlignment to TextJustification for the renderer
				var justification = HorizontalAlignment switch
				{
					HorizontalAlignment.Center => TextJustification.Center,
					HorizontalAlignment.Right => TextJustification.Right,
					_ => TextJustification.Left
				};

				List<string> renderedLines;
				if (_wrapMode != WrapMode.NoWrap)
					renderedLines = FigletRenderer.RenderWrappedJustified(_text, font, figletWidth, _wrapMode, justification);
				else
					renderedLines = FigletRenderer.RenderJustified(_text, font, figletWidth, justification);

				int figletHeight = renderedLines.Count;
				int availableHeight = bounds.Height - Margin.Top - Margin.Bottom;

				for (int i = 0; i < Math.Min(figletHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (Margin.Left > 0)
						{
							ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, Margin.Left, 1), fgColor, bgColor, preserveBg);
						}

						// Write non-space characters to avoid overwriting shadow
						var plainText = renderedLines[i];
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
							ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), fgColor, bgColor, preserveBg);
						}
					}
				}

				// Fill any remaining height after FIGlet content
				for (int y = startY + figletHeight; y < bounds.Bottom - Margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, bgColor, preserveBg);
					}
				}
			}

			// Fill bottom margin
			for (int y = bounds.Bottom - Margin.Bottom; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, bounds.Width, 1), fgColor, bgColor, preserveBg);
				}
			}
		}

		#endregion
	}
}
