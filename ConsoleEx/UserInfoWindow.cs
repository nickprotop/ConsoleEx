// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Contents;
using Spectre.Console;

namespace ConsoleEx
{
	internal class UserInfoWindow
	{
		private readonly Window _window;
		private MarkupContent _ageInfo;
		private PromptContent _agePrompt;
		private HorizontalGridContent? _bottomButtons;
		private PromptContent _namePrompt;

		public UserInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = CreateWindow(consoleWindowSystem);
			consoleWindowSystem.AddWindow(_window);

			_ageInfo = new MarkupContent(new List<string> { " " });

			_namePrompt = CreateNamePrompt();
			_agePrompt = CreateAgePrompt();

			AddWindowContents();
		}

		public Window Window => _window;

		public void AgePrompt_Enter(PromptContent prompt, string input)
		{
			_ageInfo?.SetContent(new List<string> { $"[bold]Your age is {input}[/]" });
		}

		public void NamePrompt_InputChanged(PromptContent prompt, string input)
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

		private void AddButtonToBottomButtons(ButtonContent button)
		{
			if (_bottomButtons == null) return;

			var columnContainer = new ColumnContainer(_bottomButtons);
			columnContainer.AddContent(button);
			_bottomButtons.AddColumn(columnContainer);
		}

		private void AddWindowContents()
		{
			_window.AddContent(new MarkupContent(new List<string> { "User Info", " " }));
			_window.AddContent(_namePrompt);
			_window.AddContent(_agePrompt);
			_window.AddContent(new MarkupContent(new List<string> { " " }));
			_window.AddContent(_ageInfo);
			_window.AddContent(new MarkupContent(new List<string> { " " }));

			_window.AddContent(new RuleContent
			{
				Color = Color.Yellow,
				Title = "[cyan]A[/][red]c[/][green]t[/][blue]i[/]o[white]n[/]s",
				TitleAlignment = Justify.Left,
				StickyPosition = StickyPosition.Bottom
			});

			AddBottomButtons();
		}

		private PromptContent CreateAgePrompt()
		{
			var agePrompt = new PromptContent
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]age[/] : ",
				DisableOnEnter = false,
				InputWidth = 10
			};
			agePrompt.OnEnter += AgePrompt_Enter;
			return agePrompt;
		}

		private HorizontalGridContent CreateBottomButtons()
		{
			return new HorizontalGridContent
			{
				StickyPosition = StickyPosition.Bottom
			};
		}

		private ButtonContent CreateButton(string text, Action<object> onClick)
		{
			var button = new ButtonContent
			{
				Text = text,
				Margin = new Margin { Left = 1 }
			};
			button.OnClick += onClick;
			return button;
		}

		private PromptContent CreateNamePrompt()
		{
			var namePrompt = new PromptContent
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
				Height = 10,
				IsResizable = true
			};
		}
	}
}