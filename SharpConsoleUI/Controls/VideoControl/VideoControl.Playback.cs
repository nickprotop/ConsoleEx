// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;

namespace SharpConsoleUI.Controls
{
	public partial class VideoControl
	{
		#region Playback Lifecycle

		private void StartPlayback()
		{
			StartPlaybackAtTime(0);
		}

		private void StartPlaybackAtTime(double seekTime)
		{
			StopPlayback();

			if (string.IsNullOrEmpty(_filePath)) return;

			_playbackCts = new CancellationTokenSource();
			_frameCount = 0;
			_currentTime = seekTime;
			PlaybackState = VideoPlaybackState.Playing;

			var ct = _playbackCts.Token;
			_ = Task.Run(() => PlaybackLoopAsync(ct), ct);
		}

		private void StopPlayback()
		{
			_playbackCts?.Cancel();
			_playbackCts?.Dispose();
			_playbackCts = null;

			_frameReader?.Dispose();
			_frameReader = null;

			PlaybackState = VideoPlaybackState.Stopped;
		}

		private void RestartPlayback()
		{
			if (_playbackState == VideoPlaybackState.Stopped) return;
			double savedTime = _currentTime;
			StartPlaybackAtTime(savedTime);
		}

		#endregion

		#region Playback Loop

		private async Task PlaybackLoopAsync(CancellationToken ct)
		{
			try
			{
				// Check FFmpeg availability first — show friendly message if missing
				if (!VideoFrameReader.IsFfmpegAvailable())
				{
					Container?.GetConsoleWindowSystem?.EnqueueOnUIThread(() =>
					{
						ErrorMessage = VideoDefaults.FfmpegNotFoundMessage;
						PlaybackState = VideoPlaybackState.Stopped;
					});
					return;
				}

				// Clear any previous error
				Container?.GetConsoleWindowSystem?.EnqueueOnUIThread(() => ErrorMessage = null);

				// Determine target cell size from current layout bounds
				int cellCols = Math.Max(1, ActualWidth - Margin.Left - Margin.Right);
				int cellRows = Math.Max(1, ActualHeight - Margin.Top - Margin.Bottom);

				// If control hasn't been laid out yet, use reasonable defaults
				if (cellCols <= 1 || cellRows <= 1)
				{
					cellCols = VideoDefaults.FallbackCellCols;
					cellRows = VideoDefaults.FallbackCellRows;
				}

				var (pixW, pixH) = VideoFrameRenderer.GetRequiredPixelSize(cellCols, cellRows, _renderMode);

				// Ensure even dimensions (FFmpeg requirement for many codecs)
				pixW = Math.Max(2, pixW + (pixW % 2));
				pixH = Math.Max(2, pixH + (pixH % 2));

				_frameReader = VideoFrameReader.Open(_filePath!, pixW, pixH, _currentTime);

				double fps = Math.Min(_frameReader.Fps, _targetFps);
				double spf = 1.0 / fps; // seconds per frame
				var frameBuffer = new byte[_frameReader.FrameSize];

				// Pre-allocate cell buffer to avoid per-frame GC allocation (CLAUDE.md rule 3)
				var (expectedCellW, expectedCellH) = VideoFrameRenderer.GetCellDimensions(
					_frameReader.Width, _frameReader.Height, _renderMode);
				var cellBuffer = new Cell[expectedCellW, expectedCellH];

				var sw = Stopwatch.StartNew();

				while (!ct.IsCancellationRequested)
				{
					// Handle pause — stop the stopwatch to freeze elapsed time
					if (_playbackState == VideoPlaybackState.Paused)
					{
						sw.Stop();
						await Task.Delay(VideoDefaults.PausePollDelayMs, ct);
						continue;
					}

					// Resume: restart stopwatch if it was stopped by pause
					if (!sw.IsRunning)
						sw.Start();

					double targetTime = _frameCount * spf;
					double elapsed = sw.Elapsed.TotalSeconds;

					// If we're behind, skip frames (but cap skips)
					if (elapsed > targetTime + spf * VideoDefaults.FrameSkipThreshold)
					{
						long expectedFrame = (long)(elapsed / spf);
						long framesToSkip = Math.Min(
							expectedFrame - _frameCount,
							VideoDefaults.MaxFrameSkip);

						for (long i = 0; i < framesToSkip && !ct.IsCancellationRequested; i++)
						{
							if (!await _frameReader.ReadFrameAsync(frameBuffer, ct))
							{
								HandlePlaybackEnd();
								return;
							}
							_frameCount++;
						}
					}

					// Read next frame
					if (!await _frameReader.ReadFrameAsync(frameBuffer, ct))
					{
						HandlePlaybackEnd();
						return;
					}

					// Render frame into pre-allocated cell buffer
					var bg = Container?.BackgroundColor ?? Color.Black;
					VideoFrameRenderer.RenderFrameInto(
						cellBuffer, frameBuffer, _frameReader.Width, _frameReader.Height,
						_renderMode, bg,
						out int cw, out int ch);

					// Swap frame reference (thread-safe)
					lock (_frameLock)
					{
						_currentFrameCells = cellBuffer;
						_frameCellWidth = cw;
						_frameCellHeight = ch;
					}

					_frameCount++;
					_currentTime = _frameCount * spf;

					// Signal repaint
					Container?.Invalidate(true);

					// Sleep until next frame target time
					double nextTarget = _frameCount * spf;
					double sleepMs = (nextTarget - sw.Elapsed.TotalSeconds) * 1000;
					if (sleepMs > VideoDefaults.MinSleepThresholdMs)
						await Task.Delay((int)sleepMs, ct);
				}
			}
			catch (OperationCanceledException)
			{
				// Normal cancellation
			}
			catch (Exception)
			{
				// FFmpeg failed or other error — stop gracefully
				Container?.GetConsoleWindowSystem?.EnqueueOnUIThread(() =>
				{
					PlaybackState = VideoPlaybackState.Stopped;
				});
			}
		}

		/// <summary>
		/// Handles end-of-stream. Dispatches to UI thread for looping or stop
		/// to avoid modifying UI state from the background thread (CLAUDE.md rule 13).
		/// </summary>
		private void HandlePlaybackEnd()
		{
			Container?.GetConsoleWindowSystem?.EnqueueOnUIThread(() =>
			{
				if (_looping)
				{
					StartPlayback();
				}
				else
				{
					PlaybackState = VideoPlaybackState.Stopped;
					PlaybackEnded?.Invoke(this, EventArgs.Empty);
				}
			});
		}

		#endregion
	}
}
