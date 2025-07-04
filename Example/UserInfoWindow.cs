﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Spectre.Console;
using SharpConsoleUI;

namespace SharpConsoleUI.Example
{
	internal class UserInfoWindow
	{
		private readonly Window _window;
		private MarkupControl _ageInfo;
		private PromptControl _agePrompt;
		private CheckboxControl _agreeTermsCheckbox;
		private HorizontalGridControl? _bottomButtons;
		private ConsoleWindowSystem _consoleWindowSystem;
		private DropdownControl? _countryDropdown;
		private MultilineEditControl _multilineEdit;
		private PromptControl _namePrompt;

		public UserInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = CreateWindow(consoleWindowSystem);
			_consoleWindowSystem = consoleWindowSystem;

			consoleWindowSystem.AddWindow(_window);

			_ageInfo = new MarkupControl(new List<string> { " " });

			_namePrompt = CreateNamePrompt();
			_agePrompt = CreateAgePrompt();

			// Create checkbox control
			_agreeTermsCheckbox = new CheckboxControl("I agree to terms and conditions", false)
			{
				Margin = new Margin(1, 0, 0, 0),
				CheckmarkColor = Color.Cyan3
			};
			_agreeTermsCheckbox.CheckedChanged += AgreeTerms_CheckedChanged;

			_multilineEdit = new MultilineEditControl()
			{
				ViewportHeight = 8,
				Margin = new Margin { Left = 1, Right = 1 }
			};

			AddWindowContents();
		}

		public Window Window => _window;

		public void AgePrompt_Enter(PromptControl prompt, string input)
		{
			_ageInfo?.SetContent(new List<string> { $"[bold]Your age is {input}[/]" });
		}

		public void NamePrompt_InputChanged(PromptControl prompt, string input)
		{
			_window.Title = $"User - {input}";
		}

		public void WindowThread(Window window)
		{
		}

		private void AddBottomButtons()
		{
			_bottomButtons = CreateBottomButtons();
			_window.AddControl(_bottomButtons);

			var maximizeButton = CreateButton("[yellow]Maximize[/] window", (sender, button) => _window.State = WindowState.Maximized);
			maximizeButton.Alignment = Alignment.Strecth;

			var closeButton = CreateButton("[red]Close[/] window", (sender, button) =>
			{
				// Create a modal dialog
				var modalWindow = new Window(_consoleWindowSystem, _window)
				{
					Title = "Confirm Action",
					Mode = WindowMode.Modal,
					Width = 40,
					Height = 10
				};

				// Add some content
				modalWindow.AddControl(new MarkupControl(new List<string>() { "Are you sure you want to proceed?" })
				{
					Alignment = Alignment.Center
				});

				var okButton = new ButtonControl()
				{
					Text = "ok"
				};
				okButton.Click += (sender, button) =>
				{
					_consoleWindowSystem.CloseWindow(modalWindow);
					_consoleWindowSystem.CloseWindow(_window);
				};

				modalWindow.AddControl(okButton);

				// Make the window visible
				_consoleWindowSystem.AddWindow(modalWindow);
				_consoleWindowSystem.SetActiveWindow(modalWindow);
			});

			AddButtonToBottomButtons(maximizeButton);
			AddButtonToBottomButtons(closeButton);

			SplitterControl splitter = new SplitterControl();
			_bottomButtons.AddSplitter(0, splitter);
		}

		private void AddButtonToBottomButtons(ButtonControl button)
		{
			if (_bottomButtons == null) return;

			var columnContainer = new ColumnContainer(_bottomButtons);
			columnContainer.AddContent(button);
			_bottomButtons.AddColumn(columnContainer);
		}

