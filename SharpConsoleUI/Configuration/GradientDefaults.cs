using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Default values for gradient rendering operations.
	/// </summary>
	public static class GradientDefaults
	{
		/// <summary>
		/// Default gradient direction when none is specified.
		/// </summary>
		public const GradientDirection DefaultDirection = GradientDirection.Horizontal;

		/// <summary>
		/// Arrow separator used in markup gradient specs (ASCII-friendly alias for the Unicode arrow).
		/// </summary>
		public const string ArrowSeparator = "->";

		/// <summary>
		/// Unicode arrow separator used in gradient specs.
		/// </summary>
		public const string UnicodeArrowSeparator = "\u2192";
	}
}
