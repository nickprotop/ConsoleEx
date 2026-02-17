// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Ctl = SharpConsoleUI.Builders.Controls;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Dialogs;

/// <summary>
/// Dedicated folder selection dialog with proper UI/UX for directory browsing.
/// </summary>
public static class FolderPickerDialog
{
	/// <summary>
	/// Shows a folder picker dialog for selecting a directory.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="title">Optional custom title for the dialog.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the selected folder path, or null if cancelled.</returns>
	public static Task<string?> ShowAsync(
		ConsoleWindowSystem windowSystem,
		string? startPath = null,
		string? title = null,
		Window? parentWindow = null)
	{
		var tcs = new TaskCompletionSource<string?>();
		string? selectedPath = null;
		string currentPath = startPath ?? Environment.CurrentDirectory;

		// Ensure path exists
		if (!Directory.Exists(currentPath))
			currentPath = Environment.CurrentDirectory;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle(title ?? "Select Folder")
			.Centered()
			.WithSize(70, 22)
			.AsModal()
			.Resizable(true)
			.Minimizable(false)
			.Maximizable(true)
			.Movable(true);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Breadcrumb path display with individual path segments
		var pathDisplay = Ctl.Markup()
			.AddLine($"[grey50]Current:[/] [white]{EscapeMarkup(currentPath)}[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.WithName("PathDisplay")
			.Build();
		modal.AddControl(pathDisplay);

		// Separator
		modal.AddControl(Ctl.RuleBuilder().Build());

		// Folder list (only directories, no files)
		var folderList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.WithName("FolderList")
			.Build();

		modal.AddControl(folderList);

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder().StickyBottom().Build());

		// Button panel with "Select This Folder" and "Cancel"
		var buttonPanel = Ctl.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.StickyBottom()
			.WithMargin(1, 0, 1, 0);

		var selectButton = Ctl.Button("[green]✓ Select This Folder[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithName("SelectButton")
			.Build();

		var cancelButton = Ctl.Button("[red]✗ Cancel[/]")
			.WithAlignment(HorizontalAlignment.Right)
			.WithName("CancelButton")
			.Build();

		buttonPanel
			.Column(col => col.Flex(1).Add(selectButton))
			.Column(col => col.Width(2).Add(Ctl.Markup().AddLine("").Build())) // Spacer
			.Column(col => col.Flex(1).Add(cancelButton));

		modal.AddControl(buttonPanel.Build());

		// Footer separator
		modal.AddControl(Ctl.RuleBuilder().StickyBottom().Build());

		// Footer with keyboard shortcuts
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter: Open/Select  •  Backspace: Go Up  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Populate folder list helper - FOLDERS ONLY, NO FILES
		void PopulateFolderList(string path)
		{
			folderList.ClearItems();

			try
			{
				var dirInfo = new DirectoryInfo(path);

				// Add parent directory entry if not root
				if (dirInfo.Parent != null)
				{
					folderList.AddItem(new ListItem("[yellow]📁[/] [grey70]..[/] [grey50](Parent Directory)[/]")
					{
						Tag = new FolderEntry { Path = dirInfo.Parent.FullName, IsParent = true }
					});
				}

				// Add subdirectories - ONLY DIRECTORIES
				var directories = dirInfo.GetDirectories().OrderBy(d => d.Name).ToList();

				if (directories.Count == 0 && dirInfo.Parent == null)
				{
					// Root directory with no subdirectories
					folderList.AddItem(new ListItem("[grey50](No subdirectories)[/]")
					{
						Tag = null
					});
				}
				else
				{
					foreach (var dir in directories)
					{
						try
						{
							// Count subdirectories for visual feedback
							int subDirCount = 0;
							try
							{
								subDirCount = dir.GetDirectories().Length;
							}
							catch { /* Ignore permission errors */ }

							var subDirInfo = subDirCount > 0 ? $" [grey50]({subDirCount} folders)[/]" : "";

							folderList.AddItem(new ListItem($"[yellow]📁[/] {EscapeMarkup(dir.Name)}{subDirInfo}")
							{
								Tag = new FolderEntry { Path = dir.FullName, IsParent = false }
							});
						}
						catch (UnauthorizedAccessException)
						{
							// Skip directories we can't access
						}
					}
				}
			}
			catch (Exception ex)
			{
				folderList.AddItem(new ListItem($"[red]Error: {EscapeMarkup(ex.Message)}[/]")
				{
					Tag = null
				});
			}

			folderList.SelectedIndex = 0;
			currentPath = path;

			// Update path display
			var pathCtrl = modal.FindControl<MarkupControl>("PathDisplay");
			if (pathCtrl != null)
			{
				// Truncate path if too long
				var displayPath = currentPath;
				if (displayPath.Length > 60)
				{
					displayPath = "..." + displayPath.Substring(displayPath.Length - 57);
				}
				pathCtrl.Text = $"[grey50]Current:[/] [white]{EscapeMarkup(displayPath)}[/]";
			}
		}

		// Handle folder list activation (double-click or Enter on focused item)
		folderList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is FolderEntry entry && Directory.Exists(entry.Path))
			{
				// Navigate into the selected folder
				PopulateFolderList(entry.Path);
			}
		};

		// Handle "Select This Folder" button
		selectButton.Click += (sender, e) =>
		{
			selectedPath = currentPath;
			modal.Close();
		};

		// Handle "Cancel" button
		cancelButton.Click += (sender, e) =>
		{
			selectedPath = null;
			modal.Close();
		};

		// Handle keyboard navigation
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				selectedPath = null;
				modal.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Backspace)
			{
				// Go up one directory
				var parent = Directory.GetParent(currentPath);
				if (parent != null)
				{
					PopulateFolderList(parent.FullName);
				}
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Enter && folderList.HasFocus)
			{
				// Enter on list: navigate into selected folder OR select if it's the parent
				if (folderList.SelectedItem?.Tag is FolderEntry entry && Directory.Exists(entry.Path))
				{
					PopulateFolderList(entry.Path);
					e.Handled = true;
				}
			}
			else if (e.KeyInfo.Key == ConsoleKey.Spacebar)
			{
				// Spacebar: quick "Select This Folder"
				selectedPath = currentPath;
				modal.Close();
				e.Handled = true;
			}
		};

		// Initial population
		PopulateFolderList(currentPath);

		// Complete task when modal closes
		modal.OnClosed += (s, e) => tcs.TrySetResult(selectedPath);

		// Add modal and activate
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);
		folderList.SetFocus(true, FocusReason.Programmatic);

		return tcs.Task;
	}

	private static string EscapeMarkup(string text)
	{
		return text.Replace("[", "[[").Replace("]", "]]");
	}

	/// <summary>
	/// Internal data structure for folder list entries.
	/// </summary>
	private class FolderEntry
	{
		public required string Path { get; init; }
		public bool IsParent { get; init; }
	}
}
