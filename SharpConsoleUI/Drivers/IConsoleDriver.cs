// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Color = Spectre.Console.Color;
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
		/// Sets a single cell at the specified position with the given character and colors.
		/// </summary>
		/// <param name="x">The horizontal position (column).</param>
		/// <param name="y">The vertical position (row).</param>
		/// <param name="character">The character to write.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void SetCell(int x, int y, char character, Color fg, Color bg);

		/// <summary>
		/// Fills a horizontal run of cells at the specified position with the given character and colors.
		/// </summary>
		/// <param name="x">The starting horizontal position (column).</param>
		/// <param name="y">The vertical position (row).</param>
		/// <param name="width">The number of cells to fill.</param>
		/// <param name="character">The character to fill with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillCells(int x, int y, int width, char character, Color fg, Color bg);

		/// <summary>
		/// Gets the count of dirty characters in the rendering buffer.
		/// </summary>
		/// <returns>The number of dirty characters, or 0 if not using buffered rendering.</returns>
		public int GetDirtyCharacterCount();

		/// <summary>
		/// Copies a horizontal strip of cells from a <see cref="CharacterBuffer"/> directly
		/// to the console output buffer, bypassing ANSI string serialization and parsing.
		/// </summary>
		/// <param name="destX">Destination screen X position.</param>
		/// <param name="destY">Destination screen Y position.</param>
		/// <param name="source">The source CharacterBuffer.</param>
		/// <param name="srcX">Source X offset within the buffer.</param>
		/// <param name="srcY">Source Y (row) within the buffer.</param>
		/// <param name="width">Number of cells to write.</param>
		/// <param name="fallbackBg">Background color for padding when source is out of bounds.</param>
		public void WriteBufferRegion(int destX, int destY, CharacterBuffer source, int srcX, int srcY, int width, Color fallbackBg);
	}
}