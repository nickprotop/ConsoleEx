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
		/// Hard safety cap for a layout node's auto-measured DESIRED HEIGHT when the incoming
		/// constraints are unbounded (MaxHeight == int.MaxValue). A node's reported size becomes
		/// its arranged bounds; if an unbounded child measure (e.g. a Fill child receiving
		/// int.MaxValue - fixed ≈ 2 billion) leaks into the returned size, an ancestor container's
		/// row-fill loop would iterate hundreds of millions of times (effective hang). No real
		/// terminal viewport is anywhere near this tall, so clamping here is behavior-preserving
		/// for legitimate content while making a runaway extent impossible.
		/// </summary>
		public const int MaxUnboundedMeasureHeight = 10000;

		/// <summary>
		/// Hard safety cap for AnsiEmptySpace to prevent OOM from any caller
		/// passing an excessively large width.
		/// </summary>
		public const int MaxSafeRenderWidth = 10000;
	}
}
