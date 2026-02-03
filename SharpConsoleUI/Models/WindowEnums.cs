// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI
{
	/// <summary>
	/// Specifies the direction of movement or navigation.
	/// </summary>
	public enum Direction
	{
		/// <summary>Upward direction.</summary>
		Up,
		/// <summary>Downward direction.</summary>
		Down,
		/// <summary>Leftward direction.</summary>
		Left,
		/// <summary>Rightward direction.</summary>
		Right
	}

	/// <summary>
	/// Specifies the type of window topology operation.
	/// </summary>
	public enum WindowTopologyAction
	{
		/// <summary>Resize the window.</summary>
		Resize,
		/// <summary>Move the window.</summary>
		Move
	}

	/// <summary>
	/// Specifies the direction from which a window is being resized.
	/// </summary>
	public enum ResizeDirection
	{
		/// <summary>No resize operation.</summary>
		None,
		/// <summary>Resize from the top edge.</summary>
		Top,
		/// <summary>Resize from the bottom edge.</summary>
		Bottom,
		/// <summary>Resize from the left edge.</summary>
		Left,
		/// <summary>Resize from the right edge.</summary>
		Right,
		/// <summary>Resize from the top-left corner.</summary>
		TopLeft,
		/// <summary>Resize from the top-right corner.</summary>
		TopRight,
		/// <summary>Resize from the bottom-left corner.</summary>
		BottomLeft,
		/// <summary>Resize from the bottom-right corner.</summary>
		BottomRight
	}
}
