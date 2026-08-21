// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// Right-click extending the multi-selection is opt-in: by default right-click selects a single
	/// row and fires MouseRightClick, which is what a context menu acting on the current selection
	/// expects. Hosts on terminals that swallow Shift+Click can turn the range extension on.
	/// </summary>
	public class TableRightClickExtendTests
	{
		private static TableControl Build(bool multiSelect, bool rightClickExtends)
		{
			var table = new TableControl
			{
				MultiSelectEnabled = multiSelect,
				RightClickExtendsSelection = rightClickExtends,
			};
			table.AddColumn(new TableColumn("Name", TextJustification.Left, null));
			for (int i = 0; i < 10; i++)
				table.AddRow($"row {i}");
			return table;
		}

		[Fact]
		public void DefaultIsOff()
		{
			var table = new TableControl();
			Assert.False(table.RightClickExtendsSelection);
		}

		[Fact]
		public void Off_RightClickSelectsOnlyTheClickedRow()
		{
			var table = Build(multiSelect: true, rightClickExtends: false);
			table.SelectedRowIndex = 2;              // anchor here
			table.RightClickRowForTest(6);

			Assert.Equal(6, table.SelectedRowIndex);
			// The range 2..6 must NOT have been selected.
			Assert.True(table.SelectedRowIndicesForTest().Count <= 1,
				$"expected at most one selected row, got {table.SelectedRowIndicesForTest().Count}");
		}

		[Fact]
		public void On_RightClickExtendsFromTheAnchor()
		{
			var table = Build(multiSelect: true, rightClickExtends: true);
			table.SelectedRowIndex = 2;
			table.RightClickRowForTest(6);

			Assert.Equal(6, table.SelectedRowIndex);
			var sel = table.SelectedRowIndicesForTest();
			Assert.Equal(5, sel.Count);              // rows 2,3,4,5,6
			foreach (int i in new[] { 2, 3, 4, 5, 6 })
				Assert.Contains(i, sel);
		}

		[Fact]
		public void On_ButWithoutMultiSelect_DoesNotExtend()
		{
			// The option requires MultiSelectEnabled; alone it must do nothing.
			var table = Build(multiSelect: false, rightClickExtends: true);
			table.SelectedRowIndex = 2;
			table.RightClickRowForTest(6);

			Assert.Equal(6, table.SelectedRowIndex);
			Assert.True(table.SelectedRowIndicesForTest().Count <= 1);
		}

		[Fact]
		public void Setter_IsChangeGuarded()
		{
			var table = new TableControl();
			int changes = 0;
			table.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TableControl.RightClickExtendsSelection)) changes++; };

			table.RightClickExtendsSelection = true;
			table.RightClickExtendsSelection = true;   // no-op
			table.RightClickExtendsSelection = false;

			Assert.Equal(2, changes);
		}
	}
}
