﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Controls;
using Spectre.Console;

namespace ConsoleEx.Example
{
	internal class UserInfoWindow
	{
		private readonly Window _window;
		private MarkupControl _ageInfo;
		private PromptControl _agePrompt;
		private HorizontalGridControl? _bottomButtons;
		private MultilineEditControl _multilineEdit;
		private PromptControl _namePrompt;

		public UserInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = CreateWindow(consoleWindowSystem);

			consoleWindowSystem.AddWindow(_window);

			_ageInfo = new MarkupControl(new List<string> { " " });

			_namePrompt = CreateNamePrompt();
			_agePrompt = CreateAgePrompt();

			_multilineEdit = new MultilineEditControl()
			{
				ViewportHeight = 3,
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
			_window.AddContent(_bottomButtons);

			var maximizeButton = CreateButton("[yellow]Maximize[/] window", (sender) => _window.State = WindowState.Maximized);
			var closeButton = CreateButton("[red]Close[/] window", (sender) => _window.Close());

			AddButtonToBottomButtons(maximizeButton);
			AddButtonToBottomButtons(closeButton);
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
			_window.AddContent(new MarkupControl(new List<string> { "User Info", " " }));
			_window.AddContent(_namePrompt);
			_window.AddContent(_agePrompt);
			_window.AddContent(new MarkupControl(new List<string> { " " }));
			_window.AddContent(_ageInfo);
			_window.AddContent(new MarkupControl(new List<string> { " " }));

			_window.AddContent(new RuleControl
			{
				Title = "MultilineEdit control",
				TitleAlignment = Justify.Left
			});

			_window.AddContent(_multilineEdit);

			_window.AddContent(new RuleControl());

			_window.AddContent(new RuleControl
			{
				Color = Color.Yellow,
				Title = "[cyan]A[/][red]c[/][green]t[/][blue]i[/]o[white]n[/]s",
				TitleAlignment = Justify.Left,
				StickyPosition = StickyPosition.Bottom
			});

			AddBottomButtons();
		}

		private PromptControl CreateAgePrompt()
		{
			var agePrompt = new PromptControl
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]age[/] : ",
				DisableOnEnter = false,
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

		private ButtonControl CreateButton(string text, Action<object> onClick)
		{
			var button = new ButtonControl
			{
				Text = text,
				Margin = new Margin { Left = 1 }
			};
			button.OnClick += onClick;
			return button;
		}

		private PromptControl CreateNamePrompt()
		{
			var namePrompt = new PromptControl
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]name[/]: ",
				DisableOnEnter = false
			};
			namePrompt.OnInputChange += NamePrompt_InputChanged;
			return namePrompt;
		}

		private Window CreateWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			return new Window(consoleWindowSystem, WindowThread)
			{
				Title = "User",
				Left = 22,
				Top = 6,
				Width = 40,
				Height = 16,
				IsResizable = true
			};
		}
	}
}