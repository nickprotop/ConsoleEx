﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx.Contents
{
	public enum Alignment
	{
		Left,
		Center,
		Right
	}

	public enum StickyPosition
	{
		None,
		Top,
		Bottom
	}

	public interface IWIndowContent : IDisposable
	{
		public int? ActualWidth { get; }
		public Alignment Alignment { get; set; }
		public IContainer? Container { get; set; }
		public StickyPosition StickyPosition { get; set; }
		public int? Width { get; set; }

		public void Invalidate();

		public List<string> RenderContent(int? availableWidth, int? availableHeight);
	}
}