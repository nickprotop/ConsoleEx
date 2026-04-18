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

			if (string.IsNullOrEmpty(_source)) return;

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

			// Release any terminal-side state held by the sink (e.g. transmitted Kitty images)
			// without destroying the sink itself — the user may press Play again and we want
			// the mode resolution to stick.
			_sink?.OnStopped();

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

				// Clear any previous error (only if it was a transient one like FFmpeg-not-found;
				// the Kitty-fallback warning surfaced by ResolveSink is intentional and sticky)
				Container?.GetConsoleWindowSystem?.EnqueueOnUIThread(() =>
				{
					if (ErrorMessage == VideoDefaults.FfmpegNotFoundMessage)
						ErrorMessage = null;
				});

				// Determine target cell size from current layout bounds
				int cellCols = Math.Max(1, ActualWidth - Margin.Left - Margin.Right);
				int cellRows = Math.Max(1, ActualHeight - Margin.Top - Margin.Bottom);

				// If control hasn't been laid out yet, use reasonable defaults
				if (cellCols <= 1 || cellRows <= 1)
				{
					cellCols = VideoDefaults.FallbackCellCols;
					cellRows = VideoDefaults.FallbackCellRows;
				}

				var sink = ResolveSink();
				var (pixW, pixH) = sink.GetPreferredPixelSize(cellCols, cellRows);

				// Ensure even dimensions (FFmpeg requirement for many codecs)
				pixW = Math.Max(2, pixW + (pixW % 2));
				pixH = Math.Max(2, pixH + (pixH % 2));

				_frameReader = VideoFrameReader.Open(_source!, pixW, pixH, _currentTime);

				double fps = Math.Min(_frameReader.Fps, _targetFps);
				double spf = 1.0 / fps; // seconds per frame
				var frameBuffer = new byte[_frameReader.FrameSize];

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

					// Hand the frame to the sink (cell mode: renders to cells; Kitty mode:
					// transmits to terminal). The sink is thread-safe against concurrent Paint.
					// We pass the target cell dimensions explicitly so the Kitty sink transmits
					// at the correct placement size on the very first frame — a Paint hasn't
					// necessarily run yet when the first frame arrives.
					var bg = Container?.BackgroundColor ?? Color.Black;
					sink.IngestFrame(frameBuffer, _frameReader.Width, _frameReader.Height, cellCols, cellRows, bg);

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
