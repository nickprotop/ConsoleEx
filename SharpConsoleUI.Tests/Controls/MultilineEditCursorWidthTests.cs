// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for GitHub issue #23: cursor position is incorrect when the
/// line contains wide (e.g. CJK) characters. The cursor's screen column must be
/// measured in DISPLAY columns, not logical UTF-16 character indices, because the
/// renderer advances 2 columns per wide character.
/// </summary>
public class MultilineEditCursorWidthTests
{
	private static MultilineEditControl CreateEditing(string content, WrapMode wrapMode = WrapMode.NoWrap)
	{
		var control = new MultilineEditControl
		{
			WrapMode = wrapMode,
			Content = content,
			IsEditing = true
		};
		return control;
	}

	[Fact]
	public void GetLogicalCursorPosition_CursorAfterWideChars_ReturnsDisplayColumn_NoWrap()
	{
		// "中文" = two CJK chars, each 2 display columns wide.
		// Logical cursor index 2 (end of string) must map to display column 4, not 2.
		var control = CreateEditing("中文");
		control.SetLogicalCursorPosition(new Point(2, 0));

		var pos = control.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(4, pos!.Value.X);
		Assert.Equal(0, pos.Value.Y);
	}

	[Fact]
	public void GetLogicalCursorPosition_CursorBetweenWideChars_ReturnsDisplayColumn_NoWrap()
	{
		// Cursor after the first CJK char (logical index 1) sits at display column 2.
		var control = CreateEditing("中文");
		control.SetLogicalCursorPosition(new Point(1, 0));

		var pos = control.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(2, pos!.Value.X);
	}

	[Fact]
	public void GetLogicalCursorPosition_MixedNarrowAndWide_ReturnsDisplayColumn_NoWrap()
	{
		// "ab中" = a(1) + b(1) + 中(2). Cursor at logical index 3 (end) => display column 4.
		var control = CreateEditing("ab中");
		control.SetLogicalCursorPosition(new Point(3, 0));

		var pos = control.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(4, pos!.Value.X);
	}

	[Fact]
	public void GetLogicalCursorPosition_NarrowOnly_Unchanged_NoWrap()
	{
		// Pure ASCII: logical index already equals display column. Guards against regression.
		var control = CreateEditing("hello");
		control.SetLogicalCursorPosition(new Point(3, 0));

		var pos = control.GetLogicalCursorPosition();

		Assert.NotNull(pos);
		Assert.Equal(3, pos!.Value.X);
	}

	#region Mouse positioning (display column -> logical char index)

	private static (ConsoleWindowSystem system, MultilineEditControl control) CreateFocusedInWindow(string content)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system);
		var control = new MultilineEditControl { WrapMode = WrapMode.NoWrap, Content = content };

		window.AddControl(control);
		system.WindowStateService.AddWindow(window);
		system.WindowStateService.SetActiveWindow(window);
		window.FocusManager.SetFocus(control, FocusReason.Programmatic);
		system.Render.UpdateDisplay();
		return (system, control);
	}

	private static MouseEventArgs Press(int controlRelativeX, int controlRelativeY)
	{
		var pos = new Point(controlRelativeX, controlRelativeY);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Pressed },
			pos, pos, pos);
	}

	[Fact]
	public void MouseClick_OnSecondWideChar_PlacesCursorAtCorrectLogicalIndex()
	{
		// "中文" renders across display columns 0-3. Clicking at display column 2
		// (start of 文) must place the logical cursor at char index 1, not 2.
		var (_, control) = CreateFocusedInWindow("中文");

		control.ProcessMouseEvent(Press(2, 0));

		Assert.Equal(2, control.CurrentColumn); // CurrentColumn is 1-based => logical index 1
	}

	[Fact]
	public void MouseClick_PastAllWideChars_PlacesCursorAtEnd()
	{
		// Clicking at display column 4 (end of "中文") must land at logical index 2 (end).
		var (_, control) = CreateFocusedInWindow("中文");

		control.ProcessMouseEvent(Press(4, 0));

		Assert.Equal(3, control.CurrentColumn); // 1-based => logical index 2
	}

	[Fact]
	public void MouseDragSelection_AcrossWideChars_SelectsExpectedText()
	{
		// Press at column 0, drag to column 2 (start of 文) selects only "中".
		var (_, control) = CreateFocusedInWindow("中文ab");

		control.ProcessMouseEvent(Press(0, 0));
		var dragPos = new Point(2, 0);
		control.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Dragged },
			dragPos, dragPos, dragPos));

		Assert.Equal("中", control.GetSelectedText());
	}

	#endregion
}
