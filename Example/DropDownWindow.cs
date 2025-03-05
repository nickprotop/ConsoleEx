// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using ConsoleEx.Controls;

namespace ConsoleEx.Example
{
	public class DropDownWindow
	{
		private DropdownControl _countryDropdown;
		private ConsoleWindowSystem _system;
		private Window _window;

		public DropDownWindow(ConsoleWindowSystem system)
		{
			// Create window
			_window = new Window(system) { Title = "Country Selection" };
			_window.Width = 50;
			_window.Height = 20;
			_window.Top = 2;
			_window.Left = 2;

			_system = system;

			// Create title
			_window.AddContent(new MarkupControl(new List<string> { "[bold]Country Selection Form[/]" })
			{
				Alignment = Alignment.Center,
				StickyPosition = StickyPosition.Top
			});
			_window.AddContent(new RuleControl() { StickyPosition = StickyPosition.Top });

			// Create dropdown
			_countryDropdown = new DropdownControl("Select a country:");
			_countryDropdown.AddItem("USA", "★", Color.Cyan1);
			_countryDropdown.AddItem("Canada", "♦", Color.Red);
			_countryDropdown.AddItem("UK", "♠", Color.Cyan1);
			_countryDropdown.AddItem("France", "♣", Color.Red);
			_countryDropdown.AddItem("Germany", "■", Color.Yellow);
			_countryDropdown.AddItem("Japan", "●", Color.Red);
			_countryDropdown.AddItem("Australia", "◆", Color.Green);
			_countryDropdown.SelectedIndex = 0;

			// Add spacing
			_window.AddContent(new MarkupControl(new List<string> { " " }));

			// Add dropdown
			_window.AddContent(_countryDropdown);

			// Add some more spacing
			_window.AddContent(new MarkupControl(new List<string> { " " }));

			// Add action buttons
			var buttonsGrid = new HorizontalGridControl { Alignment = Alignment.Center, StickyPosition = StickyPosition.Bottom };

			var okButton = CreateButton("OK", OnOkClicked);
			var cancelButton = CreateButton("Cancel", OnCancelClicked);

			var okColumn = new ColumnContainer(buttonsGrid);
			okColumn.AddContent(okButton);
			buttonsGrid.AddColumn(okColumn);

			var cancelColumn = new ColumnContainer(buttonsGrid);
			cancelColumn.AddContent(cancelButton);
			buttonsGrid.AddColumn(cancelColumn);

			_window.AddContent(buttonsGrid);

			// Handle selection change
			_countryDropdown.SelectedItemChanged += CountryDropdown_SelectedItemChanged;
		}

		public Window GetWindow() => _window;

		private void CountryDropdown_SelectedItemChanged(object? sender, DropdownItem? item)
		{
			if (item != null)
			{
				// Do something with the selected item
				_window.Title = $"Selected: {item.Text}";
			}
		}

		private ButtonControl CreateButton(string text, Action<object> onClick)
		{
			var button = new ButtonControl { Text = text };
			button.OnClick += onClick;
			return button;
		}

		private void OnCancelClicked(object obj)
		{
			_window.GetConsoleWindowSystem?.CloseWindow(_window);
		}

		private void OnOkClicked(object obj)
		{
			// Process the selected country
			var country = _countryDropdown.SelectedValue;
			_window.GetConsoleWindowSystem?.CloseWindow(_window);
		}
	}
}