// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Plays video files in the terminal using half-block, ASCII, braille, or Kitty graphics
	/// rendering. Decodes frames via FFmpeg subprocess and renders at up to 30 fps.
	/// Requires FFmpeg to be installed and on the system PATH.
	/// </summary>
	public partial class VideoControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private string? _source;
		private VideoRenderMode _renderMode = VideoDefaults.DefaultRenderMode;
		private VideoRenderMode _effectiveRenderMode = VideoDefaults.DefaultRenderMode;
		private VideoPlaybackState _playbackState = VideoPlaybackState.Stopped;
		private VideoFrameReader? _frameReader;
		private IVideoFrameSink? _sink;
		private readonly object _frameLock = new();
		private CancellationTokenSource? _playbackCts;
		private int _targetFps = VideoDefaults.DefaultTargetFps;
		private long _frameCount;
		private double _currentTime;
		private bool _looping;
		private bool _isEnabled = true;
		private string? _errorMessage;
		private int _lastRenderedCols;
		private int _lastRenderedRows;
		private bool _overlayEnabled;
		private bool _overlayVisible;
		private long _overlayLastInteractionTick;

		#endregion

		#region Properties

		/// <summary>
		/// Video source — file path or URL. Accepts anything FFmpeg understands:
		/// local files, HTTP/HTTPS, RTSP, HLS (m3u8), RTMP, FTP, etc.
		/// </summary>
		public string? Source
		{
			get => _source;
			set => SetProperty(ref _source, value);
		}

		/// <summary>Path to the video file. Alias for <see cref="Source"/> for backward compatibility.</summary>
		public string? FilePath
		{
			get => _source;
			set => SetProperty(ref _source, value);
		}

		/// <summary>
		/// Requested rendering mode. May differ from <see cref="EffectiveRenderMode"/> when
		/// <see cref="VideoRenderMode.Auto"/> or <see cref="VideoRenderMode.Kitty"/> falls back
		/// to HalfBlock on terminals without Kitty graphics support.
		/// </summary>
		public VideoRenderMode RenderMode
		{
			get => _renderMode;
			set
			{
				if (_renderMode == value) return;
				_renderMode = value;
				OnPropertyChanged();
				// Invalidate current sink so the next paint (or the restarted playback) picks
				// the right one for the new mode.
				DisposeSink();
				// Mode change requires restarting FFmpeg with different pixel dimensions.
				// If currently playing, restart playback.
				if (_playbackState == VideoPlaybackState.Playing)
					RestartPlayback();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// The render mode that is actually being used. Equal to <see cref="RenderMode"/> for
		/// concrete modes (HalfBlock, Ascii, Braille); for <see cref="VideoRenderMode.Auto"/>
		/// or <see cref="VideoRenderMode.Kitty"/>, this resolves to <see cref="VideoRenderMode.Kitty"/>
		/// on Kitty-capable terminals and <see cref="VideoRenderMode.HalfBlock"/> elsewhere.
		/// </summary>
		public VideoRenderMode EffectiveRenderMode => _effectiveRenderMode;

		/// <summary>Current playback state.</summary>
		public VideoPlaybackState PlaybackState
		{
			get => _playbackState;
			private set
			{
				if (_playbackState == value) return;
				_playbackState = value;
				OnPropertyChanged();
				PlaybackStateChanged?.Invoke(this, value);
			}
		}

		/// <summary>Target playback FPS. Clamped to MinFps-MaxFps.</summary>
		public int TargetFps
		{
			get => _targetFps;
			set => SetProperty(ref _targetFps, Math.Clamp(value, VideoDefaults.MinFps, VideoDefaults.MaxFps));
		}

		/// <summary>Whether playback loops when reaching the end.</summary>
		public bool Looping
		{
			get => _looping;
			set => SetProperty(ref _looping, value);
		}

		/// <summary>Total video duration in seconds (0 if unknown).</summary>
		public double DurationSeconds => _frameReader?.DurationSeconds ?? 0;

		/// <summary>Approximate current playback position in seconds.</summary>
		public double CurrentTime => _currentTime;

		/// <summary>Total frames rendered since play started.</summary>
		public long FrameCount => _frameCount;

		/// <inheritdoc/>
		public override int? ContentWidth => null; // Stretch to fill

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => true;

		/// <summary>
		/// Gets whether this control currently has focus.
		/// </summary>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <summary>
		/// Error message to display inside the control (e.g., "FFmpeg not found").
		/// Null when no error.
		/// </summary>
		public string? ErrorMessage
		{
			get => _errorMessage;
			private set
			{
				if (_errorMessage == value) return;
				_errorMessage = value;
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Whether the bottom overlay status bar is enabled.
		/// Enable via builder: Controls.Video().WithOverlay().
		/// When enabled, the overlay appears on any key press or focus gain,
		/// then auto-hides after OverlayAutoHideMs.
		/// </summary>
		public bool OverlayEnabled
		{
			get => _overlayEnabled;
			set => SetProperty(ref _overlayEnabled, value);
		}

		#endregion

		#region Events

		/// <summary>Fired when playback state changes.</summary>
		public event EventHandler<VideoPlaybackState>? PlaybackStateChanged;

		/// <summary>Fired when playback reaches end of file.</summary>
		public event EventHandler? PlaybackEnded;

		#endregion

		#region Public Methods

		/// <summary>Starts or resumes video playback.</summary>
		public void Play()
		{
			if (string.IsNullOrEmpty(_source)) return;

			if (_playbackState == VideoPlaybackState.Paused)
			{
				PlaybackState = VideoPlaybackState.Playing;
				return;
			}

			StartPlayback();
		}

		/// <summary>Pauses playback. Call Play() to resume.</summary>
		public void Pause()
		{
			if (_playbackState == VideoPlaybackState.Playing)
				PlaybackState = VideoPlaybackState.Paused;
		}

		/// <summary>Toggles between Play and Pause.</summary>
		public void TogglePlayPause()
		{
			if (_playbackState == VideoPlaybackState.Playing)
				Pause();
			else
				Play();
		}

		/// <summary>Stops playback and releases FFmpeg resources.</summary>
		public void Stop()
		{
			StopPlayback();
		}

		/// <summary>
		/// Cycles to the next render mode. The cycle visits concrete modes only —
		/// HalfBlock, Ascii, Braille, and (on Kitty-capable terminals) Kitty — skipping
		/// <see cref="VideoRenderMode.Auto"/>. When invoked while in <see cref="VideoRenderMode.Auto"/>,
		/// the cycle begins from HalfBlock so the user can step through the concrete options.
		/// </summary>
		public void CycleRenderMode()
		{
			bool kittyAvailable = (Container?.GetConsoleWindowSystem?.ConsoleDriver is IGraphicsProtocol gp)
				&& gp.SupportsKittyGraphics;

			RenderMode = _renderMode switch
			{
				VideoRenderMode.HalfBlock => VideoRenderMode.Ascii,
				VideoRenderMode.Ascii => VideoRenderMode.Braille,
				VideoRenderMode.Braille => kittyAvailable ? VideoRenderMode.Kitty : VideoRenderMode.HalfBlock,
				VideoRenderMode.Kitty => VideoRenderMode.HalfBlock,
				VideoRenderMode.Auto => VideoRenderMode.HalfBlock,
				_ => VideoRenderMode.HalfBlock,
			};
		}

		/// <summary>
		/// Forces the active frame sink to rebuild its terminal-side state on the next frame.
		/// Useful as a recovery keybind in Kitty mode — the graphics protocol has no explicit
		/// "redraw placement" primitive, so live frame updates can (rarely) leave the terminal
		/// holding stale pixels until something re-emits the placement. This method triggers
		/// the same reset that happens on window resize, without requiring the user to resize.
		/// No-op in cell render modes.
		/// </summary>
		public void RefreshKittyImage() => _sink?.ForceRefresh();

		/// <summary>Loads and immediately starts playing a video file.</summary>
		public void PlayFile(string filePath)
		{
			Stop();
			Source = filePath;
			Play();
		}

		/// <summary>
		/// Starts streaming from any source FFmpeg supports: HTTP/HTTPS URLs,
		/// RTSP streams, HLS playlists (m3u8), RTMP, FTP, local files, etc.
		/// </summary>
		/// <param name="url">Source URL or path. Anything FFmpeg's -i accepts.</param>
		public void Stream(string url)
		{
			Stop();
			Source = url;
			Play();
		}

		#endregion

		#region Sink Resolution

		/// <summary>
		/// Returns the current frame sink, creating one on first access (or after a mode
		/// change that invalidated the previous sink). Resolution table:
		/// <list type="bullet">
		///   <item><c>Auto</c> + Kitty support → <see cref="KittyVideoFrameSink"/> (silent).</item>
		///   <item><c>Auto</c> + no Kitty → <see cref="CellVideoFrameSink"/> in HalfBlock (silent).</item>
		///   <item><c>Kitty</c> + Kitty support → <see cref="KittyVideoFrameSink"/>.</item>
		///   <item><c>Kitty</c> + no Kitty → <see cref="CellVideoFrameSink"/> in HalfBlock + warning surfaced via <see cref="ErrorMessage"/>.</item>
		///   <item>Any concrete cell mode → <see cref="CellVideoFrameSink"/> in that mode.</item>
		/// </list>
		/// </summary>
		private IVideoFrameSink ResolveSink()
		{
			if (_sink != null) return _sink;

			var gp = Container?.GetConsoleWindowSystem?.ConsoleDriver as IGraphicsProtocol;
			bool kittySupported = gp != null && gp.SupportsKittyGraphics;

			switch (_renderMode)
			{
				case VideoRenderMode.Auto:
					if (kittySupported)
					{
						_sink = new KittyVideoFrameSink(gp!);
						_effectiveRenderMode = VideoRenderMode.Kitty;
					}
					else
					{
						_sink = new CellVideoFrameSink(VideoRenderMode.HalfBlock);
						_effectiveRenderMode = VideoRenderMode.HalfBlock;
					}
					break;

				case VideoRenderMode.Kitty:
					if (kittySupported)
					{
						_sink = new KittyVideoFrameSink(gp!);
						_effectiveRenderMode = VideoRenderMode.Kitty;
					}
					else
					{
						_sink = new CellVideoFrameSink(VideoRenderMode.HalfBlock);
						_effectiveRenderMode = VideoRenderMode.HalfBlock;
						ErrorMessage = "Kitty graphics not supported by this terminal —\nfalling back to HalfBlock rendering.";
					}
					break;

				default:
					_sink = new CellVideoFrameSink(_renderMode);
					_effectiveRenderMode = _renderMode;
					break;
			}

			return _sink;
		}

		private void DisposeSink()
		{
			IVideoFrameSink? sink;
			lock (_frameLock)
			{
				sink = _sink;
				_sink = null;
			}
			if (sink != null)
			{
				try { sink.OnStopped(); } catch { /* terminal-side cleanup is best-effort */ }
				sink.Dispose();
			}
		}

		#endregion

		#region Disposal

		/// <summary>
		/// Called by BaseControl.Dispose(). Stops playback and kills FFmpeg process.
		/// </summary>
		protected override void OnDisposing()
		{
			StopPlayback();
			DisposeSink();
			PlaybackStateChanged = null;
			PlaybackEnded = null;
		}

		#endregion
	}
}
