using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Observable collection of MenuItems used for both MenuItem.Children and MenuControl.Items.
/// Overrides ClearItems so Clear() fires actionable Remove events per item — the base
/// ObservableCollection only fires a single Reset with no OldItems, which would prevent
/// MenuControl from detaching items and disposing their bindings.
/// </summary>
public sealed class MenuItemCollection : ObservableCollection<MenuItem>
{
    /// <summary>
    /// The MenuItem whose Children this collection represents, or null when this collection
    /// is the top-level MenuControl.Items. Used to assign Parent on newly-added items in O(1).
    /// </summary>
    public MenuItem? OwnerItem { get; }

    /// <summary>Creates a top-level collection (no owning MenuItem).</summary>
    public MenuItemCollection() { }

    /// <summary>Creates a Children collection owned by the given MenuItem.</summary>
    public MenuItemCollection(MenuItem? ownerItem)
    {
        OwnerItem = ownerItem;
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        var removed = this.ToList();
        base.ClearItems();
        foreach (var item in removed)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove, item, index: -1));
        }
    }
}
