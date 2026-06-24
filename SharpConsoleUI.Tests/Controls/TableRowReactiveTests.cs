using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class TableRowReactiveTests
{
	private static (Window window, TableControl table, TableRow row) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 24);
		var window = new Window(system) { Title = "T", Left = 0, Top = 0, Width = 80, Height = 20 };
		var table = new TableControl();
		table.AddColumn(new TableColumn("Name"));
		table.AddColumn(new TableColumn("Age"));
		var row = new TableRow("Alice", "30");
		table.AddRow(row);
		window.AddControl(table);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 80, 20) });
		return (window, table, row);
	}
	private static List<string> Render(Window w) => w.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, 80, 20) });

	[Fact] public void CellViaIndexer_Relayout() { var (w, _, r) = Build(); Assert.Equal(FrameWork.None, w.PendingWork); r[0] = "Bob"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void CellViaCellsIndexer_Relayout() { var (w, _, r) = Build(); r.Cells[0] = "Bob"; Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void CellsAdd_Relayout() { var (w, _, r) = Build(); r.Cells.Add("extra"); Assert.Equal(FrameWork.Relayout, w.PendingWork); }
	[Fact] public void BackgroundColor_Repaint() { var (w, _, r) = Build(); r.BackgroundColor = Color.Blue; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void IsChecked_Repaint() { var (w, _, r) = Build(); r.IsChecked = true; Assert.Equal(FrameWork.Repaint, w.PendingWork); }
	[Fact] public void Cells_IsObservableCollection() { var r = new TableRow("a"); Assert.IsType<ObservableCollection<string>>(r.Cells); }
	[Fact] public void Detached_NoThrow() { var r = new TableRow("a"); var ex = Record.Exception(() => { r[0] = "b"; r.IsChecked = true; }); Assert.Null(ex); }

	[Fact]
	public void CellEdit_NewValueRendered()
	{
		var (w, _, r) = Build();
		r[0] = "Bobby";
		Assert.Contains(Render(w), l => l.Contains("Bobby"));
	}
}
