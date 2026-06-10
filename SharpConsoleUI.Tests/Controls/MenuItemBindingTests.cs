using System.ComponentModel;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MenuItemBindingTests
{
	private sealed class TestVm : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;

		private bool _canSave = true;
		public bool CanSave
		{
			get => _canSave;
			set { _canSave = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave))); }
		}

		private string _label = "Save";
		public string Label
		{
			get => _label;
			set { _label = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label))); }
		}
	}

	[Fact]
	public void IsEnabled_SetterRaisesPropertyChanged()
	{
		var item = new MenuItem { Text = "A" };
		string? raised = null;
		item.PropertyChanged += (_, e) => raised = e.PropertyName;

		item.IsEnabled = false;

		Assert.Equal(nameof(MenuItem.IsEnabled), raised);
	}

	[Fact]
	public void IsEnabled_SetterDoesNotRaiseWhenValueUnchanged()
	{
		var item = new MenuItem { Text = "A", IsEnabled = true };
		bool raised = false;
		item.PropertyChanged += (_, _) => raised = true;

		item.IsEnabled = true;

		Assert.False(raised);
	}

	[Fact]
	public void Text_SetterRaisesPropertyChanged()
	{
		var item = new MenuItem();
		string? raised = null;
		item.PropertyChanged += (_, e) => raised = e.PropertyName;

		item.Text = "Hello";

		Assert.Equal(nameof(MenuItem.Text), raised);
	}

	[Fact]
	public void Shortcut_SetterRaisesPropertyChanged()
	{
		var item = new MenuItem();
		string? raised = null;
		item.PropertyChanged += (_, e) => raised = e.PropertyName;

		item.Shortcut = "Ctrl+S";

		Assert.Equal(nameof(MenuItem.Shortcut), raised);
	}

	[Fact]
	public void ForegroundColor_SetterRaisesPropertyChanged()
	{
		var item = new MenuItem();
		string? raised = null;
		item.PropertyChanged += (_, e) => raised = e.PropertyName;

		item.ForegroundColor = Color.Red;

		Assert.Equal(nameof(MenuItem.ForegroundColor), raised);
	}

	[Fact]
	public void Children_IsMenuItemCollection_WithOwnerItemSet()
	{
		var parent = new MenuItem { Text = "Parent" };
		Assert.IsType<MenuItemCollection>(parent.Children);
		Assert.Same(parent, ((MenuItemCollection)parent.Children).OwnerItem);
	}

	[Fact]
	public void Children_FiresCollectionChangedOnAdd()
	{
		var parent = new MenuItem { Text = "Parent" };
		var child = new MenuItem { Text = "Child" };

		System.Collections.Specialized.NotifyCollectionChangedAction? action = null;
		((MenuItemCollection)parent.Children).CollectionChanged += (_, e) => action = e.Action;

		parent.Children.Add(child);

		Assert.Equal(System.Collections.Specialized.NotifyCollectionChangedAction.Add, action);
	}

	[Fact]
	public void Owner_NullByDefault()
	{
		var item = new MenuItem();
		Assert.Null(item.Owner);
	}

	[Fact]
	public void Bindings_LazilyCreated_AndSameInstanceOnSubsequentCalls()
	{
		var item = new MenuItem();
		Assert.NotNull(item.Bindings);
		Assert.Same(item.Bindings, item.Bindings);
	}

	[Fact]
	public void AddingItemToMenu_SetsOwnerOnItem()
	{
		var menu = new MenuControl();
		var item = new MenuItem { Text = "File" };

		menu.AddItem(item);

		Assert.Same(menu, item.Owner);
	}

	[Fact]
	public void AddingItemWithPrePopulatedChildren_SetsOwnerRecursively()
	{
		var menu = new MenuControl();
		var parent = new MenuItem { Text = "File" };
		var child = new MenuItem { Text = "Save" };
		var grandchild = new MenuItem { Text = "All" };
		parent.Children.Add(child);
		child.Children.Add(grandchild);

		menu.AddItem(parent);

		Assert.Same(menu, parent.Owner);
		Assert.Same(menu, child.Owner);
		Assert.Same(menu, grandchild.Owner);
	}

	[Fact]
	public void RemovingItem_ClearsOwnerAndDisposesBindingsRecursively()
	{
		var menu = new MenuControl();
		var parent = new MenuItem { Text = "File" };
		var child = new MenuItem { Text = "Save" };
		parent.Children.Add(child);
		menu.AddItem(parent);

		parent.Bindings.Add(new DisposableSpy());
		child.Bindings.Add(new DisposableSpy());

		menu.RemoveItem(parent);

		Assert.Null(parent.Owner);
		Assert.Null(child.Owner);
		Assert.Throws<ObjectDisposedException>(() => parent.Bindings.Add(new DisposableSpy()));
		Assert.Throws<ObjectDisposedException>(() => child.Bindings.Add(new DisposableSpy()));
	}

	[Fact]
	public void AddingSameItemToTwoMenus_Throws()
	{
		var menu1 = new MenuControl();
		var menu2 = new MenuControl();
		var item = new MenuItem { Text = "Shared" };

		menu1.AddItem(item);

		Assert.Throws<InvalidOperationException>(() => menu2.AddItem(item));
	}

	[Fact]
	public void AddingChildToAttachedParent_SetsChildOwnerAndParent()
	{
		var menu = new MenuControl();
		var parent = new MenuItem { Text = "File" };
		menu.AddItem(parent);

		var child = new MenuItem { Text = "New" };
		parent.Children.Add(child);

		Assert.Same(menu, child.Owner);
		Assert.Same(parent, child.Parent);
	}

	[Fact]
	public void ClearingChildren_DetachesAllAndDisposesBindings()
	{
		var menu = new MenuControl();
		var parent = new MenuItem { Text = "File" };
		parent.Children.Add(new MenuItem { Text = "A" });
		parent.Children.Add(new MenuItem { Text = "B" });
		menu.AddItem(parent);

		var childA = parent.Children[0];
		childA.Bindings.Add(new DisposableSpy());

		parent.Children.Clear();

		Assert.Null(childA.Owner);
		Assert.Throws<ObjectDisposedException>(() => childA.Bindings.Add(new DisposableSpy()));
	}

	[Fact]
	public void ItemsProperty_IsLiveCollection_AddingDirectlyTriggersAttach()
	{
		var menu = new MenuControl();
		var item = new MenuItem { Text = "Direct" };

		menu.Items.Add(item);

		Assert.Same(menu, item.Owner);
		Assert.Contains(item, menu.Items);
	}

	[Fact]
	public void Bind_OneWay_FlowsSourceToTarget()
	{
		var vm = new TestVm { CanSave = false };
		var item = new MenuItem { Text = "Save" }
			.Bind(vm, x => x.CanSave, m => m.IsEnabled);

		Assert.False(item.IsEnabled);

		vm.CanSave = true;
		Assert.True(item.IsEnabled);
	}

	[Fact]
	public void Bind_OneWay_WithConverter()
	{
		var vm = new TestVm { Label = "Open" };
		var item = new MenuItem()
			.Bind(vm, x => x.Label, m => m.Text, label => "> " + label);

		Assert.Equal("> Open", item.Text);

		vm.Label = "Close";
		Assert.Equal("> Close", item.Text);
	}

	[Fact]
	public void BindTwoWay_RoundTripsBothDirections()
	{
		var vm = new TestVm { Label = "A" };
		var item = new MenuItem()
			.BindTwoWay(vm, x => x.Label, m => m.Text);

		Assert.Equal("A", item.Text);

		vm.Label = "B";
		Assert.Equal("B", item.Text);

		item.Text = "C";
		Assert.Equal("C", vm.Label);
	}

	[Fact]
	public void DetachingBoundItem_DisposesBindingsAndStopsPropagation()
	{
		var menu = new MenuControl();
		var vm = new TestVm { CanSave = true };
		var item = new MenuItem { Text = "Save" }
			.Bind(vm, x => x.CanSave, m => m.IsEnabled);
		menu.AddItem(item);

		menu.RemoveItem(item);

		// After detach, item.Bindings is disposed. VM mutation must not affect the item
		// and must not throw.
		vm.CanSave = false;
		Assert.True(item.IsEnabled);
	}

	[Fact]
	public void MenuItemBuilder_FluentBind_AppliesBindingToBuiltItem()
	{
		var vm = new TestVm { CanSave = false };

		var menu = new MenuBuilder()
			.AddItem("File", m =>
			{
				m.AddItem("Save", () => { });
				m.Bind(vm, x => x.CanSave, mi => mi.IsEnabled);
			})
			.Build();

		var fileItem = menu.Items.Single(i => i.Text == "File");
		Assert.False(fileItem.IsEnabled);

		vm.CanSave = true;
		Assert.True(fileItem.IsEnabled);
	}

	[Fact]
	public void Krokots_MenuItem_IsEnabled_BoundToVm_RoundTrips()
	{
		// Issue #21: "MenuItem.IsEnabled bound to a bool so that it can be enabled/disabled
		// from a view model (for example - loading data enables additional options)."
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var menu = new MenuControl { Orientation = MenuOrientation.Horizontal };
		window.AddControl(menu);

		var vm = new TestVm { CanSave = true };

		var saveItem = new MenuItem { Text = "Save" }
			.Bind(vm, x => x.CanSave, m => m.IsEnabled);
		menu.AddItem(saveItem);

		Assert.True(saveItem.IsEnabled);

		vm.CanSave = false;
		Assert.False(saveItem.IsEnabled);

		vm.CanSave = true;
		Assert.True(saveItem.IsEnabled);
	}

	private sealed class DisposableSpy : IDisposable
	{
		public bool Disposed { get; private set; }
		public void Dispose() => Disposed = true;
	}
}
