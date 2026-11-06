// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using System.Runtime.CompilerServices;
using AngleSharp.Css;
using AngleSharp.Css.Dom;
using AngleSharp.Dom;
using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Resolved style properties for an HTML element.
	/// </summary>
	public struct ResolvedStyle
	{
		public Color Foreground;

		/// <summary>
		/// True when <see cref="Foreground"/> came from the page rather than from the caller's default.
		/// </summary>
		/// <remarks>
		/// Lets a link keep a colour the page chose for it while still defaulting
		/// to the theme's link colour when the page is silent. Without the
		/// distinction an anchor is always repainted, so a page that styles its
		/// own links - and whose background may have been adopted to match - loses
		/// the one colour it cared most about.
		/// </remarks>
		public bool ForegroundFromPage;
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

		// The CSS-derived part of an element's style depends only on the element, never on
		// the caller's default colors, so it is computed once per element and reused by every
		// later layout pass (image batches, resizes). Colors stay nullable so each caller's
		// defaults still apply where the CSS sets none.
		private readonly struct CachedStyle
		{
			public readonly ICssStyleDeclaration? Declaration;
			public readonly ResolvedStyle Style;
			public readonly Color? Foreground;
			public readonly Color? Background;

			public CachedStyle(ICssStyleDeclaration? declaration, ResolvedStyle style, Color? foreground, Color? background)
			{
				Declaration = declaration;
				Style = style;
				Foreground = foreground;
				Background = background;
			}
		}

		private sealed class DocumentStyles
		{
			public IStyleCollection? Styles;
			public readonly Dictionary<IElement, CachedStyle> Elements = new();
		}

		// Keyed weakly on the document, so the cache lives exactly as long as the parsed DOM.
		// The DOM must not change after its elements are first resolved; HtmlLayoutEngine only
		// rewrites URLs right after parsing, before any layout.
		private static readonly ConditionalWeakTable<IDocument, DocumentStyles> _documentStyles = new();

		// Properties whose cascaded value may still hold var()/calc(), which only the full
		// (much slower) computed-style path can resolve.
		private static readonly string[] UnresolvedCheckProperties =
		{
			"color", "background-color", "display", "border", "border-style", "width",
			"margin-top", "margin-bottom", "margin-left", "margin-right",
			"padding-top", "padding-bottom", "padding-left", "padding-right",
		};

		/// <summary>
		/// Resolves the effective style for the given element.
		/// </summary>
		public static ResolvedStyle Resolve(IElement element, Color defaultFg, Color defaultBg)
		{
			var cached = GetCachedStyle(element);
			var style = cached.Style;

			style.Background = cached.Background ?? defaultBg;

			// A page picks its text colours for ITS background, and a terminal
			// rarely has that background. Hacker News sets a:link to black, which
			// is right on its cream page and invisible on a dark terminal: every
			// story title vanished while the page looked merely empty.
			//
			// The page's choice is still honoured - the colour arrives as asked
			// whenever it can be read - and only lifted to the floor when it
			// cannot. EnsureContrastRatio moves it the shortest distance that
			// works and keeps its hue, so a dark red link comes back a readable
			// red rather than the theme's accent.
			// Checked whether the colour came from the page or from the terminal.
			// Only the page's own colours needed it at first, but once a page can
			// supply the BACKGROUND too the default foreground is just as exposed:
			// the terminal's white on a page's cream surface reads 1.09:1, which
			// is the same invisible text arrived at from the other side.
			style.ForegroundFromPage = cached.Foreground.HasValue;
			style.Foreground = PaletteColors.EnsureContrastRatio(
				cached.Foreground ?? defaultFg, style.Background, PaletteColors.TextContrast);

			return style;
		}

		private static CachedStyle GetCachedStyle(IElement element)
		{
			var document = element.Owner;
			if (document == null)
				return ComputeStyle(element, ComputeCurrentStyleSafe(element));

			var docStyles = _documentStyles.GetValue(document, _ => new DocumentStyles());
			lock (docStyles)
			{
				if (docStyles.Elements.TryGetValue(element, out var cached))
					return cached;

				docStyles.Styles ??= CreateStyleCollection(document);

				// Compute top-down from the nearest cached ancestor: each element inherits
				// from its parent's declaration instead of AngleSharp re-cascading the whole
				// ancestor chain for every element (that walk made large pages take seconds).
				var pending = new Stack<IElement>();
				for (var e = element; e != null && !docStyles.Elements.ContainsKey(e); e = e.ParentElement)
					pending.Push(e);

				while (pending.Count > 0)
				{
					var e = pending.Pop();
					ICssStyleDeclaration? parentDeclaration = null;
					if (e.ParentElement != null && docStyles.Elements.TryGetValue(e.ParentElement, out var parentStyle))
						parentDeclaration = parentStyle.Declaration;

					var declaration = docStyles.Styles != null
						? ComputeDeclaration(docStyles.Styles, e, parentDeclaration)
						: ComputeCurrentStyleSafe(e);
					docStyles.Elements[e] = ComputeStyle(e, declaration);
				}

				return docStyles.Elements[element];
			}
		}

		/// <summary>
		/// The render device used when the browsing context registers none.
		/// </summary>
		/// <remarks>
		/// Immutable and shared: AngleSharp only reads it. Device and viewport are
		/// separate properties and both are set, because media queries and
		/// relative lengths read the viewport pair.
		/// </remarks>
		private static readonly DefaultRenderDevice NominalDevice = new()
		{
			DeviceWidth = (int)HtmlConstants.CssViewportWidthPx,
			DeviceHeight = (int)HtmlConstants.CssViewportHeightPx,
			ViewPortWidth = (int)HtmlConstants.CssViewportWidthPx,
			ViewPortHeight = (int)HtmlConstants.CssViewportHeightPx,
			FontSize = (int)HtmlConstants.CssRootFontSizePx
		};

		private static IStyleCollection? CreateStyleCollection(IDocument document)
		{
			try
			{
				var window = document.DefaultView;
				if (window == null)
					return null;
				// A bare DefaultRenderDevice reports a zero viewport, and a zero
				// viewport makes every (min-width: N) false and every
				// (max-width: N) true. The synchronous context registers no
				// device, so this fallback is what the common path uses: it has
				// to state the same nominal page the engine states everywhere
				// else, or a page laid out before its CSS arrives gets a phone
				// layout and the one laid out after gets a desktop one.
				var device = document.Context?.GetService<IRenderDevice>() ?? NominalDevice;
				return window.GetStyleCollection(device);
			}
			catch
			{
				return null;
			}
		}

		private static ICssStyleDeclaration? ComputeDeclaration(IStyleCollection styles, IElement element, ICssStyleDeclaration? parent)
		{
			ICssStyleDeclaration? cascaded;
			try
			{
				cascaded = styles.ComputeCascadedStyle(element, parent!);
			}
			catch
			{
				cascaded = null;
			}

			if (cascaded != null && !HasUnresolvedValues(cascaded))
				return cascaded;

			try
			{
				return styles.ComputeDeclarations(element);
			}
			catch
			{
				return cascaded;
			}
		}

		private static bool HasUnresolvedValues(ICssStyleDeclaration css)
		{
			try
			{
				foreach (var property in UnresolvedCheckProperties)
				{
					var value = css.GetPropertyValue(property);
					if (value.Contains("var(", StringComparison.OrdinalIgnoreCase) ||
						value.Contains("calc(", StringComparison.OrdinalIgnoreCase))
						return true;
				}
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static ICssStyleDeclaration? ComputeCurrentStyleSafe(IElement element)
		{
			// May throw on complex media queries without a render device
			try
			{
				return element.ComputeCurrentStyle();
			}
			catch
			{
				return null;
			}
		}

		private static CachedStyle ComputeStyle(IElement element, ICssStyleDeclaration? css)
		{
			var style = new ResolvedStyle
			{
				Decorations = TextDecoration.None,
				Alignment = TextAlignment.Left,
			};
			Color? foreground = null;
			Color? background = null;

			// Walk ancestors to accumulate inherited decorations from semantic tags
			style.Decorations = GetInheritedDecorations(element);

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
						foreground = ParseCssColor(color);
					}

					// Background color
					var bgColor = css.GetPropertyValue("background-color");
					if (!string.IsNullOrEmpty(bgColor))
					{
						background = ParseCssColor(bgColor);
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

			return new CachedStyle(css, style, foreground, background);
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

		/// <summary>
		/// Parses a CSS length to CSS pixels, leaving the conversion to columns to the caller.
		/// </summary>
		/// <param name="value">A CSS length: <c>220px</c>, <c>12em</c>, <c>50%</c>, or a bare number.</param>
		/// <returns>The length in CSS pixels, or null if it is absent, <c>auto</c>, or unparseable.</returns>
		/// <remarks>
		/// <para>
		/// Images need the pixels, not the columns. <see cref="ParsePxToChars"/>
		/// bakes in a fixed pixels-per-column ratio, which is right for a margin
		/// (a margin IS a few columns however wide the window is) and wrong for an
		/// image, whose size is relative to the page it was designed for. Dividing
		/// first and rescaling after loses the precision and hides the decision.
		/// </para>
		/// <para>
		/// A percentage resolves against the same content column the caller then
		/// divides by, so <c>width: 50%</c> comes back as half of it and goes on
		/// to claim half the columns - which is what the page asked for. The two
		/// have to agree: resolving a percentage against one frame and measuring
		/// it against a narrower one would silently inflate every percentage
		/// width on the page.
		/// </para>
		/// </remarks>
		public static double? ParseCssPixels(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			value = value.Trim();
			if (value.Equals("auto", StringComparison.OrdinalIgnoreCase) || value == "0")
				return null;

			var invariant = System.Globalization.CultureInfo.InvariantCulture;
			const System.Globalization.NumberStyles Float = System.Globalization.NumberStyles.Float;

			if (value.EndsWith("%", StringComparison.Ordinal))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 1), Float, invariant, out var pct) && pct > 0)
					return HtmlConstants.CssContentWidthPx * Math.Min(100.0, pct) / 100.0;
				return null;
			}

			if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase)
				|| value.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
			{
				int unitLength = value.EndsWith("rem", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
				if (double.TryParse(value.AsSpan(0, value.Length - unitLength), Float, invariant, out var em) && em > 0)
					return em * HtmlConstants.CssRootFontSizePx;
				return null;
			}

			if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
			{
				if (double.TryParse(value.AsSpan(0, value.Length - 2), Float, invariant, out var px) && px > 0)
					return px;
				return null;
			}

			// A bare number in an HTML length context is pixels.
			if (double.TryParse(value, Float, invariant, out var bare) && bare > 0)
				return bare;

			return null;
		}
	}
}
