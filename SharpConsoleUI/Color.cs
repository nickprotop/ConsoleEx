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

		public static Color Orange1 => new(255, 175, 0);
		public static Color DarkOrange => new(255, 140, 0);
		public static Color IndianRed => new(205, 92, 92);
		public static Color HotPink => new(255, 105, 180);
		public static Color DeepPink1 => new(255, 20, 147);
		public static Color MediumOrchid => new(186, 85, 211);
		public static Color DarkViolet => new(148, 0, 211);
		public static Color BlueViolet => new(138, 43, 226);
		public static Color RoyalBlue1 => new(65, 105, 225);
		public static Color CornflowerBlue => new(100, 149, 237);
		public static Color DodgerBlue1 => new(30, 144, 255);
		public static Color DeepSkyBlue1 => new(0, 191, 255);
		public static Color SteelBlue => new(70, 130, 180);
		public static Color CadetBlue => new(95, 158, 160);
		public static Color MediumTurquoise => new(72, 209, 204);
		public static Color DarkTurquoise => new(0, 206, 209);
		public static Color LightSeaGreen => new(32, 178, 170);
		public static Color MediumSpringGreen => new(0, 250, 154);
		public static Color SpringGreen1 => new(0, 255, 127);
		public static Color Chartreuse1 => new(127, 255, 0);
		public static Color GreenYellow => new(173, 255, 47);
		public static Color DarkOliveGreen1 => new(202, 255, 112);
		public static Color PaleGreen1 => new(152, 251, 152);
		public static Color DarkSeaGreen => new(143, 188, 143);
		public static Color MediumSeaGreen => new(60, 179, 113);
		public static Color LightGreen => new(144, 238, 144);
		public static Color Khaki1 => new(240, 230, 140);
		public static Color DarkKhaki => new(189, 183, 107);
		public static Color DarkGoldenrod => new(184, 134, 11);
		public static Color Wheat1 => new(245, 222, 179);
		public static Color NavajoWhite1 => new(255, 222, 173);
		public static Color MistyRose1 => new(255, 228, 225);
		public static Color LightCoral => new(240, 128, 128);
		public static Color Salmon1 => new(250, 128, 114);
		public static Color LightSalmon1 => new(255, 160, 122);
		public static Color LightPink1 => new(255, 182, 193);
		public static Color Pink1 => new(255, 192, 203);
		public static Color Plum1 => new(221, 160, 221);
		public static Color Orchid => new(218, 112, 214);
		public static Color Violet => new(238, 130, 238);
		public static Color Thistle1 => new(216, 191, 216);
		public static Color SandyBrown => new(244, 164, 96);
		public static Color Tan => new(210, 180, 140);
		public static Color RosyBrown => new(188, 143, 143);
		public static Color PaleVioletRed => new(219, 112, 147);
		public static Color MediumVioletRed => new(199, 21, 133);
		public static Color MediumPurple => new(147, 112, 219);
		public static Color MediumSlateBlue => new(123, 104, 238);
		public static Color SlateBlue1 => new(106, 90, 205);
		public static Color LightSteelBlue => new(176, 196, 222);
		public static Color LightBlue => new(173, 216, 230);
		public static Color LightCyan1 => new(224, 255, 255);
		public static Color PaleTurquoise1 => new(175, 238, 238);
		public static Color LightSkyBlue1 => new(135, 206, 250);
		public static Color SkyBlue1 => new(135, 206, 235);
		public static Color LightSlateGrey => new(119, 136, 153);
		public static Color Honeydew2 => new(240, 255, 240);
		public static Color LightGoldenrodYellow => new(250, 250, 210);
		public static Color LightYellow1 => new(255, 255, 224);
		public static Color DarkSlateGray1 => new(47, 79, 79);
		public static Color DarkSlateGray3 => new(95, 135, 135);
		public static Color Cyan1 => new(0, 255, 255);
		public static Color Orange3 => new(205, 133, 0);
		public static Color Magenta1 => new(255, 0, 255);

		// Additional Spectre.Console compatibility colors
		public static Color DodgerBlue2 => new(28, 134, 238);
		public static Color SpringGreen2 => new(0, 238, 118);
		public static Color Chartreuse2 => new(118, 238, 0);
		public static Color Gold1 => new(255, 215, 0);
		public static Color Gold3 => new(205, 173, 0);
		public static Color Coral => new(255, 127, 80);

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
		public static Color Grey37 => new(94, 94, 94);
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
