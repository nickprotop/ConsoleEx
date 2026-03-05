// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class SparklineControl
	{
		private char GetBarChar(double barHeight, int rowIndex, bool useBraille)
		{
			double rowTopThreshold = rowIndex + 1;
			double rowBottomThreshold = rowIndex;

			if (useBraille)
			{
				if (barHeight >= rowTopThreshold)
					return BRAILLE_CHARS[4]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 4);
					return BRAILLE_CHARS[Math.Clamp(charIndex, 0, 4)];
				}
				return BRAILLE_CHARS[0]; // Empty
			}
			else
			{
				if (barHeight >= rowTopThreshold)
					return VERTICAL_CHARS[8]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 8);
					return VERTICAL_CHARS[Math.Clamp(charIndex, 0, 8)];
				}
				return VERTICAL_CHARS[0]; // Empty
			}
		}

		private char GetBarCharInverted(double barHeight, int rowIndex, bool useBraille)
		{
			// For inverted bars (growing downward), we fill from the top of each cell
			double rowTopThreshold = rowIndex + 1;
			double rowBottomThreshold = rowIndex;

			if (useBraille)
			{
				if (barHeight >= rowTopThreshold)
					return BRAILLE_CHARS_INVERTED[4]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 4);
					// Use inverted braille chars for top-down fill
					return BRAILLE_CHARS_INVERTED[Math.Clamp(charIndex, 0, 4)];
				}
				return BRAILLE_CHARS_INVERTED[0]; // Empty
			}
			else
			{
				// For block mode, use upper block characters for downward bars
				if (barHeight >= rowTopThreshold)
					return VERTICAL_CHARS_INVERTED[8]; // Full
				else if (barHeight > rowBottomThreshold)
				{
					double fraction = barHeight - rowBottomThreshold;
					int charIndex = (int)Math.Round(fraction * 8);
					// Use inverted vertical chars for top-down fill
					return VERTICAL_CHARS_INVERTED[Math.Clamp(charIndex, 0, 8)];
				}
				return VERTICAL_CHARS_INVERTED[0]; // Empty
			}
		}
	}
}
