// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using System.Drawing;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.Tests.Controls;

internal static class ContainerTestHelpers
{
	public const int TestSystemWidth = 120;
	public const int TestSystemHeight = 40;
	public const int TestWindowWidth = 100;
	public const int TestWindowHeight = 30;

	public static string StripAnsiCodes(IEnumerable<string> lines)
	{
		return string.Join("\n", lines.Select(line =>
			Regex.Replace(line, @"\x1b\[[0-9;]*m", "")));
	}

	public static MarkupControl CreateLabel(string text)
	{
		return new MarkupControl(new List<string> { text });
	}

	public static ListControl CreateFocusableList(params string[] items)
	{
		return new ListControl(items);
	}

	public static ButtonControl CreateButton(string text)
	{
		return new ButtonControl { Text = text };
	}

	public static (ConsoleWindowSystem system, Window window) CreateTestEnvironment(
		int sysW = TestSystemWidth, int sysH = TestSystemHeight,
		int winW = TestWindowWidth, int winH = TestWindowHeight)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(sysW, sysH);
		var window = new Window(system) { Width = winW, Height = winH };
		return (system, window);
	}

	public static MouseEventArgs CreateWheelUp(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledUp },
			pos, pos, pos);
	}

	public static MouseEventArgs CreateWheelDown(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown },
			pos, pos, pos);
	}

	public static MouseEventArgs CreateClick(int x, int y)
	{
		var pos = new Point(x, y);
		return new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			pos, pos, pos);
	}
}
