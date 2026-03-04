// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Drivers.Input
{
	/// <summary>
	/// Base type for parsed input events from raw stdin byte streams.
	/// </summary>
	internal abstract record InputEvent;

	/// <summary>
	/// A keyboard input event parsed from raw ANSI byte sequences.
	/// </summary>
	internal record KeyInputEvent(ConsoleKeyInfo KeyInfo) : InputEvent;

	/// <summary>
	/// A mouse input event parsed from SGR or X10 ANSI mouse sequences.
	/// </summary>
	internal record MouseInputEvent(List<MouseFlags> Flags, Point Position) : InputEvent;

	/// <summary>
	/// An unrecognized byte sequence that could not be parsed.
	/// </summary>
	internal record UnknownSequenceEvent(byte[] Data) : InputEvent;
}
