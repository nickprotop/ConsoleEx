using SharpConsoleUI.Controls;
using SharpConsoleUI.Services.NotificationsService;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpConsoleUI;

namespace SharpConsoleUI.Example
{
	public class ListViewWindow
	{
		private ListControl _listControl;
		private MarkupControl _selectionInfo;
		private ConsoleWindowSystem _system;
		private Window _window;

		public ListViewWindow(ConsoleWindowSystem system)
		{
			_system = system;
			// Create window
			_window = new Window(system)
			{
				Title = "List Control Example",
				Width = 60,
				Height = 20,
				Top = 4,
				Left = 4
			};

			// Create title
			_window.AddControl(new MarkupControl(new List<string> { "[bold]Example List Control[/]" })
			{
				Alignment = Alignment.Center,
				StickyPosition = StickyPosition.Top
			});

			_window.AddControl(new RuleControl() { StickyPosition = StickyPosition.Top });

			// Create a selection info display
			_selectionInfo = new MarkupControl(new List<string> { "No item selected" })
			{
				Alignment = Alignment.Left
			};
			_window.AddControl(_selectionInfo);

			// Create spacer
			_window.AddControl(new MarkupControl(new List<string> { " " }));

			// Create list control with some items
			_listControl = new ListControl("Available Items")
			{
				// Set properties
				Width = 50,
				Alignment = Alignment.Center,
				MaxVisibleItems = 8,  // Show up to 8 items at once
				AutoAdjustWidth = true
			};

			// Add items to the list
			_listControl.AddItem(new ListItem("Item 1 - Single Line", "●", Color.Green));
			_listControl.AddItem(new ListItem("Item 2 - Multi-line\nThis is the second line", "■", Color.Yellow));
			_listControl.AddItem(new ListItem("Item 3 - With Blue Icon", "★", Color.Blue));
			_listControl.AddItem("Item 4 - No Icon");
			_listControl.AddItem(new ListItem("Item 5 - With Red Icon", "♦", Color.Red));
			_listControl.AddItem("Item 6 - Plain Text");
			_listControl.AddItem(new ListItem("Item 7 - With Magenta Icon", "♥", Color.Magenta1));
			_listControl.AddItem(new ListItem("Item 8 - Multi-line\nSecond line here", "◆", Color.Cyan1));
			_listControl.AddItem("Item 9 - Scrolling Required");
			_listControl.AddItem("Item 10 - More Scrolling");

			// Handle selection changes
			_listControl.SelectedIndexChanged += ListControl_SelectedIndexChanged;

			// Select the first item by default
			_listControl.SelectedIndex = 0;

			// Add the list to the window
			_window.AddControl(_listControl);

			// Add action buttons at the bottom
			var buttonsGrid = new HorizontalGridControl
			{
				Alignment = Alignment.Center,
				StickyPosition = StickyPosition.Bottom
			};

			var selectButton = CreateButton("Select", OnSelectClicked);
			var closeButton = CreateButton("Close", OnCloseClicked);

			var selectColumn = new ColumnContainer(buttonsGrid);
			selectColumn.AddContent(selectButton);
			buttonsGrid.AddColumn(selectColumn);

			var closeColumn = new ColumnContainer(buttonsGrid);
			closeColumn.AddContent(closeButton);
			buttonsGrid.AddColumn(closeColumn);

			_window.AddControl(buttonsGrid);
		}

		// Get the window instance
		public Window GetWindow() => _window;

		private ButtonControl CreateButton(string text, Action<object> onClick)
		{
			var button = new ButtonControl { Text = text };
			button.OnClick += onClick;
			return button;
		}

		// Event handler for selection changes
		private void ListControl_SelectedIndexChanged(object? sender, int selectedIndex)
		{
			if (selectedIndex >= 0)
			{
				var item = _listControl.SelectedItem;
				_selectionInfo.SetContent(new List<string> { $"Selected: [green]{item?.Text.Split('\n')[0]}[/] (Index: {selectedIndex})" });
			}
			else
			{
				_selectionInfo.SetContent(new List<string> { "No item selected" });
			}
		}

		private void OnCloseClicked(object obj)
		{
			_window.Close();
		}

		private void OnSelectClicked(object obj)
		{
			var selectedItem = _listControl.SelectedItem;
			if (selectedItem != null)
			{
				// Process the selected item
				Notifications.ShowNotification(
					_system,
					"Selection",
					$"You selected: {selectedItem.Text.Split('\n')[0]}",
					NotificationSeverity.Info);
			}
		}
	}
}