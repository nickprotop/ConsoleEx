using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class TableColumnReactiveTests
{
	private static (Window window, TableControl table, TableColumn col) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 24);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 20 };
		var table = new TableControl();
		var col = new TableColumn("Name");
		table.AddColumn(col);
		table.AddColumn(new TableColumn("Age"));
		table.AddRow("Alice", "30");
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 80, 20) });
		return (window, table, col);
	}
	private static List<string> Render(Window w) => w.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 80, 20) });

	[Fact] public void Header_Relayout() { var (w, _, c) = Build(); Assert.Equal(FrameWork.None, w.PendingWork); c.Header = "FullName"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void Width_Relayout() { var (w, _, c) = Build(); c.Width = 20; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void Alignment_Relayout() { var (w, _, c) = Build(); c.Alignment = TextJustification.Right; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void NoWrap_Relayout() { var (w, _, c) = Build(); c.NoWrap = true; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void HeaderColor_Repaint() { var (w, _, c) = Build(); c.HeaderColor = Color.Red; Assert.Equal(FrameWork.Repaint, w.PendingWork); }

	[Fact]
	public void Header_NewValueRendered()
	{
		var (w, _, c) = Build();
		c.Header = "FullName";
		Assert.Contains(Render(w), l => l.Contains("FullName"));
	}
}
