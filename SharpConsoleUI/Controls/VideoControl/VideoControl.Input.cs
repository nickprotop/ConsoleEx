// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class VideoControl
	{
		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo keyInfo)
		{
			if (!_isEnabled) return false;

			// Any key interaction shows the overlay
			ShowOverlay();

			switch (keyInfo.Key)
			{
				case ConsoleKey.Spacebar:
					TogglePlayPause();
					return true;

				case ConsoleKey.M:
					CycleRenderMode();
					return true;

				case ConsoleKey.Escape:
					Stop();
					return true;

				case ConsoleKey.L:
					Looping = !Looping;
					return true;

				default:
					return false;
			}
		}

		/// <inheritdoc/>
		public bool WantsTabKey => false;

		#endregion
	}
}