		private void AddWindowContents()
		{
			HorizontalGridControl horizontalGridControl = new HorizontalGridControl()
			{
				StickyPosition = StickyPosition.Top
			};
			_window.AddControl(horizontalGridControl);

			var columnContainer = new ColumnContainer(horizontalGridControl);
			columnContainer.AddContent(new MarkupControl(new List<string> { "[cyan]F[/]ile [cyan]E[/]dit [cyan]V[/]iew [cyan]H[/]elp" }));
			horizontalGridControl.AddColumn(columnContainer);

			columnContainer = new ColumnContainer(horizontalGridControl);
			_countryDropdown = new DropdownControl("Select a country:")
			{
				Margin = new Margin { Left = 1, Right = 1 }
			};
			_countryDropdown.AddItem("USA", "★", Color.Cyan1);
			_countryDropdown.AddItem("Canada", "♦", Color.Red);
			_countryDropdown.AddItem("UK", "♠", Color.Cyan1);
			_countryDropdown.AddItem("France", "♣", Color.Red);
			_countryDropdown.AddItem("Germany", "■", Color.Yellow);
			_countryDropdown.AddItem("Japan", "●", Color.Red);
			_countryDropdown.AddItem("Australia", "◆", Color.Green);
			_countryDropdown.SelectedIndex = 0;
			columnContainer.AddContent(_countryDropdown);
			horizontalGridControl.AddColumn(columnContainer);

			_window.AddControl(new RuleControl() { StickyPosition = StickyPosition.Top });

			_window.AddControl(_namePrompt);
			_window.AddControl(_agePrompt);
			_window.AddControl(new MarkupControl(new List<string> { " " }));

			_window.AddControl(_agreeTermsCheckbox);

			_window.AddControl(_ageInfo);
			_window.AddControl(new MarkupControl(new List<string> { " " }));

			_window.AddControl(new RuleControl
			{
				Title = "Comment",
				TitleAlignment = Justify.Left
			});

			_multilineEdit.Content = "dddddd";

			_window.AddControl(_multilineEdit);

			_window.AddControl(new RuleControl());

			// Create a Table renderable
			var table = new Table();

			// Add some columns
			table.AddColumn("Foo");
			table.AddColumn(new TableColumn("Bar").Centered());

			// Add some rows
			table.AddRow("Baz", "[green]Qux[/]");
			table.AddRow(new Markup("[blue]Corgi[/]"), new Panel("Waldo"));

			// Create a control for the table
			var tableControl = new SpectreRenderableControl(table);
			tableControl.Alignment = Alignment.Center;
			tableControl.Margin = new Margin(1, 1, 1, 1);

			// Add to window
			_window.AddControl(tableControl);

			_window.AddControl(new RuleControl
			{
				Color = Color.Yellow,
				Title = "[cyan]A[/][red]c[/][green]t[/][blue]i[/]o[white]n[/]s",
				TitleAlignment = Justify.Left,
				StickyPosition = StickyPosition.Bottom
			});

			AddBottomButtons();
		}

		private void AgreeTerms_CheckedChanged(object? sender, bool isChecked)
		{
			// Update UI based on checkbox state
			if (isChecked)
			{
				_ageInfo?.SetContent(new List<string> { "[green]Terms accepted[/]" });
			}
			else
			{
				_ageInfo?.SetContent(new List<string> { "[red]Please accept the terms[/]" });
			}
		}

		private PromptControl CreateAgePrompt()
		{
			var agePrompt = new PromptControl
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]age[/] : ",
				UnfocusOnEnter = false,
				InputWidth = 10
			};
			agePrompt.OnEnter += AgePrompt_Enter;
			return agePrompt;
		}

		private HorizontalGridControl CreateBottomButtons()
		{
			return new HorizontalGridControl
			{
				StickyPosition = StickyPosition.Bottom
			};
		}

		private ButtonControl CreateButton(string text, EventHandler<ButtonControl> onClick)
		{
			var button = new ButtonControl
			{
				Text = text,
				Margin = new Margin { Left = 1 }
			};
			button.Click += onClick;
			return button;
		}

		private PromptControl CreateNamePrompt()
		{
			var namePrompt = new PromptControl
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]name[/]: ",
				UnfocusOnEnter = false
			};
			namePrompt.OnInputChange += NamePrompt_InputChanged;
			return namePrompt;
		}

		private Window CreateWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			return new Window(consoleWindowSystem, WindowThread)
			{
				Title = "User",
				Left = 12,
				Top = 12,
				Width = 60,
				Height = 10,
				IsResizable = true
			};
		}
	}
}