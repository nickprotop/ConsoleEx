using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MenuItemReactiveTests
{
	private static (ConsoleWindowSystem system, Window window, MenuControl menu, MenuItem item) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "M", Left = 0, Top = 0, Width = 60, Height = 20 };
		var menu = new MenuControl();
		var item = new MenuItem("File");
		menu.Items.Add(item);
		window.AddControl(menu);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 60, 20) });
		return (system, window, menu, item);
	}

	[Fact]
	public void IsSeparator_OnAddedItem_InvalidatesAtRelayout()
	{
		var (_, window, _, item) = Build();
		Assert.Equal(FrameWork.None, window.PendingWork);
		item.IsSeparator = true;
		Assert.Equal(FrameWork.Relayout, window.PendingWork);
	}
}
