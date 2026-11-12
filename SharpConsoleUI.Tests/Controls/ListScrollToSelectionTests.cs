// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Setting <see cref="ListControl.SelectedIndex"/> scrolls the selection into view.
/// </summary>
/// <remarks>
/// The setter moved the selection without touching the scroll offset, so selecting an item below
/// the fold left the viewport showing the first page and the selected row invisible. Keyboard
/// navigation was unaffected because it adjusts the offset itself.
///
/// It also made the mouse wheel appear broken: a log panel that follows its tail by assigning
/// SelectedIndex kept an offset of 0, and ListControl's WheeledUp declines when the offset is
/// already 0 so the parent can scroll instead. Nothing moved, in either direction.
/// </remarks>
public class ListScrollToSelectionTests
{
	private const int Width = 30;
	private const int Height = 6;

	/// <summary>
	/// A list inside a real window, which is what gives it a Container and therefore a visible
	/// height. A bare control reports that everything fits, so nothing is ever below the fold
	/// and the bug cannot reproduce.
	/// </summary>
	private static (ListControl list, Window window) ListOf(int count)
	{
		var system = Infrastructure.TestWindowSystemBuilder.CreateTestSystem(Width + 4, Height + 4);
		var window = new Window(system) { Left = 0, Top = 0, Width = Width + 2, Height = Height + 2 };

		var list = new ListControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		for (int i = 0; i < count; i++)
			list.AddItem(new ListItem($"item{i:D2}"));

		window.AddControl(list);
		system.AddWindow(window);
		Render(window);
		return (list, window);
	}

	private static List<string> Render(Window window)
	{
		var text = ContainerTestHelpers.StripAnsiCodes(
			window.RenderAndGetVisibleContent());
		return text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
	}

	[Fact]
	public void SelectingAnItemBelowTheFold_ScrollsItIntoView()
	{
		var (list, window) = ListOf(30);

		list.SelectedIndex = 29;
		var lines = Render(window);

		Assert.Contains(lines, l => l.Contains("item29"));
	}

	[Fact]
	public void SelectingAnItemAboveTheFold_ScrollsBackUp()
	{
		var (list, window) = ListOf(30);

		list.SelectedIndex = 29;
		Render(window);

		list.SelectedIndex = 0;
		var lines = Render(window);

		Assert.Contains(lines, l => l.Contains("item00"));
	}

	[Fact]
	public void SelectingAnItemAlreadyVisible_DoesNotScroll()
	{
		var (list, window) = ListOf(30);
		var before = Render(window);

		list.SelectedIndex = 1;
		var after = Render(window);

		Assert.Equal(before, after);
	}
}
