using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class DialogsWindow
{
	#region Constants

	private const int WindowWidth = 65;
	private const int WindowHeight = 30;
	private const int ButtonWidth = 25;
	private const int ButtonLeftMargin = 2;
	private const int SectionTopMargin = 1;
	private const int SectionLabelLeftMargin = 1;

	#endregion

	public static Window Create(ConsoleWindowSystem ws)
	{
		var resultDisplay = Controls.Markup("[dim]Dialog results will appear here[/]")
			.WithMargin(1, 1, 1, 0)
			.Build();

		#region File Dialog Buttons

		var fileSectionLabel = Controls.Label("[bold]File Dialogs[/]");
		fileSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

		var openFileBtn = Controls.Button("Open File...")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					var result = await FileDialogs.ShowFilePickerAsync(ws);
					resultDisplay.SetContent(new List<string>
					{
						result != null
							? $"[green]Selected file:[/] {result}"
							: "[dim]Cancelled[/]"
					});
				});
			})
			.Build();
		openFileBtn.Margin = new Margin { Left = ButtonLeftMargin };

		var openFolderBtn = Controls.Button("Open Folder...")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					var result = await FileDialogs.ShowFolderPickerAsync(ws);
					resultDisplay.SetContent(new List<string>
					{
						result != null
							? $"[green]Selected folder:[/] {result}"
							: "[dim]Cancelled[/]"
					});
				});
			})
			.Build();
		openFolderBtn.Margin = new Margin { Left = ButtonLeftMargin };

		var saveFileBtn = Controls.Button("Save File...")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					var result = await FileDialogs.ShowSaveFileAsync(ws);
					resultDisplay.SetContent(new List<string>
					{
						result != null
							? $"[green]Save path:[/] {result}"
							: "[dim]Cancelled[/]"
					});
				});
			})
			.Build();
		saveFileBtn.Margin = new Margin { Left = ButtonLeftMargin };

		#endregion

		#region System Dialog Buttons

		var systemSectionLabel = Controls.Label("[bold]System Dialogs[/]");
		systemSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

		var aboutBtn = Controls.Button("About")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => AboutDialog.Show(ws))
			.Build();
		aboutBtn.Margin = new Margin { Left = ButtonLeftMargin };

		var settingsBtn = Controls.Button("Settings")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => SettingsDialog.Show(ws))
			.Build();
		settingsBtn.Margin = new Margin { Left = ButtonLeftMargin };

		var performanceBtn = Controls.Button("Performance")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => PerformanceDialog.Show(ws))
			.Build();
		performanceBtn.Margin = new Margin { Left = ButtonLeftMargin };

		var themeBtn = Controls.Button("Theme Selector")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) => ThemeSelectorDialog.Show(ws))
			.Build();
		themeBtn.Margin = new Margin { Left = ButtonLeftMargin };

		#endregion

		#region Message Dialog Buttons

		var messageSectionLabel = Controls.Label("[bold]Message Dialogs[/]");
		messageSectionLabel.Margin = new Margin { Left = SectionLabelLeftMargin, Top = SectionTopMargin };

		// MessageAsync — a one-button info dialog whose body renders MARKUP (the framework advertises
		// markup everywhere; dialogs now honor it).
		var messageBtn = Controls.Button("Message (markup)")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					await Dialogs.MessageAsync(ws, "Saved",
						"Your changes were [green]saved successfully[/] to [bold]config.json[/].");
					resultDisplay.SetContent(new List<string> { "[green]Message acknowledged[/]" });
				});
			})
			.Build();
		messageBtn.Margin = new Margin { Left = ButtonLeftMargin };

		// ShowAsync — arbitrary buttons; returns the clicked button's FlowVerdict.
		var showBtn = Controls.Button("Custom Buttons")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					var verdict = await Dialogs.ShowAsync(ws, "Unsaved changes",
						"You have [yellow]unsaved changes[/]. What would you like to do?",
						new[]
						{
							new SharpConsoleUI.Flows.FlowButton("Save", SharpConsoleUI.Flows.FlowVerdict.Yes),
							new SharpConsoleUI.Flows.FlowButton("Discard", SharpConsoleUI.Flows.FlowVerdict.No),
							new SharpConsoleUI.Flows.FlowButton("Cancel", SharpConsoleUI.Flows.FlowVerdict.Cancel),
						});
					resultDisplay.SetContent(new List<string> { $"[cyan]You chose:[/] {verdict}" });
				});
			})
			.Build();
		showBtn.Margin = new Margin { Left = ButtonLeftMargin };

		// ConfirmAsync with a standardized preset (Yes/No).
		var confirmBtn = Controls.Button("Confirm (Yes/No)")
			.WithWidth(ButtonWidth)
			.OnClick((_, _) =>
			{
				_ = Task.Run(async () =>
				{
					var yes = await Dialogs.ConfirmAsync(ws, "Delete item",
						"Delete [red]report-2026.pdf[/]? This [bold]cannot be undone[/].",
						SharpConsoleUI.Flows.FlowButtons.YesNo,
						severity: SharpConsoleUI.Core.NotificationSeverityEnum.Warning);
					resultDisplay.SetContent(new List<string>
					{
						yes ? "[red]Deleted[/]" : "[dim]Kept[/]"
					});
				});
			})
			.Build();
		confirmBtn.Margin = new Margin { Left = ButtonLeftMargin };

		#endregion

		#region Window Assembly

		Window? window = null;
		window = new WindowBuilder(ws)
			.WithTitle("Built-in Dialogs")
			.WithSize(WindowWidth, WindowHeight)
			.Centered()
			.OnKeyPressed((sender, e) =>
			{
				if (e.KeyInfo.Key == ConsoleKey.Escape)
				{
					ws.CloseWindow(window!);
					e.Handled = true;
				}
			})
			.AddControl(Controls.Markup("[bold underline]Built-in Dialog Showcase[/]")
				.Centered()
				.Build())
			.AddControl(fileSectionLabel)
			.AddControl(openFileBtn)
			.AddControl(openFolderBtn)
			.AddControl(saveFileBtn)
			.AddControl(systemSectionLabel)
			.AddControl(aboutBtn)
			.AddControl(settingsBtn)
			.AddControl(performanceBtn)
			.AddControl(themeBtn)
			.AddControl(messageSectionLabel)
			.AddControl(messageBtn)
			.AddControl(showBtn)
			.AddControl(confirmBtn)
			.AddControl(resultDisplay)
			.BuildAndShow();

		return window;

		#endregion
	}
}
