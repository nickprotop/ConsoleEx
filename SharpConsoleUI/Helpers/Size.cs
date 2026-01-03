// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Represents a size with width and height dimensions.
	/// </summary>
	public class Size
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Size"/> class with the specified dimensions.
		/// </summary>
		/// <param name="width">The width dimension.</param>
		/// <param name="height">The height dimension.</param>
		public Size(int width, int height)
		{
			Width = width;
			Height = height;
		}

		/// <summary>
		/// Gets or sets the height dimension.
		/// </summary>
		public int Height { get; set; }

		/// <summary>
		/// Gets or sets the width dimension.
		/// </summary>
		public int Width { get; set; }
	}
}