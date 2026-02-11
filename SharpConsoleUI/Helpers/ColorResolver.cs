// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Spectre.Console;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides centralized color resolution logic for controls.
	/// Extracted from 11+ controls that had identical cascading null-coalescing chains.
	/// </summary>
	public static class ColorResolver
	{
		/// <summary>
		/// Resolves a background color using the standard fallback chain:
		/// explicit value → container background → theme window background → default.
		/// </summary>
		/// <param name="explicitValue">The explicitly set color value, if any.</param>
		/// <param name="container">The parent container to inherit colors from.</param>
		/// <param name="defaultColor">The default color to use if no other source is available. Defaults to Black.</param>
		/// <returns>The resolved background color.</returns>
		public static Color ResolveBackground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Black;

			return explicitValue
				?? container?.BackgroundColor
				?? container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves a foreground color using the standard fallback chain:
		/// explicit value → container foreground → theme window foreground → default.
		/// </summary>
		/// <param name="explicitValue">The explicitly set color value, if any.</param>
		/// <param name="container">The parent container to inherit colors from.</param>
		/// <param name="defaultColor">The default color to use if no other source is available. Defaults to White.</param>
		/// <returns>The resolved foreground color.</returns>
		public static Color ResolveForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.ForegroundColor
				?? container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves a menu bar background color using the menu-specific fallback chain.
		/// </summary>
		/// <param name="explicitValue">The explicitly set color value, if any.</param>
		/// <param name="container">The parent container to inherit colors from.</param>
		/// <param name="defaultColor">The default color to use if no other source is available. Defaults to Black.</param>
		/// <returns>The resolved menu bar background color.</returns>
		public static Color ResolveMenuBarBackground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Black;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.MenuBarBackgroundColor
				?? container?.BackgroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves a menu bar foreground color using the menu-specific fallback chain.
		/// </summary>
		/// <param name="explicitValue">The explicitly set color value, if any.</param>
		/// <param name="container">The parent container to inherit colors from.</param>
		/// <param name="defaultColor">The default color to use if no other source is available. Defaults to White.</param>
		/// <returns>The resolved menu bar foreground color.</returns>
		public static Color ResolveMenuBarForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.MenuBarForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves button background color: explicit → theme button bg → container bg → default.
		/// </summary>
		public static Color ResolveButtonBackground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Black;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor
				?? container?.BackgroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves button foreground color: explicit → theme button fg → container fg → default.
		/// </summary>
		public static Color ResolveButtonForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves focused button background color: explicit → theme focused bg → container bg → default.
		/// </summary>
		public static Color ResolveButtonFocusedBackground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Black;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor
				?? container?.BackgroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves focused button foreground color: explicit → theme focused fg → container fg → default.
		/// </summary>
		public static Color ResolveButtonFocusedForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves disabled button background color: explicit → theme disabled bg → container bg → default.
		/// </summary>
		public static Color ResolveButtonDisabledBackground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Black;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor
				?? container?.BackgroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves disabled button foreground color: explicit → theme disabled fg → container fg → default.
		/// </summary>
		public static Color ResolveButtonDisabledForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}
	}
}
