// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// A small set of seed colors used to generate a complete theme via <see cref="Theme.FromPalette"/>.
	/// Every field is optional: unset fields are derived (secondary/tertiary from primary, foreground
	/// from background contrast, status colors from mode-tuned defaults, background from the mode).
	/// </summary>
	public sealed record Palette
	{
		/// <summary>The main accent color (borders, buttons, selection). Drives the accent family.</summary>
		public Color? Primary { get; init; }

		/// <summary>Secondary accent. Defaults to a shaded <see cref="Primary"/> when unset.</summary>
		public Color? Secondary { get; init; }

		/// <summary>Tertiary accent. Defaults to a shaded <see cref="Secondary"/> when unset.</summary>
		public Color? Tertiary { get; init; }

		/// <summary>Window/desktop background. Drives the neutral family. Defaults per <see cref="Mode"/>.</summary>
		public Color? Background { get; init; }

		/// <summary>Default text color. Defaults to a readable contrast on <see cref="Background"/>.</summary>
		public Color? Foreground { get; init; }

		/// <summary>Success color. Defaults to a mode-tuned green.</summary>
		public Color? Success { get; init; }

		/// <summary>Warning color. Defaults to a mode-tuned amber.</summary>
		public Color? Warning { get; init; }

		/// <summary>Danger/error color. Defaults to a mode-tuned red.</summary>
		public Color? Danger { get; init; }

		/// <summary>Info color. Defaults to a mode-tuned cyan/blue.</summary>
		public Color? Info { get; init; }

		/// <summary>
		/// Explicit light/dark mode. When null, the generator infers it from <see cref="Background"/>
		/// luminance, falling back to <see cref="ThemeMode.Dark"/> if no background is given.
		/// </summary>
		public ThemeMode? Mode { get; init; }
	}
}
