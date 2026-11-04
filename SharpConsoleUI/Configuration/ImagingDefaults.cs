// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Imaging;

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Default constants for the imaging subsystem.
	/// </summary>
	public static class ImagingDefaults
	{
		/// <summary>Default scale mode for ImageControl.</summary>
		public const ImageScaleMode DefaultScaleMode = ImageScaleMode.Fit;

		/// <summary>Maximum pixel dimension (width or height) for resize operations.</summary>
		public const int MaxImageDimension = 500;

		/// <summary>Number of vertical pixels represented by a single half-block cell.</summary>
		public const int PixelsPerCell = 2;

		/// <summary>The upper half block character used for image rendering.</summary>
		public const char HalfBlockChar = '\u2580';

		/// <summary>Maximum bytes per Kitty graphics protocol chunk.</summary>
		public const int KittyChunkSize = 4096;

		/// <summary>The Unicode placeholder character for Kitty virtual placements.</summary>
		public static readonly Rune KittyPlaceholder = new Rune(0x10EEEE);

		/// <summary>Maximum image dimension supported by Kitty protocol.</summary>
		public const int KittyMaxImageDimension = 4096;

		/// <summary>
		/// The most terminal cells a Kitty placement may span, in either direction.
		/// </summary>
		/// <remarks>
		/// A placement wider or taller than this is refused rather than transmitted. The limit guards
		/// against a runaway layout asking the terminal to hold an absurd placement; it is far beyond
		/// any real terminal, which is a few hundred cells across.
		/// </remarks>
		public const int KittyMaxPlacementCells = 1000;

		/// <summary>
		/// The most pixels a Kitty raw-RGB transmission may carry, in either direction.
		/// </summary>
		public const int KittyMaxRawPixelDimension = 10000;
	}
}
