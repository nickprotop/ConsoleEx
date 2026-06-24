using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class PromptControlReactiveTests
{
	[Fact]
	public void IsEnabled_OnAddedControl_InvalidatesAtRepaint()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "P", Left = 0, Top = 0, Width = 60, Height = 10 };
		var prompt = new PromptControl();
		window.AddControl(prompt);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<System.Drawing.Rectangle> { new(0, 0, 60, 10) });
		Assert.Equal(FrameWork.None, window.PendingWork);

		prompt.IsEnabled = false;

		Assert.Equal(FrameWork.Repaint, window.PendingWork);
	}
}
