using ConsoleEx.Contents;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
	internal class UserInfoWindow
	{
		private Window _window;
		private MarkupContent? _ageInfo;
		private PromptContent? _agePrompt;
		private PromptContent? _namePrompt;
		private HorizontalGridContent? _bottomButtons;

		public Window Window => _window;

		public UserInfoWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Title = "User",
				Left = 22,
				Top = 6,
				Width = 40,
				Height = 10,
				IsResizable = true
			};
			consoleWindowSystem.AddWindow(_window);

			_ageInfo = new MarkupContent(new List<string>() { " " });

			_namePrompt = new PromptContent()
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]name[/]: ",
				DisableOnEnter = false
			};
			_namePrompt.OnInputChange += NamePrompt_InputChanged;

			_agePrompt = new PromptContent()
			{
				Prompt = "[yellow]Enter[/] [red]your[/] [blue]age[/] : ",
				DisableOnEnter = false,
				InputWidth = 10
			};
			_agePrompt.OnEnter += AgePrompt_Enter;

			_window.AddContent(new MarkupContent(new List<string>() { "User Info", " " }));
			_window.AddContent(_namePrompt);
			_window.AddContent(_agePrompt);
			_window.AddContent(new MarkupContent(new List<string>() { " " }));
			_window.AddContent(_ageInfo);
			_window.AddContent(new MarkupContent(new List<string>() { " " }));

			_window.AddContent(new RuleContent()
			{
				Color = Color.Yellow,
				Title = "[cyan]A[/][red]c[/][green]t[/][blue]i[/]o[white]n[/]s",
				TitleAlignment = Justify.Left,
				StickyPosition = StickyPosition.Bottom
			});

			_bottomButtons = new HorizontalGridContent()
			{
				StickyPosition = StickyPosition.Bottom
			};
			_window.AddContent(_bottomButtons);

			var maximizeButton = new ButtonContent()
			{
				Text = "[yellow]Maximize[/] window",
				StickyPosition = StickyPosition.Bottom,
				Margin = new Margin() { Left = 1 }
			};
			maximizeButton.OnClick += (sender) =>
			{
				_window.State = WindowState.Maximized;
			};

			ColumnContainer columnContainer = new ColumnContainer(_bottomButtons);
			columnContainer.AddContent(maximizeButton);
			_bottomButtons.AddColumn(columnContainer);

			var closeButton = new ButtonContent()
			{
				Text = "[red]Close[/] window",
				StickyPosition = StickyPosition.Bottom,
				Margin = new Margin() { Left = 1 }
			};
			closeButton.OnClick += (sender) =>
			{
				_window.Close();
			};

			columnContainer = new ColumnContainer(_bottomButtons);
			columnContainer.AddContent(closeButton);
			_bottomButtons.AddColumn(columnContainer);
		}

		public void WindowThread(Window window)
		{
		}

		public void NamePrompt_InputChanged(PromptContent prompt, string input)
		{
			_window.Title = $"User - {input}";
		}

		public void AgePrompt_Enter(PromptContent prompt, string input)
		{
			_ageInfo?.SetContent(new List<string>() { $"[bold]Your age is {input}[/]" });
		}
	}
}
