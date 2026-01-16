// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Configuration options for NetConsoleDriver.
	/// </summary>
	public class NetConsoleDriverOptions
	{
		/// <summary>
		/// Gets or sets the rendering mode for console output.
		/// </summary>
		public RenderMode RenderMode { get; set; } = RenderMode.Buffer;

		/// <summary>
		/// Gets or sets the buffer size for buffered rendering (in characters).
		/// Future use - currently not implemented but ready for enhancement.
		/// </summary>
		public int BufferSize { get; set; } = 8192;

		/// <summary>
		/// Gets or sets the cursor blink rate in milliseconds.
		/// Future use - currently not implemented but ready for enhancement.
		/// </summary>
		public int CursorBlinkRate { get; set; } = 500;

		/// <summary>
		/// Creates a default options instance with RenderMode.Buffer.
		/// </summary>
		public static NetConsoleDriverOptions Default => new()
		{
			RenderMode = RenderMode.Buffer
		};
	}
}
