// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using System.Text;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a single character cell in the character buffer.
	/// Contains the character, foreground color, background color, decorations, and dirty state.
	/// </summary>
	public struct Cell : IEquatable<Cell>
	{
		public Rune Character;
		public Color Foreground;
		public Color Background;
		public TextDecoration Decorations;
		public bool Dirty;
		public bool IsWideContinuation;

		/// <summary>
		/// Creates a new cell with a Rune character.
		/// </summary>
		public Cell(Rune character, Color foreground, Color background)
		{
			Character = character;
			Foreground = foreground;
			Background = background;
			Decorations = TextDecoration.None;
			Dirty = true;
			IsWideContinuation = false;
		}

		/// <summary>
		/// Creates a new cell with a char character.
		/// </summary>
		public Cell(char character, Color foreground, Color background)
			: this(new Rune(character), foreground, background) { }

		/// <summary>
		/// Creates a new cell with a Rune character and decorations.
		/// </summary>
		public Cell(Rune character, Color foreground, Color background, TextDecoration decorations)
		{
			Character = character;
			Foreground = foreground;
			Background = background;
			Decorations = decorations;
			Dirty = true;
			IsWideContinuation = false;
		}

		/// <summary>
		/// Creates a new cell with a char character and decorations.
		/// </summary>
		public Cell(char character, Color foreground, Color background, TextDecoration decorations)
			: this(new Rune(character), foreground, background, decorations) { }

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
		/// Creates a cell with the specified Rune character and colors.
		/// </summary>
		public static Cell Create(Rune character, Color foreground, Color background) =>
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
			Decorations == other.Decorations &&
			IsWideContinuation == other.IsWideContinuation;

		/// <summary>Determines whether this cell equals another cell.</summary>
		public bool Equals(Cell other) =>
			Character == other.Character &&
			Foreground.Equals(other.Foreground) &&
			Background.Equals(other.Background) &&
			Decorations == other.Decorations &&
			Dirty == other.Dirty &&
			IsWideContinuation == other.IsWideContinuation;

		/// <summary>Determines whether this cell equals another object.</summary>
		public override bool Equals(object? obj) => obj is Cell other && Equals(other);

		/// <summary>Gets the hash code for this cell.</summary>
		public override int GetHashCode() => HashCode.Combine(Character, Foreground, Background, Decorations, Dirty, IsWideContinuation);

		/// <summary>Equality operator.</summary>
		public static bool operator ==(Cell left, Cell right) => left.Equals(right);
		/// <summary>Inequality operator.</summary>
		public static bool operator !=(Cell left, Cell right) => !left.Equals(right);

		/// <summary>Returns a string representation of this cell.</summary>
		public override string ToString()
		{
			var dec = Decorations != TextDecoration.None ? $", {Decorations}" : "";
			var cont = IsWideContinuation ? ", continuation" : "";
			var charDisplay = Character == new Rune(' ') ? "SP" : Character.ToString();
			return $"Cell('{charDisplay}', {Foreground}, {Background}{dec}{cont}{(Dirty ? ", dirty" : "")})";
		}
	}
}
