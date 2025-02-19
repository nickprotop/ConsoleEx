// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
	public enum Alignment
	{
		Left,
		Center,
		Right
	}

	public interface IWIndowContent : IDisposable
	{
		public IContainer? Container { get; set; }
		public int? Width { get; set; }
		public int? ActualWidth { get; }
		public Alignment Alignment { get; set; }

		public void Invalidate();

		public List<string> RenderContent(int? availableWidth, int? availableHeight);
	}
}
