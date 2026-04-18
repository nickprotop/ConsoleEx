// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;

namespace SharpConsoleUI.Controls
{
	public partial class VideoControl
	{
		#region Overlay

		/// <summary>
		/// Triggers the overlay to become visible and resets the auto-hide timer.
		/// Called from ProcessKey and focus events.
		/// </summary>
		private void ShowOverlay()
		{
			if (!_overlayEnabled) return;
			_overlayVisible = true;
			_overlayLastInteractionTick = Environment.TickCount64;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Checks if the overlay should auto-hide based on elapsed time since last interaction.
		/// Called from PaintDOM on every render frame.
		/// </summary>
		private void UpdateOverlayVisibility()
		{
			if (!_overlayEnabled || !_overlayVisible) return;

			long elapsed = Environment.TickCount64 - _overlayLastInteractionTick;
			if (elapsed > VideoDefaults.OverlayAutoHideMs)
			{
				_overlayVisible = false;
			}
		}

		/// <summary>
		/// Renders the overlay bar at the bottom of the video content area.
		/// Called from PaintDOM when overlay is visible.
		/// </summary>
		private void RenderOverlay(CharacterBuffer buffer, int contentX, int contentY,
			int availW, int availH, LayoutRect clipRect, Color windowBg)
		{
			if (!_overlayVisible) return;

			int overlayY = contentY + availH - VideoDefaults.OverlayHeight;
			if (overlayY < contentY || overlayY >= clipRect.Bottom || overlayY < clipRect.Y)
				return;

			// Semi-transparent dark background for the overlay bar
			var overlayBg = new Color(20, 20, 30);
			var overlayFg = new Color(220, 220, 240);
			var accentFg = new Color(100, 200, 255);

			// Build status text
			string stateIcon = _playbackState switch
			{
				VideoPlaybackState.Playing => ">",
				VideoPlaybackState.Paused => "||",
				_ => "[]",
			};

			string timeStr = FormatTime(_currentTime);
			string durationStr = DurationSeconds > 0 ? $" / {FormatTime(DurationSeconds)}" : "";
			// Show the mode actually in use (so Auto resolves to Kitty/HalfBlock in the overlay)
			string modeStr = _effectiveRenderMode.ToString();
			string loopStr = _looping ? " Loop" : "";

			string leftText = $" {stateIcon} {timeStr}{durationStr}";
			string rightText = $"{modeStr}{loopStr}  Space:Play M:Mode L:Loop ";

			// Fill overlay background
			for (int x = contentX; x < contentX + availW && x < clipRect.Right; x++)
			{
				if (x >= clipRect.X)
					buffer.SetNarrowCell(x, overlayY, ' ', overlayFg, overlayBg);
			}

			// Write left-aligned text
			for (int i = 0; i < leftText.Length && contentX + i < clipRect.Right; i++)
			{
				int x = contentX + i;
				if (x >= clipRect.X)
					buffer.SetNarrowCell(x, overlayY, leftText[i], accentFg, overlayBg);
			}

			// Write right-aligned text
			int rightStart = contentX + availW - rightText.Length;
			for (int i = 0; i < rightText.Length; i++)
			{
				int x = rightStart + i;
				if (x >= clipRect.X && x < clipRect.Right && x >= contentX)
					buffer.SetNarrowCell(x, overlayY, rightText[i], overlayFg, overlayBg);
			}
		}

		/// <summary>Formats seconds as MM:SS.</summary>
		private static string FormatTime(double seconds)
		{
			int totalSec = (int)seconds;
			int min = totalSec / 60;
			int sec = totalSec % 60;
			return $"{min:D2}:{sec:D2}";
		}

		#endregion
	}
}
