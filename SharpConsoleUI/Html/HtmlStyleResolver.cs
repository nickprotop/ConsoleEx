// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using Spectre.Console;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Resolved style properties for an HTML element.
	/// </summary>
	public struct ResolvedStyle
	{
		public Color Foreground;
		public Color Background;
		public TextDecoration Decorations;
		public TextAlignment Alignment;
		public bool IsHidden;
		public bool PreserveWhitespace;
		public int MarginTop;
		public int MarginBottom;
		public int MarginLeft;
		public int MarginRight;
		public int PaddingTop;
		public int PaddingBottom;
		public int PaddingLeft;
		public int PaddingRight;
		public bool HasBorder;
		public string? DisplayGrid;
		public string? GridTemplateColumns;
		public int GridGap;
		public int? ExplicitWidth;
	}

	/// <summary>
	/// Resolves CSS styles from AngleSharp elements into terminal-friendly ResolvedStyle values.
	/// </summary>
	public static class HtmlStyleResolver
	{
		private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
		{
			["red"] = Color.Red,
			["green"] = Color.Green,
			["blue"] = Color.Blue,
			["white"] = Color.White,
			["black"] = Color.Black,
			["yellow"] = Color.Yellow,
			["cyan"] = Color.Cyan1,
			["magenta"] = Color.Magenta1,
			["gray"] = Color.Grey,
			["grey"] = Color.Grey,
			["orange"] = Color.Orange1,
			["purple"] = Color.Purple,
			["pink"] = Color.MistyRose1,
			["brown"] = Color.DarkOrange3,
			["navy"] = Color.Navy,
			["teal"] = Color.Teal,
			["silver"] = Color.Silver,
			["maroon"] = Color.Maroon,
			["olive"] = Color.Olive,
			["lime"] = Color.Lime,
			["aqua"] = Color.Aqua,
			["fuchsia"] = Color.Fuchsia,
		};

		/// <summary>
		/// Resolves the effective style for the given element.
		/// </summary>
		public static ResolvedStyle Resolve(IElement element, Color defaultFg, Color defaultBg)
		{
			var style = new ResolvedStyle
			{
				Foreground = defaultFg,
				Background = defaultBg,
				Decorations = TextDecoration.None,
				Alignment = TextAlignment.Left,
			};

			// Walk ancestors to accumulate inherited decorations from semantic tags
			style.Decorations = GetInheritedDecorations(element);

			// Read computed CSS (may throw on complex media queries without a render device)
			ICssStyleDeclaration? css;
			try
			{
				css = element.ComputeCurrentStyle();
			}
			catch
			{
				css = null;
			}

			if (css != null)
			{
				try
				{
				// Font weight → Bold
				var fontWeight = css.GetPropertyValue("font-weight");
				if (!string.IsNullOrEmpty(fontWeight))
				{
					if (fontWeight == "bold" || fontWeight == "bolder" ||
					    (int.TryParse(fontWeight, out var weight) && weight >= 700))
					{
						style.Decorations |= TextDecoration.Bold;
					}
				}

				// Font style → Italic
				var fontStyle = css.GetPropertyValue("font-style");
				if (fontStyle == "italic" || fontStyle == "oblique")
				{
					style.Decorations |= TextDecoration.Italic;
				}

				// Text decoration
				var textDec = css.GetPropertyValue("text-decoration");
				if (!string.IsNullOrEmpty(textDec))
				{
					if (textDec.Contains("underline", StringComparison.OrdinalIgnoreCase))
						style.Decorations |= TextDecoration.Underline;
					if (textDec.Contains("line-through", StringComparison.OrdinalIgnoreCase))
						style.Decorations |= TextDecoration.Strikethrough;
				}

				// Also check text-decoration-line (used by some browsers/parsers)
				var textDecLine = css.GetPropertyValue("text-decoration-line");
				if (!string.IsNullOrEmpty(textDecLine))
				{
					if (textDecLine.Contains("underline", StringComparison.OrdinalIgnoreCase))
						style.Decorations |= TextDecoration.Underline;
					if (textDecLine.Contains("line-through", StringComparison.OrdinalIgnoreCase))
						style.Decorations |= TextDecoration.Strikethrough;
				}

				// Color → Foreground
				var color = css.GetPropertyValue("color");
				if (!string.IsNullOrEmpty(color))
				{
					var parsed = ParseCssColor(color);
					if (parsed.HasValue)
						style.Foreground = parsed.Value;
				}

				// Background color
				var bgColor = css.GetPropertyValue("background-color");
				if (!string.IsNullOrEmpty(bgColor))
				{
					var parsed = ParseCssColor(bgColor);
					if (parsed.HasValue)
						style.Background = parsed.Value;
				}

				// Text alignment
				var textAlign = css.GetPropertyValue("text-align");
				if (!string.IsNullOrEmpty(textAlign))
				{
					style.Alignment = textAlign.ToLowerInvariant() switch
					{
						"center" => TextAlignment.Center,
						"right" => TextAlignment.Right,
						_ => TextAlignment.Left,
					};
				}

				// Display
				var display = css.GetPropertyValue("display");
				if (display == "none")
				{
					style.IsHidden = true;
				}
				else if (display == "grid")
				{
					style.DisplayGrid = "grid";
				}

				// White space
				var whiteSpace = css.GetPropertyValue("white-space");
				if (whiteSpace == "pre" || whiteSpace == "pre-wrap" || whiteSpace == "pre-line")
				{
					style.PreserveWhitespace = true;
				}

				// Margins
				style.MarginTop = ParsePxToLines(css.GetPropertyValue("margin-top"));
				style.MarginBottom = ParsePxToLines(css.GetPropertyValue("margin-bottom"));
				style.MarginLeft = ParsePxToChars(css.GetPropertyValue("margin-left"));
				style.MarginRight = ParsePxToChars(css.GetPropertyValue("margin-right"));

				// Padding
				style.PaddingTop = ParsePxToLines(css.GetPropertyValue("padding-top"));
				style.PaddingBottom = ParsePxToLines(css.GetPropertyValue("padding-bottom"));
				style.PaddingLeft = ParsePxToChars(css.GetPropertyValue("padding-left"));
				style.PaddingRight = ParsePxToChars(css.GetPropertyValue("padding-right"));

				// Border
				var border = css.GetPropertyValue("border");
				var borderStyle = css.GetPropertyValue("border-style");
				if (!string.IsNullOrEmpty(border) && !border.Contains("none") && !border.Contains("0"))
				{
					style.HasBorder = true;
				}
				else if (!string.IsNullOrEmpty(borderStyle) && borderStyle != "none")
				{
					style.HasBorder = true;
				}

				// Grid template columns
				var gridCols = css.GetPropertyValue("grid-template-columns");
				if (!string.IsNullOrEmpty(gridCols))
				{
					style.GridTemplateColumns = gridCols;
				}

				// Grid gap
				var gap = css.GetPropertyValue("gap");
				if (!string.IsNullOrEmpty(gap))
				{
					style.GridGap = ParsePxToChars(gap);
				}

				// Width
				var width = css.GetPropertyValue("width");
				if (!string.IsNullOrEmpty(width))
				{
					var w = ParsePxToChars(width);
					if (w > 0)
						style.ExplicitWidth = w;
				}
				}
				catch
				{
					// AngleSharp.Css can throw NRE on complex/malformed CSS properties — skip styling for this element
				}
			}

			return style;
		}

		/// <summary>
		/// Walks ancestor elements to accumulate text decorations from semantic HTML tags.
		/// </summary>
		private static TextDecoration GetInheritedDecorations(IElement element)
		{
			var decorations = TextDecoration.None;
			INode? current = element;

			while (current is IElement el)
			{
				var tag = el.LocalName.ToLowerInvariant();
				decorations |= tag switch
				{
					"b" or "strong" => TextDecoration.Bold,
					"i" or "em" => TextDecoration.Italic,
					"u" => TextDecoration.Underline,
					"s" or "del" or "strike" => TextDecoration.Strikethrough,
					_ => TextDecoration.None,
				};
				current = el.ParentElement;
			}

			return decorations;
		}

		/// <summary>
		/// Parses a CSS color string into a Spectre.Console Color.
		/// Supports rgb(), rgba(), hex (#fff, #ffffff), and named colors.
		/// </summary>
		public static Color? ParseCssColor(string cssColor)
		{
			if (string.IsNullOrWhiteSpace(cssColor))
				return null;

			cssColor = cssColor.Trim();

			if (cssColor == "transparent")
				return null;

			// Try named colors first
			if (NamedColors.TryGetValue(cssColor, out var namedColor))
				return namedColor;

			// Handle rgb() and rgba()
			if (cssColor.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
			{
				return ParseRgbColor(cssColor);
			}

			// Handle hex colors
			if (cssColor.StartsWith('#'))
			{
				return ParseHexColor(cssColor);
			}

			return null;
		}

		private static Color? ParseRgbColor(string cssColor)
		{
			// Extract content between parentheses
			var start = cssColor.IndexOf('(');
			var end = cssColor.LastIndexOf(')');
			if (start < 0 || end < 0 || end <= start)
				return null;

			var content = cssColor.Substring(start + 1, end - start - 1);
			var parts = content.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length < 3)
				return null;

			if (byte.TryParse(parts[0].Trim(), out var r) &&
			    byte.TryParse(parts[1].Trim(), out var g) &&
			    byte.TryParse(parts[2].Trim(), out var b))
			{
				return new Color(r, g, b);
			}

			return null;
		}

		private static Color? ParseHexColor(string hex)
		{
			hex = hex.TrimStart('#');

			if (hex.Length == 3)
			{
				// #rgb → #rrggbb
				hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
			}

			if (hex.Length == 6 &&
			    byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
			    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
			    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
			{
				return new Color(r, g, b);
			}

			return null;
		}

		/// <summary>
		/// Parses a CSS length value to terminal lines (vertical). px/16, em rounds to int.
		/// </summary>
		public static int ParsePxToLines(string? value)
		{
			if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "auto")
				return 0;

			value = value.Trim();

			if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 2), System.Globalization.NumberStyles.Float,
					    System.Globalization.CultureInfo.InvariantCulture, out var em))
				{
					return (int)Math.Round(em);
				}
			}
			else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 2), System.Globalization.NumberStyles.Float,
					    System.Globalization.CultureInfo.InvariantCulture, out var px))
				{
					return (int)Math.Round(px / 16.0);
				}
			}
			else if (double.TryParse(value, System.Globalization.NumberStyles.Float,
				         System.Globalization.CultureInfo.InvariantCulture, out var raw))
			{
				// Bare number treated as px
				return (int)Math.Round(raw / 16.0);
			}

			return 0;
		}

		/// <summary>
		/// Parses a CSS length value to terminal characters (horizontal). px/8, em*2.
		/// </summary>
		public static int ParsePxToChars(string? value)
		{
			if (string.IsNullOrWhiteSpace(value) || value == "0" || value == "auto")
				return 0;

			value = value.Trim();

			if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 2), System.Globalization.NumberStyles.Float,
					    System.Globalization.CultureInfo.InvariantCulture, out var em))
				{
					return (int)Math.Round(em * HtmlConstants.EmToCharRatio);
				}
			}
			else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 2), System.Globalization.NumberStyles.Float,
					    System.Globalization.CultureInfo.InvariantCulture, out var px))
				{
					return (int)Math.Round(px / HtmlConstants.PxToCharRatio);
				}
			}
			else if (double.TryParse(value, System.Globalization.NumberStyles.Float,
				         System.Globalization.CultureInfo.InvariantCulture, out var raw))
			{
				return (int)Math.Round(raw / HtmlConstants.PxToCharRatio);
			}

			return 0;
		}
	}
}
