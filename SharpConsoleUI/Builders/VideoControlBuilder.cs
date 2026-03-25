// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Video;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="VideoControl"/>.
/// </summary>
public sealed class VideoControlBuilder : IControlBuilder<VideoControl>
{
	private readonly VideoControl _control = new();

	/// <summary>Sets the video file path.</summary>
	public VideoControlBuilder WithFile(string filePath)
	{
		_control.FilePath = filePath;
		return this;
	}

	/// <summary>Sets the render mode.</summary>
	public VideoControlBuilder WithRenderMode(VideoRenderMode mode)
	{
		_control.RenderMode = mode;
		return this;
	}

	/// <summary>Sets the target frames per second.</summary>
	public VideoControlBuilder WithTargetFps(int fps)
	{
		_control.TargetFps = fps;
		return this;
	}

	/// <summary>Enables looping playback.</summary>
	public VideoControlBuilder WithLooping(bool loop = true)
	{
		_control.Looping = loop;
		return this;
	}

	/// <summary>Enables the bottom overlay status bar (auto-show on interaction, auto-hide after 3s).</summary>
	public VideoControlBuilder WithOverlay(bool enabled = true)
	{
		_control.OverlayEnabled = enabled;
		return this;
	}

	/// <summary>Sets horizontal alignment.</summary>
	public VideoControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_control.HorizontalAlignment = alignment;
		return this;
	}

	/// <summary>Sets vertical alignment.</summary>
	public VideoControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_control.VerticalAlignment = alignment;
		return this;
	}

	/// <summary>Stretch horizontally to fill.</summary>
	public VideoControlBuilder Stretch()
	{
		_control.HorizontalAlignment = HorizontalAlignment.Stretch;
		return this;
	}

	/// <summary>Fill vertically and stretch horizontally.</summary>
	public VideoControlBuilder Fill()
	{
		_control.VerticalAlignment = VerticalAlignment.Fill;
		_control.HorizontalAlignment = HorizontalAlignment.Stretch;
		return this;
	}

	/// <summary>Sets control margin.</summary>
	public VideoControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_control.Margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>Sets the control name.</summary>
	public VideoControlBuilder WithName(string name)
	{
		_control.Name = name;
		return this;
	}

	/// <summary>Subscribes to playback state changes.</summary>
	public VideoControlBuilder OnPlaybackStateChanged(EventHandler<VideoPlaybackState> handler)
	{
		_control.PlaybackStateChanged += handler;
		return this;
	}

	/// <summary>Subscribes to playback ended.</summary>
	public VideoControlBuilder OnPlaybackEnded(EventHandler handler)
	{
		_control.PlaybackEnded += handler;
		return this;
	}

	/// <summary>Builds the VideoControl.</summary>
	public VideoControl Build() => _control;
}
