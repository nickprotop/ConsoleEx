// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Parsing
{
	/// <summary>
	/// A clickable link region over a single rendered line, expressed in display-column
	/// (cell) coordinates. The range is half-open: <c>[StartCol, EndCol)</c>.
	/// </summary>
	public readonly struct LinkSpan
	{
		/// <summary>First display column (inclusive) covered by the link.</summary>
		public readonly int StartCol;
		/// <summary>One past the last display column (exclusive) covered by the link.</summary>
		public readonly int EndCol;
		/// <summary>The link target URL (already unescaped).</summary>
		public readonly string Url;
		/// <summary>The visible link text.</summary>
		public readonly string Text;

		/// <summary>Creates a link span.</summary>
		/// <param name="startCol">First display column (inclusive).</param>
		/// <param name="endCol">One past the last display column (exclusive).</param>
		/// <param name="url">The link target URL (already unescaped).</param>
		/// <param name="text">The visible link text.</param>
		public LinkSpan(int startCol, int endCol, string url, string text)
		{
			StartCol = startCol;
			EndCol = endCol;
			Url = url;
			Text = text;
		}

		/// <summary>True if <paramref name="col"/> falls within <c>[StartCol, EndCol)</c>.</summary>
		/// <param name="col">The display column to test.</param>
		/// <returns>True if the column is covered by this span.</returns>
		public bool Contains(int col) => col >= StartCol && col < EndCol;
	}
}
