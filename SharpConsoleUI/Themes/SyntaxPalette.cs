// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Per-role colours used to render syntax-highlighted code. Each role maps to a family of
	/// TextMate scopes, so a single palette colours every supported language consistently.
	/// </summary>
	public sealed record SyntaxPalette
	{
		/// <summary>Colour for text matching no other role.</summary>
		public Color? Default { get; init; }

		/// <summary>Colour for language keywords (<c>if</c>, <c>class</c>, <c>return</c>).</summary>
		public Color? Keyword { get; init; }

		/// <summary>Colour for operators (<c>=</c>, <c>+</c>, <c>=&gt;</c>).</summary>
		public Color? Operator { get; init; }

		/// <summary>Colour for string and character literals.</summary>
		public Color? String { get; init; }

		/// <summary>Colour for numeric literals.</summary>
		public Color? Number { get; init; }

		/// <summary>Colour for comments.</summary>
		public Color? Comment { get; init; }

		/// <summary>Colour for type, class, struct, and interface names.</summary>
		public Color? Type { get; init; }

		/// <summary>Colour for function and method names.</summary>
		public Color? Function { get; init; }

		/// <summary>Colour for variable, field, and parameter names.</summary>
		public Color? Variable { get; init; }

		/// <summary>Colour for language constants (<c>true</c>, <c>null</c>).</summary>
		public Color? Constant { get; init; }

		/// <summary>Colour for markup tag names (HTML/XML elements).</summary>
		public Color? Tag { get; init; }

		/// <summary>Colour for markup attribute names.</summary>
		public Color? Attribute { get; init; }

		/// <summary>Colour for punctuation (braces, semicolons, commas).</summary>
		public Color? Punctuation { get; init; }

		/// <summary>Colour for text the grammar marks invalid.</summary>
		public Color? Invalid { get; init; }

		/// <summary>
		/// Builds a palette from a theme's base colours, so a theme that specifies no explicit
		/// syntax colours still renders readable, well-separated code.
		/// </summary>
		/// <param name="theme">The theme supplying base colours.</param>
		/// <returns>A palette with every role populated.</returns>
		public static SyntaxPalette DeriveFrom(ITheme theme)
		{
			// WindowBackgroundColor / WindowForegroundColor are non-nullable on ITheme.
			Color background = theme.WindowBackgroundColor;
			Color foreground = theme.WindowForegroundColor;
			bool dark = background.IsDark();

			// Hue anchors chosen for separation at terminal colour depth; lightened on dark
			// backgrounds and darkened on light ones so every role stays legible.
			Color Adapt(Color c) => dark ? c.Tint(TintAmount) : c.Shade(ShadeAmount);

			return new SyntaxPalette
			{
				Default = foreground,
				Keyword = Adapt(Color.DodgerBlue2),
				Operator = foreground,
				String = Adapt(Color.DarkSeaGreen),
				Number = Adapt(Color.Orange3),
				Comment = Adapt(Color.Grey),
				Type = Adapt(Color.MediumTurquoise),
				Function = Adapt(Color.Khaki3),
				Variable = Adapt(Color.LightSkyBlue1),
				Constant = Adapt(Color.Orange3),
				Tag = Adapt(Color.DodgerBlue2),
				Attribute = Adapt(Color.MediumTurquoise),
				Punctuation = foreground,
				Invalid = Color.Red,
			};
		}

		private const double TintAmount = 0.15;
		private const double ShadeAmount = 0.25;
	}
}
