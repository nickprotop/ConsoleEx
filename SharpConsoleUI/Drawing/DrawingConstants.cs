// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drawing
{
	/// <summary>
	/// Named constants for drawing primitives including characters, thresholds, and algorithm parameters.
	/// </summary>
#pragma warning disable CS1591 // Self-documenting constants
	public static class DrawingConstants
	{
		public const char DotChar = '·';
		public const char BlockFull = '█';
		public const char BlockLight = '░';
		public const char BlockMedium = '▒';
		public const char BlockDark = '▓';
		public const char BulletChar = '•';

		// Stipple density thresholds (0.0 - 1.0)
		public const double StippleLightThreshold = 0.25;
		public const double StippleMediumThreshold = 0.50;
		public const double StippleDarkThreshold = 0.75;

		// Arc resolution (segments per full circle)
		public const int DefaultArcSegments = 64;

		// Word wrap minimum width
		public const int MinWordWrapWidth = 4;

		// Hash primes for deterministic stipple pattern
		public const int StipplePrimeX = 7919;
		public const int StipplePrimeY = 104729;
		public const int StippleModulus = 1000;
	}
#pragma warning restore CS1591
}
