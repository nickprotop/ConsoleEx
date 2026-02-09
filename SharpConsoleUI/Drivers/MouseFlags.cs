// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// Code from Terminal.Gui - https://github.com/gui-cs/Terminal.Gui

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Specifies flags that describe mouse button states and events.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This enumeration supports a bitwise combination of its member values,
	/// allowing multiple mouse states to be represented simultaneously.
	/// </para>
	/// <para>
	/// Button numbers correspond to: Button1 (left), Button2 (middle), Button3 (right), Button4 (extended).
	/// </para>
	/// <para>
	/// Code adapted from Terminal.Gui - https://github.com/gui-cs/Terminal.Gui
	/// </para>
	/// </remarks>
	[Flags]
	public enum MouseFlags
	{
		/// <summary>No mouse event</summary>
		None = 0,

		/// <summary>The first mouse button was pressed.</summary>
		Button1Pressed = 0x2,

		/// <summary>The first mouse button was released.</summary>
		Button1Released = 0x1,

		/// <summary>The first mouse button was clicked (press+release).</summary>
		Button1Clicked = 0x4,

		/// <summary>The first mouse button was double-clicked.</summary>
		Button1DoubleClicked = 0x8,

		/// <summary>The first mouse button was triple-clicked.</summary>
		Button1TripleClicked = 0x10,

		/// <summary>The first mouse button is being dragged (held down while moving).</summary>
		Button1Dragged = 0x20,

		/// <summary>The second mouse button was pressed.</summary>
		Button2Pressed = 0x80,

		/// <summary>The second mouse button was released.</summary>
		Button2Released = 0x40,

		/// <summary>The second mouse button was clicked (press+release).</summary>
		Button2Clicked = 0x100,

		/// <summary>The second mouse button was double-clicked.</summary>
		Button2DoubleClicked = 0x200,

		/// <summary>The second mouse button was triple-clicked.</summary>
		Button2TripleClicked = 0x400,

		/// <summary>The second mouse button is being dragged (held down while moving).</summary>
		Button2Dragged = 0x800,

		/// <summary>The third mouse button was pressed.</summary>
		Button3Pressed = 0x2000,

		/// <summary>The third mouse button was released.</summary>
		Button3Released = 0x1000,

		/// <summary>The third mouse button was clicked (press+release).</summary>
		Button3Clicked = 0x4000,

		/// <summary>The third mouse button was double-clicked.</summary>
		Button3DoubleClicked = 0x8000,

		/// <summary>The third mouse button was triple-clicked.</summary>
		Button3TripleClicked = 0x10000,

		/// <summary>The third mouse button is being dragged (held down while moving).</summary>
		Button3Dragged = 0x20000,

		/// <summary>The fourth mouse button was pressed.</summary>
		Button4Pressed = 0x80000,

		/// <summary>The fourth mouse button was released.</summary>
		Button4Released = 0x40000,

		/// <summary>The fourth button was clicked (press+release).</summary>
		Button4Clicked = 0x100000,

		/// <summary>The fourth button was double-clicked.</summary>
		Button4DoubleClicked = 0x200000,

		/// <summary>The fourth button was triple-clicked.</summary>
		Button4TripleClicked = 0x400000,

		/// <summary>Flag: the shift key was pressed when the mouse button took place.</summary>
		ButtonShift = 0x2000000,

		/// <summary>Flag: the ctrl key was pressed when the mouse button took place.</summary>
		ButtonCtrl = 0x1000000,

		/// <summary>Flag: the alt key was pressed when the mouse button took place.</summary>
		ButtonAlt = 0x4000000,

		/// <summary>The mouse position is being reported in this event.</summary>
		ReportMousePosition = 0x8000000,

		/// <summary>Vertical button wheeled up.</summary>
		WheeledUp = 0x10000000,

		/// <summary>Vertical button wheeled down.</summary>
		WheeledDown = 0x20000000,

		/// <summary>Vertical button wheeled up while pressing ButtonCtrl.</summary>
		WheeledLeft = ButtonCtrl | WheeledUp,

		/// <summary>Vertical button wheeled down while pressing ButtonCtrl.</summary>
		WheeledRight = ButtonCtrl | WheeledDown,

		/// <summary>The mouse entered the control area.</summary>
		MouseEnter = 0x40000000,

		/// <summary>The mouse left the control area.</summary>
		MouseLeave = unchecked((int)0x80000000),

		/// <summary>Mask that captures all the events.</summary>
		AllEvents = unchecked((int)0xffffffff)
	}
}