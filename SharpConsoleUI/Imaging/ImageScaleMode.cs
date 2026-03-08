// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// Determines how an image is scaled to fit the available space.
	/// </summary>
	public enum ImageScaleMode
	{
		/// <summary>Scale uniformly to fit within bounds, preserving aspect ratio.</summary>
		Fit,

		/// <summary>Scale uniformly to fill bounds, cropping excess. Preserves aspect ratio.</summary>
		Fill,

		/// <summary>Stretch to exactly match bounds, ignoring aspect ratio.</summary>
		Stretch,

		/// <summary>Display at original size without scaling.</summary>
		None
	}
}
