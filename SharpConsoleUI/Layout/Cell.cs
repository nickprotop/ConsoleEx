// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a single character cell in the character buffer.
	/// Contains the character, foreground color, background color, and dirty state.
	/// </summary>
	public struct Cell : IEquatable<Cell>
	{
		/// <summary>
		/// The character to display.
		/// </summary>
		public char Character;

		/// <summary>
		/// The foreground color.
		/// </summary>
		public Color Foreground;

		/// <summary>
		/// The background color.
		/// </summary>
		public Color Background;

		/// <summary>
		/// Whether this cell has been modified since the last render.
		/// </summary>
		public bool Dirty;

		/// <summary>
		/// Creates a new cell with the specified values.
		/// </summary>
		public Cell(char character, Color foreground, Color background)
		{
			Character = character;
			Foreground = foreground;
			Background = background;
			Dirty = true;
		}

		/// <summary>
		/// Gets a blank cell with default colors.
		/// </summary>
		public static Cell Blank => new(' ', Color.White, Color.Black);

		/// <summary>
		/// Gets a blank cell with the specified background color.
		/// </summary>
		public static Cell BlankWithBackground(Color background) =>
			new(' ', Color.White, background);

		/// <summary>
		/// Creates a cell with the specified character and colors.
		/// </summary>
		public static Cell Create(char character, Color foreground, Color background) =>
			new(character, foreground, background);

		/// <summary>
		/// Returns a copy of this cell with the dirty flag cleared.
		/// </summary>
		public Cell AsClean()
		{
			var copy = this;
			copy.Dirty = false;
			return copy;
		}

		/// <summary>
		/// Returns a copy of this cell with the dirty flag set.
		/// </summary>
		public Cell AsDirty()
		{
			var copy = this;
			copy.Dirty = true;
			return copy;
		}

		/// <summary>
		/// Determines whether this cell has the same visual appearance as another.
		/// Does not compare dirty state.
		/// </summary>
		public bool VisuallyEquals(Cell other) =>
			Character == other.Character &&
			Foreground.Equals(other.Foreground) &&
			Background.Equals(other.Background);

		public bool Equals(Cell other) =>
			Character == other.Character &&
			Foreground.Equals(other.Foreground) &&
			Background.Equals(other.Background) &&
			Dirty == other.Dirty;

		public override bool Equals(object? obj) => obj is Cell other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Dirty);

		public static bool operator ==(Cell left, Cell right) => left.Equals(right);
		public static bool operator !=(Cell left, Cell right) => !left.Equals(right);

		public override string ToString() =>
			$"Cell('{(Character == ' ' ? "SP" : Character)}', {Foreground}, {Background}{(Dirty ? ", dirty" : "")})";
	}
}
