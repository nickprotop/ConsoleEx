// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------


#pragma warning disable CS1591

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Represents an item in a <see cref="CommandPaletteControl"/>.
	/// </summary>
	public class CommandPaletteItem
	{
		public CommandPaletteItem(string label, Action action)
		{
			Label = label;
			Action = action;
		}

		public string Label { get; set; }
		public string? Description { get; set; }
		public string? Category { get; set; }
		public string? Icon { get; set; }
		public Color? IconColor { get; set; }
		public Action Action { get; set; }
		public string? Shortcut { get; set; }
		public bool IsEnabled { get; set; } = true;
		public object? Tag { get; set; }
	}
}
