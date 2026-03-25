// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

namespace SharpConsoleUI
{
	/// <summary>
	/// Represents a 24-bit RGB color. Drop-in replacement for Spectre.Console.Color
	/// with identical constructor, property, and named-color API surface.
	/// </summary>
	public readonly struct Color : IEquatable<Color>
	{
		/// <summary>RGB red component (0-255).</summary>
		public byte R { get; }

		/// <summary>RGB green component (0-255).</summary>
		public byte G { get; }

		/// <summary>RGB blue component (0-255).</summary>
		public byte B { get; }

		/// <summary>Alpha component (0 = fully transparent, 255 = fully opaque).</summary>
		public byte A { get; }

		/// <summary>
		/// True when this color was created via <c>default</c> or <see cref="Default"/>,
		/// meaning "inherit from theme/container".
		/// </summary>
		public bool IsDefault { get; }

		/// <summary>Creates a color from RGB components.</summary>
		public Color(byte r, byte g, byte b, byte a = 255)
		{
			R = r;
			G = g;
			B = b;
			A = a;
			IsDefault = false;
		}

		private Color(byte r, byte g, byte b, bool isDefault)
		{
			R = r;
			G = g;
			B = b;
			A = 255;
			IsDefault = isDefault;
		}

		#region Named Colors — Basic 16

		/// <summary>Sentinel: "no explicit color, inherit from context".</summary>
		public static Color Default => new(0, 0, 0, isDefault: true);

		/// <summary>Fully transparent — composites over whatever is already in the buffer.</summary>
		public static Color Transparent => new(0, 0, 0, 0);
		public static Color Black => new(0, 0, 0);
		public static Color Maroon => new(128, 0, 0);
		public static Color Green => new(0, 128, 0);
		public static Color Olive => new(128, 128, 0);
		public static Color Navy => new(0, 0, 128);
		public static Color Purple => new(128, 0, 128);
		public static Color Teal => new(0, 128, 128);
		public static Color Silver => new(192, 192, 192);
		public static Color Grey => new(128, 128, 128);
		public static Color Red => new(255, 0, 0);
		public static Color Lime => new(0, 255, 0);
		public static Color Yellow => new(255, 255, 0);
		public static Color Blue => new(0, 0, 255);
		public static Color Fuchsia => new(255, 0, 255);
		public static Color Aqua => new(0, 255, 255);
		public static Color White => new(255, 255, 255);

		#endregion

		#region Named Colors — Aliases

		public static Color Gray => Grey;
		public static Color Magenta => Fuchsia;
		public static Color Cyan => Aqua;
		public static Color DarkRed => Maroon;
		public static Color DarkGreen => Green;
		public static Color DarkYellow => Olive;
		public static Color DarkBlue => Navy;
		public static Color DarkMagenta => Purple;
		public static Color DarkCyan => Teal;

		#endregion

		#region Named Colors — Extended Palette

		// xterm-256 color values matching Spectre.Console exactly
		public static Color Orange1 => new(255, 175, 0);        // #214
		public static Color DarkOrange => new(255, 135, 0);     // #208
		public static Color IndianRed => new(175, 95, 95);      // #131
		public static Color HotPink => new(255, 95, 175);       // #205
		public static Color DeepPink1 => new(255, 0, 135);      // #198
		public static Color MediumOrchid => new(175, 95, 215);  // #134
		public static Color DarkViolet => new(135, 0, 215);     // #92
		public static Color BlueViolet => new(95, 0, 255);      // #57
		public static Color RoyalBlue1 => new(95, 95, 255);     // #63
		public static Color CornflowerBlue => new(95, 135, 255); // #69
		public static Color DodgerBlue1 => new(0, 135, 255);    // #33
		public static Color DeepSkyBlue1 => new(0, 175, 255);   // #39
		public static Color SteelBlue => new(95, 135, 175);     // #67
		public static Color CadetBlue => new(95, 175, 135);     // #72
		public static Color MediumTurquoise => new(95, 215, 215); // #80
		public static Color DarkTurquoise => new(0, 215, 215);  // #44
		public static Color LightSeaGreen => new(0, 175, 175);  // #37
		public static Color MediumSpringGreen => new(0, 255, 175); // #49
		public static Color SpringGreen1 => new(0, 255, 135);   // #48
		public static Color Chartreuse1 => new(135, 255, 0);    // #118
		public static Color GreenYellow => new(175, 255, 0);    // #154
		public static Color DarkOliveGreen1 => new(215, 255, 95); // #191
		public static Color PaleGreen1 => new(135, 255, 175);   // #121
		public static Color DarkSeaGreen => new(135, 175, 135); // #108
		public static Color MediumSeaGreen => new(95, 215, 135); // #78 (SeaGreen3)
		public static Color LightGreen => new(135, 255, 95);    // #119
		public static Color Khaki1 => new(255, 255, 135);       // #228
		public static Color DarkKhaki => new(175, 175, 95);     // #143
		public static Color DarkGoldenrod => new(175, 135, 0);  // #136
		public static Color Wheat1 => new(255, 255, 175);       // #229
		public static Color NavajoWhite1 => new(255, 215, 175); // #223
		public static Color MistyRose1 => new(255, 215, 215);   // #224
		public static Color LightCoral => new(255, 135, 135);   // #210
		public static Color Salmon1 => new(255, 135, 95);       // #209
		public static Color LightSalmon1 => new(255, 175, 135); // #216
		public static Color LightPink1 => new(255, 175, 175);   // #217
		public static Color Pink1 => new(255, 175, 215);        // #218
		public static Color Plum1 => new(255, 175, 255);        // #219
		public static Color Orchid => new(215, 95, 215);        // #170
		public static Color Violet => new(215, 135, 255);       // #177
		public static Color Thistle1 => new(255, 215, 255);     // #225
		public static Color SandyBrown => new(255, 175, 95);    // #215
		public static Color Tan => new(215, 175, 135);          // #180
		public static Color RosyBrown => new(175, 135, 135);    // #138
		public static Color PaleVioletRed => new(255, 135, 175); // #211 (PaleVioletRed1)
		public static Color MediumVioletRed => new(175, 0, 135); // #126
		public static Color MediumPurple => new(135, 135, 215); // #104
		public static Color MediumSlateBlue => new(95, 95, 175); // #61 (SlateBlue3)
		public static Color SlateBlue1 => new(135, 95, 255);    // #99
		public static Color LightSteelBlue => new(175, 175, 255); // #147
		public static Color LightBlue => new(135, 175, 215);    // #110 (LightSkyBlue3_1)
		public static Color LightCyan1 => new(215, 255, 255);   // #195
		public static Color PaleTurquoise1 => new(175, 255, 255); // #159
		public static Color LightSkyBlue1 => new(175, 215, 255); // #153
		public static Color SkyBlue1 => new(135, 215, 255);     // #117
		public static Color LightSlateGrey => new(135, 135, 175); // #103
		public static Color Honeydew2 => new(215, 255, 215);    // #194
		public static Color LightGoldenrodYellow => new(215, 215, 135); // #186 (LightGoldenrod2)
		public static Color LightYellow1 => new(255, 255, 175); // #229 (LightYellow1)
		public static Color DarkSlateGray1 => new(135, 255, 255); // #123
		public static Color DarkSlateGray3 => new(135, 215, 215); // #116
		public static Color Cyan1 => new(0, 255, 255);          // #51
		public static Color Orange3 => new(215, 135, 0);        // #172
		public static Color Magenta1 => new(255, 0, 255);       // #201
		public static Color DodgerBlue2 => new(0, 95, 255);     // #27
		public static Color SpringGreen2 => new(0, 215, 135);   // #42
		public static Color Chartreuse2 => new(95, 255, 0);     // #82
		public static Color Gold1 => new(255, 215, 0);          // #220
		public static Color Gold3 => new(175, 175, 0);          // #142
		public static Color Coral => new(255, 127, 80);         // CSS (no xterm equivalent)

		// xterm-256 palette — full Spectre.Console compatibility
		// These use exact xterm-256 RGB values. Where a name already existed above
		// with CSS RGB values, the values were updated to match xterm-256.
		public static Color NavyBlue => new(0, 0, 95);
		public static Color Blue3 => new(0, 0, 175);
		public static Color Blue3_1 => new(0, 0, 215);
		public static Color Blue1 => new(0, 0, 255);
		public static Color DeepSkyBlue4 => new(0, 95, 95);
		public static Color DeepSkyBlue4_1 => new(0, 95, 135);
		public static Color DeepSkyBlue4_2 => new(0, 95, 175);
		public static Color DodgerBlue3 => new(0, 95, 215);
		public static Color Green4 => new(0, 135, 0);
		public static Color SpringGreen4 => new(0, 135, 95);
		public static Color Turquoise4 => new(0, 135, 135);
		public static Color DeepSkyBlue3 => new(0, 135, 175);
		public static Color DeepSkyBlue3_1 => new(0, 135, 215);
		public static Color Green3 => new(0, 175, 0);
		public static Color SpringGreen3 => new(0, 175, 95);
		public static Color DeepSkyBlue2 => new(0, 175, 215);
		public static Color Green3_1 => new(0, 215, 0);
		public static Color SpringGreen3_1 => new(0, 215, 95);
		public static Color Cyan3 => new(0, 215, 175);
		public static Color Turquoise2 => new(0, 215, 255);
		public static Color Green1 => new(0, 255, 0);
		public static Color SpringGreen2_1 => new(0, 255, 95);
		public static Color Cyan2 => new(0, 255, 215);
		public static Color DeepPink4 => new(95, 0, 95);
		public static Color Purple4 => new(95, 0, 135);
		public static Color Purple4_1 => new(95, 0, 175);
		public static Color Purple3 => new(95, 0, 215);
		public static Color Orange4 => new(95, 95, 0);
		public static Color MediumPurple4 => new(95, 95, 135);
		public static Color SlateBlue3 => new(95, 95, 175);
		public static Color SlateBlue3_1 => new(95, 95, 215);
		public static Color Chartreuse4 => new(95, 135, 0);
		public static Color DarkSeaGreen4 => new(95, 135, 95);
		public static Color PaleTurquoise4 => new(95, 135, 135);
		public static Color SteelBlue3 => new(95, 135, 215);
		public static Color Chartreuse3 => new(95, 175, 0);
		public static Color DarkSeaGreen4_1 => new(95, 175, 95);
		public static Color CadetBlue_1 => new(95, 175, 175);
		public static Color SkyBlue3 => new(95, 175, 215);
		public static Color SteelBlue1 => new(95, 175, 255);
		public static Color Chartreuse3_1 => new(95, 215, 0);
		public static Color PaleGreen3 => new(95, 215, 95);
		public static Color SeaGreen3 => new(95, 215, 135);
		public static Color Aquamarine3 => new(95, 215, 175);
		public static Color SteelBlue1_1 => new(95, 215, 255);
		public static Color SeaGreen2 => new(95, 255, 95);
		public static Color SeaGreen1 => new(95, 255, 135);
		public static Color SeaGreen1_1 => new(95, 255, 175);
		public static Color Aquamarine1 => new(95, 255, 215);
		public static Color DarkSlateGray2 => new(95, 255, 255);
		public static Color DarkRed_1 => new(135, 0, 0);
		public static Color DeepPink4_1 => new(135, 0, 95);
		public static Color DarkMagenta_1 => new(135, 0, 175);
		public static Color DarkViolet_1 => new(175, 0, 215);
		public static Color Purple_1 => new(135, 0, 255);
		public static Color Orange4_1 => new(135, 95, 0);
		public static Color LightPink4 => new(135, 95, 95);
		public static Color Plum4 => new(135, 95, 135);
		public static Color MediumPurple3 => new(135, 95, 175);
		public static Color MediumPurple3_1 => new(135, 95, 215);
		public static Color Yellow4 => new(135, 135, 0);
		public static Color Wheat4 => new(135, 135, 95);
		public static Color LightSlateBlue => new(135, 135, 255);
		public static Color Yellow4_1 => new(135, 175, 0);
		public static Color DarkOliveGreen3 => new(135, 175, 95);
		public static Color LightSkyBlue3 => new(135, 175, 175);
		public static Color LightSkyBlue3_1 => new(135, 175, 215);
		public static Color SkyBlue2 => new(135, 175, 255);
		public static Color Chartreuse2_1 => new(135, 215, 0);
		public static Color DarkOliveGreen3_1 => new(135, 215, 95);
		public static Color PaleGreen3_1 => new(135, 215, 135);
		public static Color DarkSeaGreen3 => new(135, 215, 175);
		public static Color LightGreen_1 => new(135, 255, 135);
		public static Color Aquamarine1_1 => new(135, 255, 215);
		public static Color Red3 => new(175, 0, 0);
		public static Color DeepPink4_2 => new(175, 0, 95);
		public static Color Magenta3 => new(175, 0, 175);
		public static Color Purple_2 => new(175, 0, 255);
		public static Color DarkOrange3 => new(175, 95, 0);
		public static Color HotPink3 => new(175, 95, 135);
		public static Color MediumOrchid3 => new(175, 95, 175);
		public static Color MediumPurple2 => new(175, 95, 255);
		public static Color LightSalmon3 => new(175, 135, 95);
		public static Color MediumPurple2_1 => new(175, 135, 215);
		public static Color MediumPurple1 => new(175, 135, 255);
		public static Color Gold3_1 => new(175, 175, 0);
		public static Color NavajoWhite3 => new(175, 175, 135);
		public static Color LightSteelBlue3 => new(175, 175, 215);
		public static Color Yellow3 => new(175, 215, 0);
		public static Color DarkOliveGreen3_2 => new(175, 215, 95);
		public static Color DarkSeaGreen3_1 => new(175, 215, 135);
		public static Color DarkSeaGreen2 => new(175, 215, 175);
		public static Color LightCyan3 => new(175, 215, 215);
		public static Color DarkOliveGreen2 => new(175, 255, 95);
		public static Color PaleGreen1_1 => new(175, 255, 135);
		public static Color DarkSeaGreen2_1 => new(175, 255, 175);
		public static Color DarkSeaGreen1 => new(175, 255, 215);
		public static Color Red3_1 => new(215, 0, 0);
		public static Color DeepPink3 => new(215, 0, 95);
		public static Color DeepPink3_1 => new(215, 0, 135);
		public static Color Magenta3_1 => new(215, 0, 175);
		public static Color Magenta3_2 => new(215, 0, 215);
		public static Color Magenta2 => new(215, 0, 255);
		public static Color DarkOrange3_1 => new(215, 95, 0);
		public static Color IndianRed_1 => new(215, 95, 95);
		public static Color HotPink3_1 => new(215, 95, 135);
		public static Color HotPink2 => new(215, 95, 175);
		public static Color MediumOrchid1 => new(215, 95, 255);
		public static Color LightSalmon3_1 => new(215, 135, 95);
		public static Color LightPink3 => new(215, 135, 135);
		public static Color Pink3 => new(215, 135, 175);
		public static Color Plum3 => new(215, 135, 215);
		public static Color LightGoldenrod3 => new(215, 175, 95);
		public static Color MistyRose3 => new(215, 175, 175);
		public static Color Thistle3 => new(215, 175, 215);
		public static Color Plum2 => new(215, 175, 255);
		public static Color Yellow3_1 => new(215, 215, 0);
		public static Color Khaki3 => new(215, 215, 95);
		public static Color LightGoldenrod2 => new(215, 215, 135);
		public static Color LightYellow3 => new(215, 215, 175);
		public static Color LightSteelBlue1 => new(215, 215, 255);
		public static Color Yellow2 => new(215, 255, 0);
		public static Color DarkOliveGreen1_1 => new(215, 255, 95);
		public static Color DarkOliveGreen1_2 => new(215, 255, 135);
		public static Color DarkSeaGreen1_1 => new(215, 255, 175);
		public static Color Red1 => new(255, 0, 0);
		public static Color DeepPink2 => new(255, 0, 95);
		public static Color DeepPink1_1 => new(255, 0, 175);
		public static Color Magenta2_1 => new(255, 0, 215);
		public static Color OrangeRed1 => new(255, 95, 0);
		public static Color IndianRed1 => new(255, 95, 95);
		public static Color IndianRed1_1 => new(255, 95, 135);
		public static Color HotPink_1 => new(255, 95, 215);
		public static Color MediumOrchid1_1 => new(255, 95, 255);
		public static Color PaleVioletRed1 => new(255, 135, 175);
		public static Color Orchid2 => new(255, 135, 215);
		public static Color Orchid1 => new(255, 135, 255);
		public static Color LightGoldenrod2_1 => new(255, 215, 95);
		public static Color LightGoldenrod2_2 => new(255, 215, 135);
		public static Color Yellow1 => new(255, 255, 0);
		public static Color LightGoldenrod1 => new(255, 255, 95);
		public static Color Cornsilk1 => new(255, 255, 215);

		#endregion

		#region Named Colors — Grey Scale (matching Spectre.Console naming)

		public static Color Grey0 => new(0, 0, 0);
		public static Color Grey3 => new(8, 8, 8);
		public static Color Grey7 => new(18, 18, 18);
		public static Color Grey11 => new(28, 28, 28);
		public static Color Grey15 => new(38, 38, 38);
		public static Color Grey19 => new(48, 48, 48);
		public static Color Grey23 => new(58, 58, 58);
		public static Color Grey27 => new(68, 68, 68);
		public static Color Grey30 => new(78, 78, 78);
		public static Color Grey35 => new(88, 88, 88);
		public static Color Grey37 => new(95, 95, 95);
		public static Color Grey39 => new(98, 98, 98);
		public static Color Grey42 => new(108, 108, 108);
		public static Color Grey46 => new(118, 118, 118);
		public static Color Grey50 => new(128, 128, 128);
		public static Color Grey53 => new(135, 135, 135);
		public static Color Grey54 => new(138, 138, 138);
		public static Color Grey58 => new(148, 148, 148);
		public static Color Grey62 => new(158, 158, 158);
		public static Color Grey63 => new(160, 160, 160);
		public static Color Grey66 => new(168, 168, 168);
		public static Color Grey69 => new(175, 175, 175);
		public static Color Grey70 => new(178, 178, 178);
		public static Color Grey74 => new(188, 188, 188);
		public static Color Grey78 => new(198, 198, 198);
		public static Color Grey82 => new(208, 208, 208);
		public static Color Grey84 => new(215, 215, 215);
		public static Color Grey85 => new(218, 218, 218);
		public static Color Grey89 => new(228, 228, 228);
		public static Color Grey93 => new(238, 238, 238);
		public static Color Grey100 => new(255, 255, 255);

		#endregion

		#region Parsing

		/// <summary>Attempts to resolve a color name to a Color value.</summary>
		public static bool TryFromName(string name, out Color color)
		{
			return ColorTable.TryGetByName(name, out color);
		}

		/// <summary>
		/// Attempts to parse a hex color string (#RGB, #RRGGBB, or RRGGBB).
		/// </summary>
		public static bool TryFromHex(string hex, out Color color)
		{
			color = default;
			if (string.IsNullOrEmpty(hex)) return false;

			ReadOnlySpan<char> span = hex.AsSpan();
			if (span[0] == '#') span = span[1..];

			if (span.Length == 3)
			{
				if (TryParseHexNibble(span[0], out byte r) &&
					TryParseHexNibble(span[1], out byte g) &&
					TryParseHexNibble(span[2], out byte b))
				{
					color = new Color((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
					return true;
				}
				return false;
			}

			if (span.Length == 8)
			{
				if (byte.TryParse(span[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
					byte.TryParse(span[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
					byte.TryParse(span[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b) &&
					byte.TryParse(span[6..8], System.Globalization.NumberStyles.HexNumber, null, out byte a))
				{
					color = new Color(r, g, b, a);
					return true;
				}
				return false;
			}

			if (span.Length == 6)
			{
				if (byte.TryParse(span[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
					byte.TryParse(span[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
					byte.TryParse(span[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
				{
					color = new Color(r, g, b);
					return true;
				}
			}

			return false;
		}

		private static bool TryParseHexNibble(char c, out byte value)
		{
			if (c >= '0' && c <= '9') { value = (byte)(c - '0'); return true; }
			if (c >= 'a' && c <= 'f') { value = (byte)(c - 'a' + 10); return true; }
			if (c >= 'A' && c <= 'F') { value = (byte)(c - 'A' + 10); return true; }
			value = 0;
			return false;
		}

		#endregion

		#region Equality

		public bool Equals(Color other) =>
			IsDefault == other.IsDefault && R == other.R && G == other.G && B == other.B && A == other.A;

		public override bool Equals(object? obj) => obj is Color other && Equals(other);

		public override int GetHashCode() => IsDefault ? -1 : HashCode.Combine(R, G, B, A);

		public static bool operator ==(Color left, Color right) => left.Equals(right);
		public static bool operator !=(Color left, Color right) => !left.Equals(right);

		#endregion

		/// <summary>Returns the color as "Color(R, G, B)" or "Color(Default)".</summary>
		public override string ToString() =>
			IsDefault ? "Color(Default)" :
			A < 255 ? $"Color({R}, {G}, {B}, {A})" :
			$"Color({R}, {G}, {B})";

		/// <summary>
		/// Returns the color as a markup string. For opaque colors, returns a Spectre-compatible
		/// "rgb(R,G,B)" string. For semi-transparent colors, returns "rgba(R,G,B,A)" which
		/// is interpreted by the framework's own renderer, not by Spectre.Console.
		/// </summary>
		public string ToMarkup()
		{
			if (IsDefault) return "default";
			if (A < 255) return $"rgba({R},{G},{B},{A / 255.0:0.##})";
			return $"rgb({R},{G},{B})";
		}

		/// <summary>Returns a copy of this color with the specified alpha value.</summary>
		public Color WithAlpha(byte a) => new(R, G, B, a);

		#region Blending
	
		/// <summary>
		/// Composites <paramref name="src"/> over <paramref name="dst"/> using Porter-Duff "over".
		/// Fast paths: fully opaque src (A=255) returns src; fully transparent src (A=0) returns dst.
		/// If dst.IsDefault, returns src unmodified (no RGB to blend against).
		/// Always returns a fully opaque color (A=255) — alpha is consumed at blend time.
		/// </summary>
		public static Color Blend(Color src, Color dst)
		{
			if (dst.IsDefault)  return src;
			if (src.A == 255)   return src;
			if (src.A == 0)     return dst;
	
			// Integer arithmetic with rounding to avoid truncation error
			int alpha = src.A;
			int invAlpha = 255 - alpha;
			return new Color(
				(byte)((src.R * alpha + dst.R * invAlpha + 127) / 255),
				(byte)((src.G * alpha + dst.G * invAlpha + 127) / 255),
				(byte)((src.B * alpha + dst.B * invAlpha + 127) / 255),
				255);
		}
	
		#endregion

		/// <summary>
		/// Creates a Color from a 256-color palette index (0-255).
		/// Indices 0-7 are standard colors, 8-15 are bright colors,
		/// 16-231 are a 6x6x6 RGB cube, 232-255 are grayscale.
		/// </summary>
		public static Color FromInt32(int index)
		{
			if (index < 0 || index > 255)
				throw new ArgumentOutOfRangeException(nameof(index), "Color index must be 0-255.");

			// Standard and bright colors (0-15)
			Color[] basic =
			{
				new(0, 0, 0), new(128, 0, 0), new(0, 128, 0), new(128, 128, 0),
				new(0, 0, 128), new(128, 0, 128), new(0, 128, 128), new(192, 192, 192),
				new(128, 128, 128), new(255, 0, 0), new(0, 255, 0), new(255, 255, 0),
				new(0, 0, 255), new(255, 0, 255), new(0, 255, 255), new(255, 255, 255)
			};
			if (index < 16)
				return basic[index];

			// 6x6x6 RGB cube (16-231)
			if (index < 232)
			{
				int i = index - 16;
				int r = i / 36;
				int g = (i % 36) / 6;
				int b = i % 6;
				return new Color(
					(byte)(r == 0 ? 0 : 55 + r * 40),
					(byte)(g == 0 ? 0 : 55 + g * 40),
					(byte)(b == 0 ? 0 : 55 + b * 40));
			}

			// Grayscale (232-255)
			byte gray = (byte)(8 + (index - 232) * 10);
			return new Color(gray, gray, gray);
		}

		/// <summary>Implicit conversion from Spectre.Console.Color for migration compatibility.</summary>
		public static implicit operator Color(Spectre.Console.Color spectreColor)
		{
			if (spectreColor == Spectre.Console.Color.Default)
				return Default;
			return new Color(spectreColor.R, spectreColor.G, spectreColor.B);
		}

		/// <summary>Implicit conversion to Spectre.Console.Color for migration compatibility.</summary>
		public static implicit operator Spectre.Console.Color(Color color)
		{
			if (color.IsDefault)
				return Spectre.Console.Color.Default;
			return new Spectre.Console.Color(color.R, color.G, color.B);
		}
	}
}
