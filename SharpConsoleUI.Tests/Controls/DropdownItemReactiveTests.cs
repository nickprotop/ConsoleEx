using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class DropdownItemReactiveTests
{
	private static (Window window, DropdownControl dd, DropdownItem item) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "D", Left = 0, Top = 0, Width = 60, Height = 12 };
		var dd = new DropdownControl();
		var item = new DropdownItem("Original");
		dd.AddItem(item);
		window.AddControl(dd);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 60, 12) });
		return (window, dd, item);
	}

	[Fact] public void Text_Relayout() { var (w, _, i) = Build(); Assert.Equal(FrameWork.None, w.PendingWork); i.Text = "New"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void Icon_Relayout() { var (w, _, i) = Build(); i.Icon = "★"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void IconColor_Repaint() { var (w, _, i) = Build(); i.IconColor = Color.Red; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void IsEnabled_Repaint() { var (w, _, i) = Build(); i.IsEnabled = false; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void Detached_NoThrow() { var i = new DropdownItem("x"); var ex = Record.Exception(() => { i.Text = "y"; i.IsEnabled = false; }); Assert.Null(ex); }

	[Fact]
	public void SelectedItemText_NewValueRendered()
	{
		var (w, dd, i) = Build(); // first item auto-selected → its Text shows in the closed header
		i.Text = "Renamed";
		var after = w.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 60, 12) });
		Assert.Contains(after, l => l.Contains("Renamed"));
	}
}
