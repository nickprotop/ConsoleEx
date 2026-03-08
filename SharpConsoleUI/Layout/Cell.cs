// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a single character cell in the character buffer.
	/// Contains the character, foreground color, background color, decorations, and dirty state.
	/// </summary>
	public struct Cell : IEquatable<Cell>
	{
		public char Character;
		public Color Foreground;
		public Color Background;
		public TextDecoration Decorations;
		public bool Dirty;

		/// <summary>
		/// Creates a new cell with the specified values.
		/// </summary>
		public Cell(char character, Color foreground, Color background)
		{
			Character = character;
			Foreground = foreground;
			Background = background;
			Decorations = TextDecoration.None;
			Dirty = true;
		}

		/// <summary>
		/// Creates a new cell with the specified values and decorations.
		/// </summary>
		public Cell(char character, Color foreground, Color background, TextDecoration decorations)
		{
			Character = character;
			Foreground = foreground;
			Background = background;
			Decorations = decorations;
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
			Background.Equals(other.Background) &&
			Decorations == other.Decorations;

		/// <summary>Determines whether this cell equals another cell.</summary>
		public bool Equals(Cell other) =>
			Character == other.Character &&
			Foreground.Equals(other.Foreground) &&
			Background.Equals(other.Background) &&
			Decorations == other.Decorations &&
			Dirty == other.Dirty;

		/// <summary>Determines whether this cell equals another object.</summary>
		public override bool Equals(object? obj) => obj is Cell other && Equals(other);

		/// <summary>Gets the hash code for this cell.</summary>
		public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Decorations, Dirty);

		/// <summary>Equality operator.</summary>
		public static bool operator ==(Cell left, Cell right) => left.Equals(right);
		/// <summary>Inequality operator.</summary>
		public static bool operator !=(Cell left, Cell right) => !left.Equals(right);

		/// <summary>Returns a string representation of this cell.</summary>
		public override string ToString()
		{
			var dec = Decorations != TextDecoration.None ? $", {Decorations}" : "";
			return $"Cell('{(Character == ' ' ? "SP" : Character)}', {Foreground}, {Background}{dec}{(Dirty ? ", dirty" : "")})";
		}
	}
}
