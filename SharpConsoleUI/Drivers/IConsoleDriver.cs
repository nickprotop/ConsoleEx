// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Defines the interface for console drivers that handle low-level console input/output operations.
	/// </summary>
	/// <remarks>
	/// Console drivers abstract the platform-specific console functionality, providing a unified
	/// interface for keyboard input, mouse events, screen resizing, and console output.
	/// </remarks>
	public interface IConsoleDriver
	{
		/// <summary>
		/// Represents the method that will handle mouse events.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="flags">A list of mouse flags indicating the type of mouse action(s).</param>
		/// <param name="point">The position where the mouse event occurred.</param>
		public delegate void MouseEventHandler(object sender, List<MouseFlags> flags, Point point);

		/// <summary>
		/// Occurs when a key is pressed on the keyboard.
		/// </summary>
		public event EventHandler<ConsoleKeyInfo> KeyPressed;

		/// <summary>
		/// Occurs when a mouse event is detected.
		/// </summary>
		public event MouseEventHandler? MouseEvent;

		/// <summary>
		/// Occurs when the console screen is resized.
		/// </summary>
		public event EventHandler<Size>? ScreenResized;

		/// <summary>
		/// Gets the current size of the console screen.
		/// </summary>
		/// <value>A <see cref="Size"/> representing the width and height of the console in characters.</value>
		public Size ScreenSize { get; }

		/// <summary>
		/// Clears the console screen or buffer.
		/// </summary>
		public void Clear();

		/// <summary>
		/// Flushes any buffered output to the console.
		/// </summary>
		/// <remarks>
		/// For buffered render modes, this triggers the actual rendering to the console.
		/// For direct render modes, this may be a no-op.
		/// </remarks>
		public void Flush();

		/// <summary>
		/// Starts the console driver, initializing input handling and enabling mouse/keyboard events.
		/// </summary>
		public void Start();

		/// <summary>
		/// Stops the console driver, disabling input handling and restoring the console to its original state.
		/// </summary>
		public void Stop();

		/// <summary>
		/// Sets the cursor position on the console screen.
		/// </summary>
		/// <param name="x">The column position (0-based).</param>
		/// <param name="y">The row position (0-based).</param>
		public void SetCursorPosition(int x, int y);

		/// <summary>
		/// Sets the visibility of the cursor.
		/// </summary>
		/// <param name="visible">True to show the cursor, false to hide it.</param>
		public void SetCursorVisible(bool visible);

		/// <summary>
		/// Sets the cursor shape/style.
		/// </summary>
		/// <param name="shape">The desired cursor shape.</param>
		public void SetCursorShape(Core.CursorShape shape);

		/// <summary>
		/// Resets the cursor to the default shape.
		/// </summary>
		public void ResetCursorShape();

		/// <summary>
		/// Initializes the driver with a reference to the window system.
		/// Called by ConsoleWindowSystem after state services are created.
		/// </summary>
		/// <param name="windowSystem">The window system instance</param>
		public void Initialize(ConsoleWindowSystem windowSystem);

		/// <summary>
		/// Writes a string value to the console at the specified position.
		/// </summary>
		/// <param name="x">The horizontal position (column) to write at.</param>
		/// <param name="y">The vertical position (row) to write at.</param>
		/// <param name="value">The string value to write, which may include ANSI escape sequences.</param>
		public void WriteToConsole(int x, int y, string value);
	}
}