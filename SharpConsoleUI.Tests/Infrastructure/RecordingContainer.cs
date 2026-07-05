// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Tests.Infrastructure
{
	/// <summary>
	/// A minimal <see cref="IContainer"/> that records every <c>Invalidate</c> call and the
	/// <c>callerControl</c> it received. Used to assert which control identity reaches a container.
	/// </summary>
	public sealed class RecordingContainer : IContainer
	{
		public IWindowControl? LastCaller { get; private set; }
		public Invalidation? LastWork { get; private set; }
		public int InvalidateCount { get; private set; }
		public List<IWindowControl?> Callers { get; } = new();

		public Color BackgroundColor { get; set; } = Color.Black;
		public Color ForegroundColor { get; set; } = Color.White;
		public ConsoleWindowSystem? GetConsoleWindowSystem => null;

		public void Invalidate(Invalidation work, IWindowControl? callerControl = null)
		{
			LastWork = work;
			LastCaller = callerControl;
			Callers.Add(callerControl);
			InvalidateCount++;
		}

		public int? GetVisibleHeightForControl(IWindowControl control) => null;
	}
}
