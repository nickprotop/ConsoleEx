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
	/// Thrown when image data is not in a format the library can decode.
	/// </summary>
	/// <remarks>
	/// The supported formats are PNG, JPEG, BMP, GIF and TGA. Anything else — a TIFF, a WebP, a
	/// truncated file, bytes that are not an image at all — lands here. The decoder identifies a
	/// format from its content, so a wrong file extension or <c>Content-Type</c> does not change
	/// the outcome.
	/// </remarks>
	public class UnsupportedImageFormatException : Exception
	{
		/// <summary>Creates the exception with a default message.</summary>
		public UnsupportedImageFormatException()
			: base("The data is not in a supported image format (PNG, JPEG, BMP, GIF or TGA).")
		{
		}

		/// <summary>Creates the exception with the specified message.</summary>
		public UnsupportedImageFormatException(string message)
			: base(message)
		{
		}

		/// <summary>Creates the exception with the specified message and inner exception.</summary>
		public UnsupportedImageFormatException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
