// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx.Controls
{
	public enum Alignment
	{
		Left,
		Center,
		Right,
		Strecth
	}

	public enum StickyPosition
	{
		None,
		Top,
		Bottom
	}

	public interface IWIndowControl : IDisposable
	{
		public int? ActualWidth { get; }
		public Alignment Alignment { get; set; }
		public IContainer? Container { get; set; }
		public Margin Margin { get; set; }
		public StickyPosition StickyPosition { get; set; }
		public int? Width { get; set; }

		public void Invalidate();

		public List<string> RenderContent(int? availableWidth, int? availableHeight);
	}

	public struct Margin
	{
		public Margin(int left, int top, int right, int bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public int Bottom { get; set; }
		public int Left { get; set; }
		public int Right { get; set; }
		public int Top { get; set; }
	}
}