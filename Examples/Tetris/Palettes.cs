// -----------------------------------------------------------------------
// Tetris - the piece colours and particle gradients.
//
// The guideline colours, which players read as piece identity rather than
// decoration: a cyan I and a yellow O are as much part of the game as the
// shapes themselves.
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;

namespace Tetris;

internal static class Palettes
{
	public static readonly Color I = new(0, 215, 215);
	public static readonly Color J = new(60, 90, 230);
	public static readonly Color L = new(240, 150, 40);
	public static readonly Color O = new(240, 215, 60);
	public static readonly Color S = new(80, 220, 100);
	public static readonly Color T = new(170, 90, 230);
	public static readonly Color Z = new(235, 70, 80);

	public static readonly Color Empty = new(18, 20, 32);
	public static readonly Color Grid = new(30, 34, 52);
	public static readonly Color Wall = new(70, 82, 120);
	public static readonly Color Ghost = new(90, 100, 135);

	/// <summary>A piece's colour.</summary>
	public static Color For(PieceKind kind) => kind switch
	{
		PieceKind.I => I,
		PieceKind.J => J,
		PieceKind.L => L,
		PieceKind.O => O,
		PieceKind.S => S,
		PieceKind.T => T,
		_ => Z,
	};

	/// <summary>A board cell's colour: 0 is empty, otherwise the piece that locked there.</summary>
	public static Color ForCell(int cell) =>
		cell <= 0 ? Empty : For((PieceKind)(cell - 1));

	/// <summary>Debris from a cleared cell, shaded from its own colour.</summary>
	public static ColorGradient DebrisFor(int cell) => FromColor(ForCell(cell));

	/// <summary>A gradient built from one colour: a bright lead-in, the colour, a dark tail.</summary>
	public static ColorGradient FromColor(Color c) => ColorGradient.FromColors(
		new Color(
			(byte)Math.Min(255, c.R + 70),
			(byte)Math.Min(255, c.G + 70),
			(byte)Math.Min(255, c.B + 70)),
		c,
		new Color((byte)(c.R / 3), (byte)(c.G / 3), (byte)(c.B / 3)));
}
