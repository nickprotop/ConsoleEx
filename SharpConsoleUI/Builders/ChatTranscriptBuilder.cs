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
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="ChatTranscriptControl"/>.
/// </summary>
public sealed class ChatTranscriptBuilder : IControlBuilder<ChatTranscriptControl>
{
	private bool _animateMessages = true;
	private bool _autoScroll = true;
	private string? _name;
	private Margin _margin = new(0, 0, 0, 0);
	private ColorRole _role = ColorRole.Default;
	private ThemeMode? _colorRoleMode;
	private readonly List<(ChatRole Role, ChatRoleStyle Style)> _roleStyles = new();

	/// <summary>
	/// Sets whether message panels animate their expand/collapse. Defaults to <c>true</c>.
	/// </summary>
	public ChatTranscriptBuilder AnimateMessages(bool animate = true)
	{
		_animateMessages = animate;
		return this;
	}

	/// <summary>
	/// Sets whether the transcript auto-scrolls to follow the newest message. Defaults to <c>true</c>.
	/// </summary>
	public ChatTranscriptBuilder WithAutoScroll(bool autoScroll = true)
	{
		_autoScroll = autoScroll;
		return this;
	}

	/// <summary>
	/// Sets the visual style used for messages of the given role.
	/// </summary>
	/// <param name="role">The role to configure.</param>
	/// <param name="style">The style to apply.</param>
	public ChatTranscriptBuilder WithRoleStyle(ChatRole role, ChatRoleStyle style)
	{
		_roleStyles.Add((role, style));
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public ChatTranscriptBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a uniform margin on all four sides.
	/// </summary>
	public ChatTranscriptBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the margin with individual values for each side.
	/// </summary>
	public ChatTranscriptBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets the control's semantic colour role.
	/// </summary>
	/// <param name="role">The semantic role determining the control's colours.</param>
	/// <param name="mode">Optional <see cref="ThemeMode"/> override. When null, the active theme's mode is used.</param>
	public ChatTranscriptBuilder WithColorRole(ColorRole role, ThemeMode? mode = null)
	{
		_role = role;
		_colorRoleMode = mode;
		return this;
	}

	/// <summary>
	/// Builds and returns the configured <see cref="ChatTranscriptControl"/>.
	/// </summary>
	public ChatTranscriptControl Build()
	{
		var control = new ChatTranscriptControl
		{
			AnimateMessages = _animateMessages,
			AutoScroll = _autoScroll,
			Name = _name,
			Margin = _margin,
			ColorRole = _role,
			ColorRoleMode = _colorRoleMode
		};

		foreach (var (role, style) in _roleStyles)
			control.SetRoleStyle(role, style);

		return control;
	}

	/// <summary>
	/// Implicit conversion to <see cref="ChatTranscriptControl"/>.
	/// </summary>
	public static implicit operator ChatTranscriptControl(ChatTranscriptBuilder builder) => builder.Build();
}
