using System.Collections.ObjectModel;
using TerminalMail.Models;
using TerminalMail.UI;
using TerminalMail.ViewModels;
using SharpConsoleUI.Layout;
using Xunit;

namespace TerminalMail.Tests;

public class MessageTableDataSourceTests
{
    private static ObservableCollection<MessageViewModel> Two()
    {
        return new ObservableCollection<MessageViewModel>
        {
            new(new Message { From = "alice@example.com", Subject = "Q3 roadmap", Body = "b", Date = DateTime.Today, IsRead = false, IsFlagged = true }),
            new(new Message { From = "bob@example.com",   Subject = "Lunch?",     Body = "b", Date = DateTime.Today, IsRead = true }),
        };
    }

    [Fact]
    public void ReportsRowAndColumnCounts()
    {
        var ds = new MessageTableDataSource(Two());
        Assert.Equal(2, ds.RowCount);
        Assert.Equal(4, ds.ColumnCount); // flag, from, subject, date
    }

    [Fact]
    public void GetCellValue_ReturnsExpectedColumns()
    {
        var ds = new MessageTableDataSource(Two());
        Assert.Equal("alice@example.com", ds.GetCellValue(0, 1));
        Assert.Equal("Q3 roadmap", ds.GetCellValue(0, 2));
    }

    [Fact]
    public void CollectionChange_RaisesCollectionChanged()
    {
        var coll = Two();
        var ds = new MessageTableDataSource(coll);
        var fired = false;
        ds.CollectionChanged += (_, _) => fired = true;

        coll.Clear();

        Assert.True(fired);
        Assert.Equal(0, ds.RowCount);
    }

    [Fact]
    public void GetMessage_ReturnsRowViewModel()
    {
        var coll = Two();
        var ds = new MessageTableDataSource(coll);
        Assert.Same(coll[1], ds.GetMessage(1));
    }
}
