using System.Collections.Specialized;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MenuItemCollectionTests
{
    [Fact]
    public void Clear_FiresRemoveEventForEachItem_NotJustReset()
    {
        var collection = new MenuItemCollection();
        var a = new MenuItem { Text = "A" };
        var b = new MenuItem { Text = "B" };
        collection.Add(a);
        collection.Add(b);

        var events = new List<NotifyCollectionChangedEventArgs>();
        collection.CollectionChanged += (_, e) => events.Add(e);

        collection.Clear();

        var removes = events.Where(e => e.Action == NotifyCollectionChangedAction.Remove).ToList();
        Assert.Equal(2, removes.Count);
        Assert.Contains(removes, e => e.OldItems != null && e.OldItems.Contains(a));
        Assert.Contains(removes, e => e.OldItems != null && e.OldItems.Contains(b));
    }

    [Fact]
    public void OwnerItem_NullByDefault()
    {
        var collection = new MenuItemCollection();
        Assert.Null(collection.OwnerItem);
    }

    [Fact]
    public void OwnerItem_SetViaConstructor()
    {
        var parent = new MenuItem { Text = "Parent" };
        var collection = new MenuItemCollection(parent);
        Assert.Same(parent, collection.OwnerItem);
    }
}
