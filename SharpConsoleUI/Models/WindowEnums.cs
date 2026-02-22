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
	/// Per-border movement permissions for window resizing.
	/// Each border has an Expand direction (border moves outward, window grows)
	/// and a Contract direction (border moves inward, window shrinks).
	/// </summary>
	/// <remarks>
	/// Sign conventions when dragging or pressing keyboard shortcuts:
	/// <list type="bullet">
	///   <item>Top border — Expand: border moves up; Contract: border moves down</item>
	///   <item>Bottom border — Expand: border moves down; Contract: border moves up</item>
	///   <item>Left border — Expand: border moves left; Contract: border moves right</item>
	///   <item>Right border — Expand: border moves right; Contract: border moves left</item>
	/// </list>
	/// Keyboard shortcuts (Shift+arrows) only trigger Expand movement on the natural border.
	/// </remarks>
	[Flags]
	public enum ResizeBorderDirections
	{
		/// <summary>No resize movement is permitted.</summary>
		None            = 0,

		/// <summary>Top border moves up — window grows taller.</summary>
		TopExpand       = 1 << 0,
		/// <summary>Top border moves down — window shrinks from the top.</summary>
		TopContract     = 1 << 1,

		/// <summary>Bottom border moves down — window grows taller.</summary>
		BottomExpand    = 1 << 2,
		/// <summary>Bottom border moves up — window shrinks from the bottom.</summary>
		BottomContract  = 1 << 3,

		/// <summary>Left border moves left — window grows wider.</summary>
		LeftExpand      = 1 << 4,
		/// <summary>Left border moves right — window shrinks from the left.</summary>
		LeftContract    = 1 << 5,

		/// <summary>Right border moves right — window grows wider.</summary>
		RightExpand     = 1 << 6,
		/// <summary>Right border moves left — window shrinks from the right.</summary>
		RightContract   = 1 << 7,

		/// <summary>Both expand and contract on the top border.</summary>
		Top    = TopExpand    | TopContract,
		/// <summary>Both expand and contract on the bottom border.</summary>
		Bottom = BottomExpand | BottomContract,
		/// <summary>Both expand and contract on the left border.</summary>
		Left   = LeftExpand   | LeftContract,
		/// <summary>Both expand and contract on the right border.</summary>
		Right  = RightExpand  | RightContract,

		/// <summary>All borders may expand outward only.</summary>
		ExpandOnly   = TopExpand   | BottomExpand   | LeftExpand   | RightExpand,
		/// <summary>All borders may contract inward only.</summary>
		ContractOnly = TopContract | BottomContract | LeftContract | RightContract,

		/// <summary>All borders and all directions are resizable.</summary>
		All = Top | Bottom | Left | Right
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
