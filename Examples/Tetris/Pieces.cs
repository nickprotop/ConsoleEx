// -----------------------------------------------------------------------
// Tetris - the seven pieces, their rotations, and the kicks.
//
// Guideline shapes and Super Rotation System kick tables. The numbers are
// the spec's, not invented: a rotation that fails its five kick tests is
// refused, which is what makes T-spins and wall slides work the way players
// expect. Getting these wrong produces a game that LOOKS like Tetris and
// feels subtly wrong to anyone who has played one.
// -----------------------------------------------------------------------

namespace Tetris;

internal enum PieceKind { I, J, L, O, S, T, Z }

/// <summary>A piece's four rotations, each as the cells it fills.</summary>
internal static class Pieces
{
	/// <summary>Cells of each piece at spawn, in a 4x4 box for I and O, 3x3 for the rest.</summary>
	/// <remarks>
	/// Spawn orientation is the guideline's: flat side down, I and O centred. Rotations are
	/// generated rather than listed, so a typo cannot make one rotation disagree with another.
	/// </remarks>
	private static readonly (int X, int Y)[][] Spawn =
	{
		new[] { (0, 1), (1, 1), (2, 1), (3, 1) },   // I
		new[] { (0, 0), (0, 1), (1, 1), (2, 1) },   // J
		new[] { (2, 0), (0, 1), (1, 1), (2, 1) },   // L
		new[] { (0, 0), (1, 0), (0, 1), (1, 1) },   // O
		new[] { (1, 0), (2, 0), (0, 1), (1, 1) },   // S
		new[] { (1, 0), (0, 1), (1, 1), (2, 1) },   // T
		new[] { (0, 0), (1, 0), (1, 1), (2, 1) },   // Z
	};

	/// <summary>The box a piece rotates inside: 4 for I, 2 for O, 3 for the rest.</summary>
	private static readonly int[] BoxSize = { 4, 3, 3, 2, 3, 3, 3 };

	/// <summary>[kind][rotation] -> the cells that rotation fills.</summary>
	private static readonly (int X, int Y)[][][] Rotations = BuildRotations();

	/// <summary>The cells a piece fills in one of its four rotations.</summary>
	public static (int X, int Y)[] Cells(PieceKind kind, int rotation) =>
		Rotations[(int)kind][((rotation % 4) + 4) % 4];

	/// <summary>
	/// Rotates each spawn shape three times inside its own box. O is square, so its four
	/// rotations are identical and the kick tests never move it.
	/// </summary>
	private static (int X, int Y)[][][] BuildRotations()
	{
		var all = new (int X, int Y)[Spawn.Length][][];

		for (int k = 0; k < Spawn.Length; k++)
		{
			int size = BoxSize[k];
			all[k] = new (int X, int Y)[4][];
			all[k][0] = Spawn[k];

			for (int r = 1; r < 4; r++)
			{
				var previous = all[k][r - 1];
				var next = new (int X, int Y)[previous.Length];
				for (int i = 0; i < previous.Length; i++)
				{
					// Clockwise inside the box: (x,y) -> (size-1-y, x).
					next[i] = (size - 1 - previous[i].Y, previous[i].X);
				}
				all[k][r] = next;
			}
		}

		return all;
	}

	/// <summary>
	/// The offsets to try when a rotation collides, in order. The first that fits is taken; if
	/// none do the rotation is refused.
	/// </summary>
	/// <remarks>
	/// Two tables, because I rotates around a different centre from everything else. O never
	/// needs them. Y is negated against the spec's tables, which count upward.
	/// </remarks>
	public static (int X, int Y)[] Kicks(PieceKind kind, int from, int to)
	{
		if (kind == PieceKind.O) return new[] { (0, 0) };

		bool clockwise = to == (from + 1) % 4;
		int index = clockwise ? from : to;

		var table = kind == PieceKind.I ? IKicks : StandardKicks;
		var row = table[index];

		if (clockwise) return row;

		// Anticlockwise is the same row negated: the spec lists only one direction.
		var mirrored = new (int X, int Y)[row.Length];
		for (int i = 0; i < row.Length; i++) mirrored[i] = (-row[i].X, -row[i].Y);
		return mirrored;
	}

	// Rows are 0->1, 1->2, 2->3, 3->0.
	private static readonly (int X, int Y)[][] StandardKicks =
	{
		new[] { (0, 0), (-1, 0), (-1, -1), (0, 2), (-1, 2) },
		new[] { (0, 0), (1, 0), (1, 1), (0, -2), (1, -2) },
		new[] { (0, 0), (1, 0), (1, -1), (0, 2), (1, 2) },
		new[] { (0, 0), (-1, 0), (-1, 1), (0, -2), (-1, -2) },
	};

	private static readonly (int X, int Y)[][] IKicks =
	{
		new[] { (0, 0), (-2, 0), (1, 0), (-2, 1), (1, -2) },
		new[] { (0, 0), (-1, 0), (2, 0), (-1, -2), (2, 1) },
		new[] { (0, 0), (2, 0), (-1, 0), (2, -1), (-1, 2) },
		new[] { (0, 0), (1, 0), (-2, 0), (1, 2), (-2, -1) },
	};
}
