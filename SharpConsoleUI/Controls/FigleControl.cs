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
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Shadow rendering style for FIGlet text.
	/// </summary>
	public enum ShadowStyle
	{
		/// <summary>No shadow</summary>
		None,

		/// <summary>Drop shadow to the bottom-right</summary>
		DropShadow,

		/// <summary>Outline around the text</summary>
		Outline,

		/// <summary>3D extrusion effect</summary>
		Extrude3D
	}

	/// <summary>
	/// FIGlet text size based on embedded fonts.
	/// </summary>
	public enum FigletSize
	{
		/// <summary>Small font (~4 lines height)</summary>
		Small,

		/// <summary>Medium/Standard font (~6 lines height)</summary>
		Medium,

		/// <summary>Large/Banner font (~8 lines height)</summary>
		Large,

		/// <summary>Custom font provided by user</summary>
		Custom
	}

	/// <summary>
	/// A control that renders text using FIGlet ASCII art fonts.
	/// Wraps the Spectre.Console FigletText component for large decorative text display.
	/// </summary>
	public class FigleControl : IWindowControl, IDOMPaintable
	{
		private Color? _color;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private string? _text;
		private bool _visible = true;
		private int? _width;
		private bool _rightPadded = true;
		private FigletSize _size = FigletSize.Medium;
		private FigletFont? _customFont;
		private string? _fontPath;
		private ShadowStyle _shadowStyle = ShadowStyle.None;
		private Color? _shadowColor;
		private int _shadowOffsetX = 1;
		private int _shadowOffsetY = 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="FigleControl"/> class.
		/// </summary>
		public FigleControl()
		{
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				if (string.IsNullOrEmpty(_text)) return _margin.Left + _margin.Right;

				// Calculate width by rendering to get actual FIGlet dimensions
				FigletText figletText = new FigletText(GetFont(), _text);
				var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
				var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? 80, null, bgColor);

				int maxLength = 0;
				foreach (var line in content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength + _margin.Left + _margin.Right;
			}
		}

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <summary>
		/// Gets or sets the color of the FIGlet text.
		/// </summary>
		public Color? Color
		{ get => _color; set { _color = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public IContainer? Container { get; set; }

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets or sets the text to render as FIGlet ASCII art.
		/// </summary>
		public string? Text
		{ get => _text; set { _text = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public bool Visible
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

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
		/// Gets or sets the path to a custom .flf font file.
		/// Takes precedence over Size but lower than CustomFont.
		/// </summary>
		public string? FontPath
		{
			get => _fontPath;
			set => PropertySetterHelper.SetProperty(ref _fontPath, value, Container);
		}

		/// <summary>
		/// Gets or sets the shadow style.
		/// </summary>
		public ShadowStyle ShadowStyle
		{
			get => _shadowStyle;
			set => PropertySetterHelper.SetEnumProperty(ref _shadowStyle, value, Container);
		}

		/// <summary>
		/// Gets or sets the shadow color (defaults to darker version of background).
		/// </summary>
		public Color? ShadowColor
		{
			get => _shadowColor;
			set => PropertySetterHelper.SetProperty(ref _shadowColor, value, Container);
		}

		/// <summary>
		/// Gets or sets the horizontal shadow offset (pixels).
		/// </summary>
		public int ShadowOffsetX
		{
			get => _shadowOffsetX;
			set => PropertySetterHelper.SetProperty(ref _shadowOffsetX, value, Container);
		}

		/// <summary>
		/// Gets or sets the vertical shadow offset (pixels).
		/// </summary>
		public int ShadowOffsetY
		{
			get => _shadowOffsetY;
			set => PropertySetterHelper.SetProperty(ref _shadowOffsetY, value, Container);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			if (string.IsNullOrEmpty(_text))
				return new System.Drawing.Size(_margin.Left + _margin.Right, _margin.Top + _margin.Bottom);

			// For Figlet text, we need to render to get the size
			FigletText figletText = new FigletText(GetFont(), _text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, _width ?? 80, null, bgColor);

			int maxWidth = content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line));
			int width = maxWidth + _margin.Left + _margin.Right;
			int height = content.Count + _margin.Top + _margin.Bottom;

			// Add shadow bounds
			if (_shadowStyle != ShadowStyle.None)
			{
				if (_shadowStyle == ShadowStyle.DropShadow || _shadowStyle == ShadowStyle.Extrude3D)
				{
					width += Math.Abs(_shadowOffsetX);
					height += Math.Abs(_shadowOffsetY);
				}
				else if (_shadowStyle == ShadowStyle.Outline)
				{
					width += 2;
					height += 2;
				}
			}

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

			if (!string.IsNullOrEmpty(_fontPath) && File.Exists(_fontPath))
			{
				try
				{
					using var stream = File.OpenRead(_fontPath);
					return FigletFont.Load(stream);
				}
				catch
				{
					// Fall through to size-based
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
				FigletSize.Medium => LoadEmbeddedFont("standard.flf"),
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

		/// <summary>
		/// Renders shadow based on the shadow style.
		/// </summary>
		private void RenderShadow(CharacterBuffer buffer, List<string> renderedContent,
			int startX, int startY, LayoutRect bounds, LayoutRect clipRect, Color fgColor, Color bgColor)
		{
			if (_shadowStyle == ShadowStyle.None) return;

			// Default shadow color: dark grey instead of darkened background (which might already be black)
			var shadowColor = _shadowColor ?? new Color(64, 64, 64);

			switch (_shadowStyle)
			{
				case ShadowStyle.DropShadow:
					RenderDropShadow(buffer, renderedContent, startX, startY, bounds, clipRect, shadowColor, bgColor);
					break;

				case ShadowStyle.Outline:
					RenderOutline(buffer, renderedContent, startX, startY, bounds, clipRect, shadowColor, bgColor);
					break;

				case ShadowStyle.Extrude3D:
					RenderExtrude3D(buffer, renderedContent, startX, startY, bounds, clipRect, shadowColor, bgColor);
					break;
			}
		}

		/// <summary>
		/// Renders drop shadow effect.
		/// </summary>
		private void RenderDropShadow(CharacterBuffer buffer, List<string> renderedContent,
			int startX, int startY, LayoutRect bounds, LayoutRect clipRect, Color shadowColor, Color bgColor)
		{
			int shadowX = startX + _shadowOffsetX;
			int shadowY = startY + _shadowOffsetY;

			for (int i = 0; i < renderedContent.Count; i++)
			{
				int paintY = shadowY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					var cells = AnsiParser.Parse(renderedContent[i], shadowColor, bgColor);
					buffer.WriteCellsClipped(shadowX, paintY, cells, clipRect);
				}
			}
		}

		/// <summary>
		/// Renders outline effect.
		/// </summary>
		private void RenderOutline(CharacterBuffer buffer, List<string> renderedContent,
			int startX, int startY, LayoutRect bounds, LayoutRect clipRect, Color shadowColor, Color bgColor)
		{
			// Render in 8 directions around the text
			int[] offsetsX = { -1, 0, 1, -1, 1, -1, 0, 1 };
			int[] offsetsY = { -1, -1, -1, 0, 0, 1, 1, 1 };

			for (int dir = 0; dir < 8; dir++)
			{
				int outlineX = startX + offsetsX[dir];
				int outlineY = startY + offsetsY[dir];

				for (int i = 0; i < renderedContent.Count; i++)
				{
					int paintY = outlineY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						var cells = AnsiParser.Parse(renderedContent[i], shadowColor, bgColor);
						buffer.WriteCellsClipped(outlineX, paintY, cells, clipRect);
					}
				}
			}
		}

		/// <summary>
		/// Renders 3D extrusion effect.
		/// </summary>
		private void RenderExtrude3D(CharacterBuffer buffer, List<string> renderedContent,
			int startX, int startY, LayoutRect bounds, LayoutRect clipRect, Color shadowColor, Color bgColor)
		{
			// Render multiple layers with progressively darker colors
			int depth = Math.Max(Math.Abs(_shadowOffsetX), Math.Abs(_shadowOffsetY));
			int dirX = Math.Sign(_shadowOffsetX);
			int dirY = Math.Sign(_shadowOffsetY);

			for (int layer = depth; layer > 0; layer--)
			{
				float darkenAmount = (float)layer / depth;
				var layerColor = DarkenColor(shadowColor, darkenAmount);

				int layerX = startX + (dirX * layer);
				int layerY = startY + (dirY * layer);

				for (int i = 0; i < renderedContent.Count; i++)
				{
					int paintY = layerY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						var cells = AnsiParser.Parse(renderedContent[i], layerColor, bgColor);
						buffer.WriteCellsClipped(layerX, paintY, cells, clipRect);
					}
				}
			}
		}

		/// <summary>
		/// Darkens a color by a specified amount.
		/// </summary>
		private Color DarkenColor(Color color, float amount)
		{
			return new Color(
				(byte)(color.R * (1 - amount)),
				(byte)(color.G * (1 - amount)),
				(byte)(color.B * (1 - amount))
			);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			if (string.IsNullOrEmpty(_text))
			{
				return new LayoutSize(
					Math.Clamp(_margin.Left + _margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(_margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
				);
			}

			// For Figlet text, we need to render to get the size
			FigletText figletText = new FigletText(GetFont(), _text);
			var bgColor = Container?.BackgroundColor ?? Spectre.Console.Color.Black;
			int targetWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;
			var content = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, targetWidth, null, bgColor);

			int maxWidth = content.Max(line => AnsiConsoleHelper.StripAnsiStringLength(line));
			int width = maxWidth + _margin.Left + _margin.Right;
			int height = content.Count + _margin.Top + _margin.Bottom;

			// Add shadow bounds
			if (_shadowStyle != ShadowStyle.None)
			{
				if (_shadowStyle == ShadowStyle.DropShadow || _shadowStyle == ShadowStyle.Extrude3D)
				{
					width += Math.Abs(_shadowOffsetX);
					height += Math.Abs(_shadowOffsetY);
				}
				else if (_shadowStyle == ShadowStyle.Outline)
				{
					width += 2;  // Outline adds 1 pixel on each side
					height += 2;
				}
			}

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = Container?.BackgroundColor ?? defaultBg;
			var fgColor = _color ?? Container?.ForegroundColor ?? defaultFg;
			int targetWidth = bounds.Width - _margin.Left - _margin.Right;

			if (targetWidth <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			if (!string.IsNullOrEmpty(_text))
			{
				// Render the FIGlet text
				FigletText figletText = new FigletText(GetFont(), _text);
				figletText.Color = fgColor;
				figletText.Justification = _horizontalAlignment switch
				{
					HorizontalAlignment.Left => Justify.Left,
					HorizontalAlignment.Center => Justify.Center,
					HorizontalAlignment.Right => Justify.Right,
					_ => Justify.Left
				};

				int figletWidth = _width ?? targetWidth;
				var renderedContent = AnsiConsoleHelper.ConvertSpectreRenderableToAnsi(figletText, figletWidth, null, bgColor);

				int figletHeight = renderedContent.Count;
				int availableHeight = bounds.Height - _margin.Top - _margin.Bottom;

				// Render shadow first (so it appears behind the text)
				RenderShadow(buffer, renderedContent, startX, startY, bounds, clipRect, fgColor, bgColor);

				for (int i = 0; i < Math.Min(figletHeight, availableHeight); i++)
				{
					int paintY = startY + i;
					if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
					{
						// Fill left margin
						if (_margin.Left > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
						}

						// Parse and write the FIGlet line (Spectre handles justification)
						var cells = AnsiParser.Parse(renderedContent[i], fgColor, bgColor);
						buffer.WriteCellsClipped(startX, paintY, cells, clipRect);

						// Fill right margin
						if (_margin.Right > 0)
						{
							buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
						}
					}
				}

				// Fill any remaining height after FIGlet content
				for (int y = startY + figletHeight; y < bounds.Bottom - _margin.Bottom; y++)
				{
					if (y >= clipRect.Y && y < clipRect.Bottom)
					{
						buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
					}
				}
			}

			// Fill bottom margin
			for (int y = bounds.Bottom - _margin.Bottom; y < bounds.Bottom; y++)
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
