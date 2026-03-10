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
	/// Maps color names to Color values. Used by the markup parser and
	/// <see cref="Color.TryFromName"/> for name-based color resolution.
	/// Names are case-insensitive and underscore/space-tolerant.
	/// </summary>
	internal static class ColorTable
	{
		private static readonly Dictionary<string, Color> NameToColor =
			new(StringComparer.OrdinalIgnoreCase)
		{
			// Basic 16
			["default"] = Color.Default,
			["black"] = Color.Black,
			["maroon"] = Color.Maroon,
			["green"] = Color.Green,
			["olive"] = Color.Olive,
			["navy"] = Color.Navy,
			["purple"] = Color.Purple,
			["teal"] = Color.Teal,
			["silver"] = Color.Silver,
			["grey"] = Color.Grey,
			["gray"] = Color.Grey,
			["red"] = Color.Red,
			["lime"] = Color.Lime,
			["yellow"] = Color.Yellow,
			["blue"] = Color.Blue,
			["fuchsia"] = Color.Fuchsia,
			["magenta"] = Color.Fuchsia,
			["aqua"] = Color.Aqua,
			["cyan"] = Color.Aqua,
			["white"] = Color.White,

			// Aliases
			["darkred"] = Color.DarkRed,
			["darkgreen"] = Color.DarkGreen,
			["darkyellow"] = Color.DarkYellow,
			["darkblue"] = Color.DarkBlue,
			["darkmagenta"] = Color.DarkMagenta,
			["darkcyan"] = Color.DarkCyan,

			// Extended palette
			["orange1"] = Color.Orange1,
			["darkorange"] = Color.DarkOrange,
			["indianred"] = Color.IndianRed,
			["hotpink"] = Color.HotPink,
			["deeppink1"] = Color.DeepPink1,
			["mediumorchid"] = Color.MediumOrchid,
			["darkviolet"] = Color.DarkViolet,
			["blueviolet"] = Color.BlueViolet,
			["royalblue1"] = Color.RoyalBlue1,
			["cornflowerblue"] = Color.CornflowerBlue,
			["dodgerblue1"] = Color.DodgerBlue1,
			["deepskyblue1"] = Color.DeepSkyBlue1,
			["steelblue"] = Color.SteelBlue,
			["cadetblue"] = Color.CadetBlue,
			["mediumturquoise"] = Color.MediumTurquoise,
			["darkturquoise"] = Color.DarkTurquoise,
			["lightseagreen"] = Color.LightSeaGreen,
			["mediumspringgreen"] = Color.MediumSpringGreen,
			["springgreen1"] = Color.SpringGreen1,
			["chartreuse1"] = Color.Chartreuse1,
			["greenyellow"] = Color.GreenYellow,
			["darkolivegreen1"] = Color.DarkOliveGreen1,
			["palegreen1"] = Color.PaleGreen1,
			["darkseagreen"] = Color.DarkSeaGreen,
			["mediumseagreen"] = Color.MediumSeaGreen,
			["lightgreen"] = Color.LightGreen,
			["khaki1"] = Color.Khaki1,
			["darkkhaki"] = Color.DarkKhaki,
			["darkgoldenrod"] = Color.DarkGoldenrod,
			["wheat1"] = Color.Wheat1,
			["navajowhite1"] = Color.NavajoWhite1,
			["mistyrose1"] = Color.MistyRose1,
			["lightcoral"] = Color.LightCoral,
			["salmon1"] = Color.Salmon1,
			["lightsalmon1"] = Color.LightSalmon1,
			["lightpink1"] = Color.LightPink1,
			["pink1"] = Color.Pink1,
			["plum1"] = Color.Plum1,
			["orchid"] = Color.Orchid,
			["violet"] = Color.Violet,
			["thistle1"] = Color.Thistle1,
			["sandybrown"] = Color.SandyBrown,
			["tan"] = Color.Tan,
			["rosybrown"] = Color.RosyBrown,
			["palevioletred"] = Color.PaleVioletRed,
			["mediumvioletred"] = Color.MediumVioletRed,
			["mediumpurple"] = Color.MediumPurple,
			["mediumslateblue"] = Color.MediumSlateBlue,
			["slateblue1"] = Color.SlateBlue1,
			["lightsteelblue"] = Color.LightSteelBlue,
			["lightblue"] = Color.LightBlue,
			["lightcyan1"] = Color.LightCyan1,
			["paleturquoise1"] = Color.PaleTurquoise1,
			["lightskyblue1"] = Color.LightSkyBlue1,
			["skyblue1"] = Color.SkyBlue1,
			["lightslategrey"] = Color.LightSlateGrey,
			["lightslategray"] = Color.LightSlateGrey,
			["honeydew2"] = Color.Honeydew2,
			["lightgoldenrodyellow"] = Color.LightGoldenrodYellow,
			["lightyellow1"] = Color.LightYellow1,
			["darkslategray1"] = Color.DarkSlateGray1,
			["darkslategray3"] = Color.DarkSlateGray3,
			["cyan1"] = Color.Cyan1,
			["orange3"] = Color.Orange3,
			["magenta1"] = Color.Magenta1,
			["dodgerblue2"] = Color.DodgerBlue2,
			["springgreen2"] = Color.SpringGreen2,
			["chartreuse2"] = Color.Chartreuse2,
			["gold1"] = Color.Gold1,
			["gold3"] = Color.Gold3,
			["coral"] = Color.Coral,

			// Grey scale
			["grey0"] = Color.Grey0,
			["grey3"] = Color.Grey3,
			["grey7"] = Color.Grey7,
			["grey11"] = Color.Grey11,
			["grey15"] = Color.Grey15,
			["grey19"] = Color.Grey19,
			["grey23"] = Color.Grey23,
			["grey27"] = Color.Grey27,
			["grey30"] = Color.Grey30,
			["grey35"] = Color.Grey35,
			["grey37"] = Color.Grey37,
			["grey39"] = Color.Grey39,
			["grey42"] = Color.Grey42,
			["grey46"] = Color.Grey46,
			["grey50"] = Color.Grey50,
			["grey53"] = Color.Grey53,
			["grey54"] = Color.Grey54,
			["grey58"] = Color.Grey58,
			["grey62"] = Color.Grey62,
			["grey63"] = Color.Grey63,
			["grey66"] = Color.Grey66,
			["grey69"] = Color.Grey69,
			["grey70"] = Color.Grey70,
			["grey74"] = Color.Grey74,
			["grey78"] = Color.Grey78,
			["grey82"] = Color.Grey82,
			["grey84"] = Color.Grey84,
			["grey85"] = Color.Grey85,
			["grey89"] = Color.Grey89,
			["grey93"] = Color.Grey93,
			["grey100"] = Color.Grey100,
			// gray aliases
			["gray0"] = Color.Grey0,
			["gray3"] = Color.Grey3,
			["gray7"] = Color.Grey7,
			["gray11"] = Color.Grey11,
			["gray15"] = Color.Grey15,
			["gray19"] = Color.Grey19,
			["gray23"] = Color.Grey23,
			["gray27"] = Color.Grey27,
			["gray30"] = Color.Grey30,
			["gray35"] = Color.Grey35,
			["gray37"] = Color.Grey37,
			["gray39"] = Color.Grey39,
			["gray42"] = Color.Grey42,
			["gray46"] = Color.Grey46,
			["gray50"] = Color.Grey50,
			["gray53"] = Color.Grey53,
			["gray54"] = Color.Grey54,
			["gray58"] = Color.Grey58,
			["gray62"] = Color.Grey62,
			["gray63"] = Color.Grey63,
			["gray66"] = Color.Grey66,
			["gray69"] = Color.Grey69,
			["gray70"] = Color.Grey70,
			["gray74"] = Color.Grey74,
			["gray78"] = Color.Grey78,
			["gray82"] = Color.Grey82,
			["gray84"] = Color.Grey84,
			["gray85"] = Color.Grey85,
			["gray89"] = Color.Grey89,
			["gray93"] = Color.Grey93,
			["gray100"] = Color.Grey100,
		};

		/// <summary>
		/// Looks up a color by name. Normalizes underscores and spaces for tolerance.
		/// </summary>
		public static bool TryGetByName(string name, out Color color)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				color = default;
				return false;
			}

			// Normalize: strip underscores and spaces
			string normalized = name.Replace("_", "").Replace(" ", "");
			return NameToColor.TryGetValue(normalized, out color);
		}
	}
}
