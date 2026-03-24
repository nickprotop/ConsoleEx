// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.FocusManagement;

public class HorizontalGridFocusScopeTests
{
    private static (Window window, HorizontalGridControl hGrid, ButtonControl b1, SplitterControl splitter, ButtonControl b2)
        BuildTwoColumnGrid()
    {
        var system = TestWindowSystemBuilder.CreateTestSystem();
        var window = new Window(system) { Width = 80, Height = 25 };
        var hGrid = new HorizontalGridControl();

        var b1 = new ButtonControl { Text = "B1" };
        var col1 = new ColumnContainer(hGrid);
        col1.AddContent(b1);
        hGrid.AddColumn(col1);

        var b2 = new ButtonControl { Text = "B2" };
        var col2 = new ColumnContainer(hGrid);
        col2.AddContent(b2);
        hGrid.AddColumn(col2);

        // Add a splitter between the two columns
        hGrid.AddSplitter(0, new SplitterControl());

        window.AddControl(hGrid);

        // Get the splitter that HGrid created between columns
        var splitter = hGrid.GetChildren().OfType<SplitterControl>().First();
        return (window, hGrid, b1, splitter, b2);
    }

    [Fact]
    public void GetNextFocus_FromFirstButton_ReturnsSplitter()
    {
        var (_, hGrid, b1, splitter, _) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Equal(splitter, scope.GetNextFocus(b1, backward: false));
    }

    [Fact]
    public void GetNextFocus_FromSplitter_ReturnsSecondColumnButton()
    {
        var (_, hGrid, _, splitter, b2) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Equal(b2, scope.GetNextFocus(splitter, backward: false));
    }

    [Fact]
    public void GetNextFocus_FromLastControl_ReturnsNull_ToExitScope()
    {
        var (_, hGrid, _, _, b2) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Null(scope.GetNextFocus(b2, backward: false));
    }

    [Fact]
    public void GetInitialFocus_ReturnsFirstButton_WhenForward()
    {
        var (_, hGrid, b1, _, _) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Equal(b1, scope.GetInitialFocus(backward: false));
    }

    [Fact]
    public void GetInitialFocus_ReturnsLastButton_WhenBackward()
    {
        var (_, hGrid, _, _, b2) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Equal(b2, scope.GetInitialFocus(backward: true));
    }

    [Fact]
    public void SavedFocus_IsIgnoredByGetInitialFocus_AndReturnsFirstControl()
    {
        // HGrid does NOT restore saved focus — GetInitialFocus always uses first/last
        var (_, hGrid, b1, _, b2) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        scope.SavedFocus = b2;
        // SavedFocus is required by interface but HGrid ignores it
        Assert.Equal(b1, scope.GetInitialFocus(backward: false));
    }

    [Fact]
    public void GetNextFocus_Backward_FromLastButton_ReturnsSplitter()
    {
        var (_, hGrid, _, splitter, b2) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Equal(splitter, scope.GetNextFocus(b2, backward: true));
    }

    [Fact]
    public void GetNextFocus_Backward_FromFirstButton_ReturnsNull_ToExitScope()
    {
        var (_, hGrid, b1, _, _) = BuildTwoColumnGrid();
        var scope = (IFocusScope)hGrid;
        Assert.Null(scope.GetNextFocus(b1, backward: true));
    }
}
