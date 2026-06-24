using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ListItemReactiveTests
{
	private static (Window window, ListControl list, ListItem item) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "L", Left = 0, Top = 0, Width = 60, Height = 20 };
		var list = new ListControl();
		var item = new ListItem("Original");
		list.AddItem(item);
		window.AddControl(list);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 60, 20) });
		return (window, list, item);
	}
	private static List<string> Render(Window w) => w.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 60, 20) });

	[Fact] public void Text_Relayout() { var (w, _, i) = Build(); Assert.Equal(FrameWork.None, w.PendingWork); i.Text = "New"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void Icon_Relayout() { var (w, _, i) = Build(); i.Icon = "★"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void IconColor_Repaint() { var (w, _, i) = Build(); i.IconColor = Color.Red; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void IsEnabled_Repaint() { var (w, _, i) = Build(); i.IsEnabled = false; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void IsChecked_Repaint() { var (w, _, i) = Build(); i.IsChecked = true; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void Detached_NoThrow() { var i = new ListItem("x"); var ex = Record.Exception(() => { i.Text = "y"; i.IsChecked = true; }); Assert.Null(ex); }

	[Fact]
	public void Text_OnAddedItem_NewValueRendered_OldGone()
	{
		var (w, _, i) = Build();
		Assert.Contains(Render(w), l => l.Contains("Original"));
		i.Text = "Renamed";
		var after = Render(w);
		Assert.Contains(after, l => l.Contains("Renamed"));
		Assert.DoesNotContain(after, l => l.Contains("Original"));
	}
}
