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
using Ctl = SharpConsoleUI.Builders.Controls;

using SharpConsoleUI.Extensions;
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

		// Builds the path line with a clickable drive chip + (Ctrl+D) hint.
		string BuildPathMarkup(string path)
		{
			var (seg, rest) = DrivePlaces.SplitDriveSegment(path);
			var restSafe = rest.Replace("[", "[[").Replace("]", "]]");
			// Truncate the remainder if very long, keeping the chip intact.
			if (restSafe.Length > 50)
				restSafe = "..." + restSafe.Substring(restSafe.Length - 47);
			return $"{DrivePlaces.BuildChipMarkup(seg)}[white]{restSafe}[/]";
		}

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

		// Breadcrumb path display with clickable drive chip
		var pathDisplay = Ctl.Markup()
			.AddLine(BuildPathMarkup(currentPath))
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
			.AddLine("[grey70]Enter: Open/Select  •  Backspace: Go Up  •  Ctrl+D: Places  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.WithName("FooterHint")
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
							if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
								continue;

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
				pathCtrl.Text = BuildPathMarkup(currentPath);
			}
		}

		// "Places" mode: the folder list is temporarily replaced with drives/mounts.
		bool inPlacesMode = false;

		const string BrowsingFooter = "[grey70]Enter: Open/Select  •  Backspace: Go Up  •  Ctrl+D: Places  •  Escape: Cancel[/]";

		void EnterPlacesMode()
		{
			inPlacesMode = true;
			folderList.ClearItems();
			var places = DrivePlaces.GetPlaces(currentPath);
			int currentIndex = 0;
			for (int i = 0; i < places.Count; i++)
			{
				var pl = places[i];
				var marker = pl.IsCurrent ? "[grey50]●[/] " : "  ";
				folderList.AddItem(new ListItem(
					$"{marker}{pl.Icon} {EscapeMarkup(pl.DisplayName)} [grey50]{EscapeMarkup(pl.Detail)}[/]")
				{
					Tag = pl.Path
				});
				if (pl.IsCurrent) currentIndex = i;
			}
			folderList.SelectedIndex = currentIndex;

			var footer = modal.FindControl<MarkupControl>("FooterHint");
			if (footer != null)
				footer.Text = "[grey70]Enter: Go to location  •  Esc: Back to browsing[/]";
		}

		void ExitPlacesMode()
		{
			inPlacesMode = false;
			PopulateFolderList(currentPath);
			var footer = modal.FindControl<MarkupControl>("FooterHint");
			if (footer != null)
				footer.Text = BrowsingFooter;
		}

		// Handle folder list activation (double-click or Enter on focused item)
		folderList.ItemActivated += (sender, item) =>
		{
			if (inPlacesMode)
			{
				if (item?.Tag is string placePath && Directory.Exists(placePath))
				{
					inPlacesMode = false;
					PopulateFolderList(placePath);
					var footer = modal.FindControl<MarkupControl>("FooterHint");
					if (footer != null)
						footer.Text = BrowsingFooter;
				}
				return;
			}

			if (item?.Tag is FolderEntry entry && Directory.Exists(entry.Path))
			{
				// Navigate into the selected folder
				PopulateFolderList(entry.Path);
			}
		};

		// Ctrl+D / Places-mode Esc handled before the focused list sees the key.
		modal.PreviewKeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.D &&
				(e.KeyInfo.Modifiers & ConsoleModifiers.Control) != 0)
			{
				if (!inPlacesMode) EnterPlacesMode();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Escape && inPlacesMode)
			{
				ExitPlacesMode();
				e.Handled = true; // do NOT let the dialog's Escape=cancel fire
			}
		};

		// Clicking the path line opens Places.
		pathDisplay.MouseClick += (sender, e) =>
		{
			if (!inPlacesMode) EnterPlacesMode();
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
			if (inPlacesMode)
			{
				// In Places mode only the list (Enter to activate) and the
				// PreviewKeyPressed Esc handler are active.
				return;
			}

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
		folderList.GetParentWindow()?.FocusManager.SetFocus(folderList, FocusReason.Programmatic);

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
