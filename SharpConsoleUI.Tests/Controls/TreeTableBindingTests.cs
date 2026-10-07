// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// <c>BindItems</c> shows a hierarchy of items in a <see cref="TreeTableControl"/> and keeps the
/// rows in step with the items and their collections, row by row.
/// </summary>
public class TreeTableBindingTests
{
	#region Helpers

	/// <summary>A part of a starship: a name, a crew count, and the parts it is made of.</summary>
	private sealed class Part : INotifyPropertyChanged
	{
		private string _name;
		private bool _isOpen = true;
		private ObservableCollection<Part> _parts = new();

		public Part(string name, params Part[] parts)
		{
			_name = name;
			foreach (var part in parts)
				_parts.Add(part);
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public string Name
		{
			get => _name;
			set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
		}

		public bool IsOpen
		{
			get => _isOpen;
			set { _isOpen = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpen))); }
		}

		public ObservableCollection<Part> Parts
		{
			get => _parts;
			set { _parts = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parts))); }
		}

		public override string ToString() => Name;
	}

	/// <summary>Bridge > (Helm, Sensors > (Radar)); Engine > (Warp core); Galley.</summary>
	private static ObservableCollection<Part> Starship() =>
	[
		new Part("Bridge", new Part("Helm"), new Part("Sensors", new Part("Radar"))),
		new Part("Engine", new Part("Warp core")),
		new Part("Galley"),
	];

	private static TreeTableControl Bound(ObservableCollection<Part> ship, Action<TreeTableItemsOptions<Part>>? configure = null)
	{
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Part");
		return table.BindItems(ship, part => part.Parts, part => [part.Name], configure);
	}

	private static List<string> Names(TableControl table) => table.Rows.Select(r => r.Cells[0]).ToList();

	private static List<string> Displayed(TableControl table)
		=> Enumerable.Range(0, table.RowCount).Select(i => table.GetRow(table.MapDisplayToData(i)).Cells[0]).ToList();

	private static TreeTableRow RowOf(TreeTableControl table, Part part) => (TreeTableRow)table.FindRowByTag(part)!;

	/// <summary>A list that only ever reports a reset, as many collections do.</summary>
	private sealed class ResettingList<T> : Collection<T>, INotifyCollectionChanged
	{
		public ResettingList(IEnumerable<T> items) : base(items.ToList()) { }

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public void ReplaceAll(IEnumerable<T> items)
		{
			var copy = items.ToList();
			Items.Clear();
			foreach (var item in copy)
				Items.Add(item);
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}

	#endregion

	#region Binding

	[Fact]
	public void BindItems_ShowsEveryItemAsARow_TaggedWithIt()
	{
		var ship = Starship();

		var table = Bound(ship);

		Assert.Equal(["Bridge", "Helm", "Sensors", "Radar", "Engine", "Warp core", "Galley"], Names(table));
		Assert.Same(ship[0], table.Rows[0].Tag);
		Assert.Equal(2, table.GetDepth(RowOf(table, ship[0].Parts[1].Parts[0])));
	}

	[Fact]
	public void UpdateRow_IsCalledForEveryRow()
	{
		var ship = Starship();

		var table = Bound(ship, options => options.UpdateRow = (row, part) => row.IsEnabled = part.Name != "Galley");

		Assert.False(RowOf(table, ship[2]).IsEnabled);
		Assert.True(RowOf(table, ship[0]).IsEnabled);
	}

	[Fact]
	public void TheBuilder_BindsOnceBuilt_AfterItsOwnRows()
	{
		var ship = Starship();

		var table = Builders.Controls.TreeTable()
			.AddColumn("Part")
			.AddRow("Hull")
			.BindItems(ship, part => part.Parts, part => [part.Name])
			.Build();

		Assert.Equal(["Hull", "Bridge", "Engine", "Galley"], table.RootRows.Select(r => r.Cells[0]));
	}

	#endregion

	#region Following the collections

	[Fact]
	public void AnItemAdded_GetsARowInItsPlace()
	{
		var ship = Starship();
		var table = Bound(ship);

		ship.Insert(1, new Part("Armory"));
		ship[0].Parts.Insert(0, new Part("Captain's chair"));

		Assert.Equal(["Bridge", "Captain's chair", "Helm", "Sensors", "Radar", "Armory", "Engine", "Warp core", "Galley"], Names(table));
	}

	[Fact]
	public void AnItemRemoved_TakesItsRowsWithIt()
	{
		var ship = Starship();
		var table = Bound(ship);

		ship.RemoveAt(0);

		Assert.Equal(["Engine", "Warp core", "Galley"], Names(table));
	}

	[Fact]
	public void AnItemMoved_KeepsItsRow_AndTheSelection()
	{
		var ship = Starship();
		var table = Bound(ship);
		var galley = RowOf(table, ship[2]);
		table.SelectRow(galley);

		ship.Move(2, 0);

		Assert.Equal(["Galley", "Bridge", "Engine"], table.RootRows.Select(r => r.Cells[0]));
		Assert.Same(galley, table.SelectedRow);
	}

	[Fact]
	public void AnItemReplaced_GetsANewRow_UnlessTheComparerCallsThemEqual()
	{
		var ship = Starship();
		var table = Bound(ship, options => options.ItemComparer = new NameComparer());
		var engine = RowOf(table, ship[1]);

		ship[1] = new Part("Engine", new Part("Spare core"));
		ship[2] = new Part("Observation deck");

		Assert.Same(engine, table.RootRows[1]);
		Assert.Same(ship[1], engine.Tag);
		Assert.Equal(["Engine", "Spare core"], Names(table).Skip(4).Take(2));
		Assert.Equal("Observation deck", table.RootRows[2].Cells[0]);
	}

	private sealed class NameComparer : IEqualityComparer<Part>
	{
		public bool Equals(Part? x, Part? y) => x?.Name == y?.Name;

		public int GetHashCode(Part obj) => obj.Name.GetHashCode();
	}

	[Fact]
	public void AReset_KeepsTheRowsOfItemsStillThere_InTheNewOrder()
	{
		var bridge = new Part("Bridge");
		var engine = new Part("Engine");
		var galley = new Part("Galley");
		var ship = new ResettingList<Part>([bridge, engine, galley]);
		var table = new TreeTableControl { ReadOnly = false };
		table.AddColumn("Part");
		table.BindItems(ship, part => part.Parts, part => [part.Name]);
		var engineRow = RowOf(table, engine);
		table.SelectRow(engineRow);

		ship.ReplaceAll([galley, new Part("Armory"), engine]);

		Assert.Equal(["Galley", "Armory", "Engine"], Names(table));
		Assert.Same(engineRow, table.SelectedRow);
		Assert.Null(table.FindRowByTag(bridge));
	}

	[Fact]
	public void AResetThatOnlyRemoves_LeavesTheOtherRowsWhereTheyAre()
	{
		var bridge = new Part("Bridge");
		var galley = new Part("Galley");
		var ship = new ResettingList<Part>([bridge, new Part("Engine"), galley]);
		var table = new TreeTableControl();
		table.AddColumn("Part");
		table.AddRootRow("Hull");
		table.BindItems(ship, part => part.Parts, part => [part.Name]);
		table.MoveRow(table.RootRows[0], null, 2);      // Hull between Engine and Galley

		ship.ReplaceAll([bridge, galley]);

		Assert.Equal(["Bridge", "Hull", "Galley"], Names(table));
	}

	[Fact]
	public void ClearingACollection_RemovesItsRows()
	{
		var ship = Starship();
		var table = Bound(ship);

		ship[0].Parts.Clear();

		Assert.Equal(["Bridge", "Engine", "Warp core", "Galley"], Names(table));
	}

	[Fact]
	public void RowsNotCreatedByTheBinding_KeepTheirPlaces()
	{
		var ship = Starship();
		var table = new TreeTableControl();
		table.AddColumn("Part");
		table.AddRootRow("Hull");
		table.BindItems(ship, part => part.Parts, part => [part.Name]);
		table.AddRootRow("Antenna");

		ship.Insert(0, new Part("Armory"));
		ship.Add(new Part("Brig"));
		ship.Move(0, 4);

		Assert.Equal(["Hull", "Bridge", "Engine", "Galley", "Brig", "Armory", "Antenna"], table.RootRows.Select(r => r.Cells[0]));
	}

	[Fact]
	public void EachChange_IsOneRecompute()
	{
		var ship = Starship();
		var table = new CountingTable();
		table.AddColumn("Part");
		table.BindItems(ship, part => part.Parts, part => [part.Name]);
		int before = table.Computes;

		ship[0].Parts = [new Part("Helm"), new Part("Periscope"), new Part("Sensors")];

		Assert.Equal(1, table.Computes - before);
	}

	private sealed class CountingTable : TreeTableControl
	{
		public int Computes { get; private set; }

		protected override int[]? ComputeDisplayRows(TableDisplayQuery query)
		{
			Computes++;
			return base.ComputeDisplayRows(query);
		}
	}

	#endregion

	#region Following the items

	[Fact]
	public void AnItemChanged_UpdatesItsCells()
	{
		var ship = Starship();
		var table = Bound(ship);

		ship[2].Name = "Mess hall";

		Assert.Equal("Mess hall", RowOf(table, ship[2]).Cells[0]);
	}

	[Fact]
	public void AnItemsChildrenReplaced_KeepsTheRowsOfChildrenStillThere()
	{
		var ship = Starship();
		var table = Bound(ship);
		var helm = ship[0].Parts[0];
		var helmRow = RowOf(table, helm);

		ship[0].Parts = [new Part("Periscope"), helm];

		Assert.Equal(["Bridge", "Periscope", "Helm", "Engine"], Names(table).Take(4));
		Assert.Same(helmRow, RowOf(table, helm));
	}

	[Fact]
	public void Expansion_FollowsTheItem_AndIsWrittenBack()
	{
		var ship = Starship();
		var table = Bound(ship, options =>
		{
			options.IsExpanded = part => part.IsOpen;
			options.IsExpandedChanged = (part, isOpen) => part.IsOpen = isOpen;
		});

		ship[0].IsOpen = false;
		Assert.False(RowOf(table, ship[0]).IsExpanded);

		table.Expand(RowOf(table, ship[0]));
		Assert.True(ship[0].IsOpen);
	}

	[Fact]
	public void ChildrenLoadedOnDemand_AreAskedForOnlyWhenTheRowOpens()
	{
		var ship = Starship();
		var asked = new List<string>();
		var table = new TreeTableControl();
		table.AddColumn("Part");
		table.BindItems(ship,
			part => { asked.Add(part.Name); return part.Parts; },
			part => [part.Name],
			options => options.HasUnrealizedChildren = part => part.Parts.Count > 0);

		// The galley has no parts, so it does not load on demand: its parts are asked for at once.
		Assert.Equal(["Galley"], asked);
		Assert.Equal(["Bridge", "Engine", "Galley"], Displayed(table));

		table.Expand(RowOf(table, ship[0]));

		Assert.Equal(["Galley", "Bridge", "Helm"], asked);
		Assert.Equal(["Bridge", "Helm", "Sensors", "Engine", "Galley"], Displayed(table));
	}

	#endregion

	#region Ending the binding

	[Fact]
	public void BindingAgain_ReplacesTheRowsAndStopsFollowingTheOldItems()
	{
		var ship = Starship();
		var table = Bound(ship);
		var shuttle = new ObservableCollection<Part> { new Part("Cockpit") };

		table.BindItems(shuttle, part => part.Parts, part => [part.Name]);
		ship.Add(new Part("Brig"));

		Assert.Equal(["Cockpit"], Names(table));
	}

	[Fact]
	public void BindingAgain_LetsTheOldItemsGo()
	{
		var table = new TreeTableControl();
		table.AddColumn("Part");
		var oldItem = BindAShipAndForgetIt(table);

		table.BindItems(new ObservableCollection<Part> { new Part("Cockpit") }, part => part.Parts, part => [part.Name]);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		Assert.False(oldItem.IsAlive);
	}

	/// <summary>Binds a ship nothing else holds, and returns a weak reference to one of its parts.</summary>
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static WeakReference BindAShipAndForgetIt(TreeTableControl table)
	{
		var ship = Starship();
		table.BindItems(ship, part => part.Parts, part => [part.Name]);
		return new WeakReference(ship[0].Parts[1]);
	}

	[Fact]
	public void DisposingTheBindings_StopsFollowingTheItems()
	{
		var ship = Starship();
		var table = Bound(ship);

		table.Bindings.Dispose();
		ship.Add(new Part("Brig"));
		ship[0].Name = "Command deck";

		Assert.DoesNotContain("Brig", Names(table));
		Assert.Equal("Bridge", table.Rows[0].Cells[0]);
	}

	#endregion
}
