// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Configuration
{
	/// <summary>
	/// Centralized default values for layout measurement and rendering width limits.
	/// </summary>
	public static class LayoutDefaults
	{
		/// <summary>
		/// Width used by GetLogicalContentSize when no explicit width is set.
		/// Prevents unbounded allocation when measuring Spectre renderables.
		/// </summary>
		public const int DefaultUnboundedMeasureWidth = 1000;

		/// <summary>
		/// Hard safety cap for AnsiEmptySpace to prevent OOM from any caller
		/// passing an excessively large width.
		/// </summary>
		public const int MaxSafeRenderWidth = 10000;
	}
}
