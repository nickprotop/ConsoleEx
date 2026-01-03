// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Core;

namespace SharpConsoleUI.Controls
{
	public enum Alignment
	{
		Left,
		Center,
		Right,
		Stretch
	}

	public enum StickyPosition
	{
		None,
		Top,
		Bottom
	}

	public interface IWindowControl : IDisposable
	{
		public int? ActualWidth { get; }
		public Alignment Alignment { get; set; }
		public IContainer? Container { get; set; }
		public Margin Margin { get; set; }
		public StickyPosition StickyPosition { get; set; }
		public object? Tag { get; set; }
		public bool Visible { get; set; }
		public int? Width { get; set; }

		/// <summary>
		/// Gets the logical size of the control's content
		/// </summary>
		Size GetLogicalContentSize();

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

	/// <summary>
	/// Optional interface for controls that participate in layout negotiation.
	/// Controls implementing this interface can express their sizing requirements
	/// and receive notifications when their allocated space changes.
	/// </summary>
	public interface ILayoutAware
	{
		/// <summary>
		/// Gets the control's layout requirements.
		/// Called during the measure phase before RenderContent.
		/// </summary>
		LayoutRequirements GetLayoutRequirements();

		/// <summary>
		/// Notifies the control of its layout allocation.
		/// Called after layout calculation, before RenderContent.
		/// </summary>
		void OnLayoutAllocated(LayoutAllocation allocation);
	}
}