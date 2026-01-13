// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using System.Drawing;

namespace SharpConsoleUI.Events
{
	/// <summary>
	/// Event arguments for mouse events
	/// </summary>
	public class MouseEventArgs
	{
		/// <summary>
		/// Mouse flags indicating the type of mouse event
		/// </summary>
		public List<MouseFlags> Flags { get; set; } = new List<MouseFlags>();

		/// <summary>
		/// Position relative to the control receiving the event
		/// </summary>
		public Point Position { get; set; }

		/// <summary>
		/// Absolute position in desktop coordinates
		/// </summary>
		public Point AbsolutePosition { get; set; }

		/// <summary>
		/// Position relative to the window containing the control
		/// </summary>
		public Point WindowPosition { get; set; }

		/// <summary>
		/// Whether this event has been handled and should not propagate further
		/// </summary>
		public bool Handled { get; set; }

		/// <summary>
		/// The window that received the mouse event
		/// </summary>
		public Window? SourceWindow { get; set; }

		/// <summary>
		/// Creates a new MouseEventArgs instance
		/// </summary>
		public MouseEventArgs(List<MouseFlags> flags, Point position, Point absolutePosition, Point windowPosition, Window? sourceWindow = null)
		{
			Flags = flags ?? new List<MouseFlags>();
			Position = position;
			AbsolutePosition = absolutePosition;
			WindowPosition = windowPosition;
			SourceWindow = sourceWindow;
			Handled = false;
		}

		/// <summary>
		/// Convenience method to check if specific mouse flags are present
		/// </summary>
		public bool HasFlag(MouseFlags flag)
		{
			return Flags.Contains(flag);
		}

		/// <summary>
		/// Convenience method to check if any of the specified flags are present
		/// </summary>
		public bool HasAnyFlag(params MouseFlags[] flags)
		{
			return flags.Any(flag => Flags.Contains(flag));
		}

		/// <summary>
		/// Creates a copy of this event args with a new position (for coordinate translation)
		/// </summary>
		public MouseEventArgs WithPosition(Point newPosition)
		{
			return new MouseEventArgs(Flags, newPosition, AbsolutePosition, WindowPosition, SourceWindow)
			{
				Handled = this.Handled
			};
		}

		/// <summary>
		/// Creates a copy of this event args with additional flags
		/// </summary>
		public MouseEventArgs WithFlags(params MouseFlags[] additionalFlags)
		{
			var newFlags = new List<MouseFlags>(Flags);
			newFlags.AddRange(additionalFlags);
			return new MouseEventArgs(newFlags, Position, AbsolutePosition, WindowPosition, SourceWindow)
			{
				Handled = this.Handled
			};
		}

		/// <summary>
		/// Creates a copy of this event args with flags replaced
		/// </summary>
		public MouseEventArgs WithReplacedFlags(params MouseFlags[] replacementFlags)
		{
			return new MouseEventArgs(new List<MouseFlags>(replacementFlags), Position, AbsolutePosition, WindowPosition, SourceWindow)
			{
				Handled = this.Handled
			};
		}
	}
}