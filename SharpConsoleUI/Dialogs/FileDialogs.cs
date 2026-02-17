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
/// Provides file and folder picker dialogs.
/// </summary>
public static class FileDialogs
{
	/// <summary>
	/// Shows a folder picker dialog for selecting a directory.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the selected folder path, or null if cancelled.</returns>
	public static Task<string?> ShowFolderPickerAsync(ConsoleWindowSystem windowSystem, string? startPath = null, Window? parentWindow = null)
	{
		// Delegate to dedicated folder picker with proper UI/UX
		return FolderPickerDialog.ShowAsync(windowSystem, startPath, "Select Folder", parentWindow);
	}

	/// <summary>
	/// Shows a file picker dialog for selecting a file.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="filter">Optional file filter (e.g., "*.txt", "*.cs;*.txt"). Null shows all files.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the selected file path, or null if cancelled.</returns>
	public static Task<string?> ShowFilePickerAsync(ConsoleWindowSystem windowSystem, string? startPath = null, string? filter = null, Window? parentWindow = null)
	{
		return ShowFilePickerInternalAsync(windowSystem, "Select File", startPath, filter, foldersOnly: false, parentWindow);
	}

	/// <summary>
	/// Shows a save file dialog for specifying a file to save.
	/// </summary>
	/// <param name="windowSystem">The window system to show the dialog in.</param>
	/// <param name="startPath">The initial directory path to display. Defaults to current directory.</param>
	/// <param name="filter">Optional file filter (e.g., "*.txt", "*.cs;*.txt"). Null shows all files.</param>
	/// <param name="defaultFileName">Default filename to pre-populate in the input field.</param>
	/// <param name="parentWindow">Optional parent window. If specified, the dialog will be modal to this window only.</param>
	/// <returns>A task that completes with the specified file path, or null if cancelled.</returns>
	public static Task<string?> ShowSaveFileAsync(ConsoleWindowSystem windowSystem, string? startPath = null, string? filter = null, string? defaultFileName = null, Window? parentWindow = null)
	{
		return ShowSaveFileInternalAsync(windowSystem, "Save File", startPath, filter, defaultFileName, parentWindow);
	}

	private static Task<string?> ShowFilePickerInternalAsync(ConsoleWindowSystem windowSystem, string title, string? startPath, string? filter, bool foldersOnly, Window? parentWindow = null)
	{
		var tcs = new TaskCompletionSource<string?>();
		string? selectedPath = null;
		string currentPath = startPath ?? Environment.CurrentDirectory;

		// Ensure path exists
		if (!Directory.Exists(currentPath))
			currentPath = Environment.CurrentDirectory;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle(title)
			.Centered()
			.WithSize(80, 24)
			.AsModal()
			.Resizable(true)
			.Minimizable(false)
			.Maximizable(true)
			.Movable(true);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Current path display
		var pathDisplay = Ctl.Markup()
			.AddLine($"[grey50]Path:[/] [white]{EscapeMarkup(currentPath)}[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.WithName("PathDisplay")
			.Build();
		modal.AddControl(pathDisplay);

		// Separator
		modal.AddControl(Ctl.RuleBuilder()
						.Build());

		// Create folder list
		var folderList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.WithName("FolderList")
			.Build();

		// File list (only for file picker mode)
		ListControl? fileList = null;
		if (!foldersOnly)
		{
			fileList = Ctl.List()
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.WithDoubleClickActivation(true)
				.WithName("FileList")
				.Build();
		}

		// Populate folder list helper
		void PopulateFolderList(string path)
		{
			folderList.ClearItems();

			try
			{
				var dirInfo = new DirectoryInfo(path);

				// Add parent directory entry if not root
				if (dirInfo.Parent != null)
				{
					folderList.AddItem(new ListItem("[yellow]📁[/] [grey70]..[/]") { Tag = dirInfo.Parent.FullName });
				}

				// Add subdirectories
				foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
				{
					try
					{
						folderList.AddItem(new ListItem($"[yellow]📁[/] {EscapeMarkup(dir.Name)}") { Tag = dir.FullName });
					}
					catch (UnauthorizedAccessException)
					{
						// Skip directories we can't access due to permissions
						// Errors are expected when browsing system directories
					}
				}

				// For folder picker, also show files dimmed (for context)
				if (foldersOnly)
				{
					foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
					{
						folderList.AddItem(new ListItem($"[grey50]📄 {EscapeMarkup(file.Name)}[/]") { Tag = null });
					}
				}
			}
			catch (Exception ex)
			{
				folderList.AddItem(new ListItem($"[red]Error: {EscapeMarkup(ex.Message)}[/]") { Tag = null });
			}

			folderList.SelectedIndex = 0;
			currentPath = path;

			// Update path display
			var pathCtrl = modal.FindControl<MarkupControl>("PathDisplay");
			if (pathCtrl != null)
			{
				pathCtrl.Text = $"[grey50]Path:[/] [white]{EscapeMarkup(currentPath)}[/]";
			}
		}

		// Populate file list helper (for file picker mode)
		void PopulateFileList(string path)
		{
			if (fileList == null) return;
			fileList.ClearItems();

			try
			{
				var dirInfo = new DirectoryInfo(path);
				var files = dirInfo.GetFiles();

				// Apply filter if specified
				if (!string.IsNullOrEmpty(filter))
				{
					var patterns = filter.Split(';', StringSplitOptions.RemoveEmptyEntries);
					files = files.Where(f => patterns.Any(p =>
					{
						var pattern = p.Trim();
						// Handle wildcard patterns: *.txt, *.cs, *.*, etc.
						if (pattern == "*" || pattern == "*.*")
							return true;

						if (pattern.StartsWith("*."))
						{
							var extension = pattern.Substring(1); // Remove leading *
							return f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase);
						}

						// Fallback: exact filename match
						return f.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
					})).ToArray();
				}

				foreach (var file in files.OrderBy(f => f.Name))
				{
					var icon = GetFileIcon(file.Extension);
					var size = FormatFileSize(file.Length);
					fileList.AddItem(new ListItem($"{icon} {EscapeMarkup(file.Name)} [grey50]({size})[/]") { Tag = file.FullName });
				}
			}
			catch (Exception ex)
			{
				fileList.AddItem(new ListItem($"[red]Error: {EscapeMarkup(ex.Message)}[/]") { Tag = null });
			}

			if (fileList.Items.Count > 0)
				fileList.SelectedIndex = 0;
		}

		// Create layout
		if (foldersOnly)
		{
			// Single list for folder picker
			modal.AddControl(folderList);
		}
		else
		{
			// Two-column layout for file picker
			var grid = Ctl.HorizontalGrid()
				.Column(col => col.Flex(1).Add(folderList))
				.WithSplitterAfter(0)
				.Column(col => col.Flex(1).Add(fileList!))
				.WithAlignment(HorizontalAlignment.Stretch)
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Build();
			modal.AddControl(grid);
		}

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
						.StickyBottom()
			.Build());

		// Footer with instructions
		var instructions = foldersOnly
			? "[grey70]Enter: Select Folder  •  Backspace: Go Up  •  Escape: Cancel[/]"
			: "[grey70]Enter: Select  •  Backspace: Go Up  •  Escape: Cancel[/]";
		modal.AddControl(Ctl.Markup()
			.AddLine(instructions)
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Initial population
		PopulateFolderList(currentPath);
		if (!foldersOnly)
			PopulateFileList(currentPath);

		// Handle folder list activation (navigate into folder)
		folderList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string folderPath && Directory.Exists(folderPath))
			{
				PopulateFolderList(folderPath);
				if (!foldersOnly)
					PopulateFileList(folderPath);
			}
			else if (foldersOnly && item?.Tag is string selectedFolder)
			{
				// In folder-only mode, selecting a folder path means selecting it
				selectedPath = selectedFolder;
				modal.Close();
			}
		};

		// Handle file list activation (select file)
		if (fileList != null)
		{
			fileList.ItemActivated += (sender, item) =>
			{
				if (item?.Tag is string filePath)
				{
					selectedPath = filePath;
					modal.Close();
				}
			};
		}

		// Handle keyboard navigation
		modal.KeyPressed += (sender, e) =>
		{
			if (e.KeyInfo.Key == ConsoleKey.Escape)
			{
				selectedPath = null;
				modal.Close();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Enter && foldersOnly)
			{
				// In folder mode, Enter selects current directory
				selectedPath = currentPath;
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
					if (!foldersOnly)
						PopulateFileList(parent.FullName);
				}
				e.Handled = true;
			}
		};

		// Handle folder selection change to update file list
		folderList.SelectedIndexChanged += (sender, args) =>
		{
			if (!foldersOnly && folderList.SelectedItem?.Tag is string folderPath && Directory.Exists(folderPath))
			{
				// Don't auto-navigate, just update file list preview
			}
		};

		// Complete task when modal closes
		modal.OnClosed += (s, e) => tcs.TrySetResult(selectedPath);

		// Add modal and activate
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);
		folderList.SetFocus(true, FocusReason.Programmatic);

		return tcs.Task;
	}

	private static Task<string?> ShowSaveFileInternalAsync(ConsoleWindowSystem windowSystem, string title, string? startPath, string? filter, string? defaultFileName, Window? parentWindow = null)
	{
		var tcs = new TaskCompletionSource<string?>();
		string? selectedPath = null;
		string currentPath = startPath ?? Environment.CurrentDirectory;
		string currentFileName = defaultFileName ?? "untitled.txt";

		// Ensure path exists
		if (!Directory.Exists(currentPath))
			currentPath = Environment.CurrentDirectory;

		// Create modal window
		var builder = new WindowBuilder(windowSystem)
			.WithTitle(title)
			.Centered()
			.WithSize(80, 26)
			.AsModal()
			.Resizable(true)
			.Minimizable(false)
			.Maximizable(true)
			.Movable(true);

		if (parentWindow != null)
			builder.WithParent(parentWindow);

		var modal = builder.Build();

		// Current path display
		var pathDisplay = Ctl.Markup()
			.AddLine($"[grey50]Folder:[/] [white]{EscapeMarkup(currentPath)}[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.WithName("PathDisplay")
			.Build();
		modal.AddControl(pathDisplay);

		// Separator
		modal.AddControl(Ctl.RuleBuilder()
						.Build());

		// Folder list (left panel)
		var folderList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.WithName("FolderList")
			.Build();

		// File list (right panel - shows existing files for reference/overwrite)
		var fileList = Ctl.List()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithDoubleClickActivation(true)
			.WithName("FileList")
			.Build();

		// Filename input field
		var filenameInput = Ctl.Prompt("")
			.WithInput(currentFileName)
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithMargin(1, 0, 1, 0)
			.StickyBottom()
			.WithName("FilenameInput")
			.Build();

		// Populate folder list helper
		void PopulateFolderList(string path)
		{
			folderList.ClearItems();

			try
			{
				var dirInfo = new DirectoryInfo(path);

				// Add parent directory entry if not root
				if (dirInfo.Parent != null)
				{
					folderList.AddItem(new ListItem("[yellow]📁[/] [grey70]..[/]") { Tag = dirInfo.Parent.FullName });
				}

				// Add subdirectories
				foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
				{
					try
					{
						folderList.AddItem(new ListItem($"[yellow]📁[/] {EscapeMarkup(dir.Name)}") { Tag = dir.FullName });
					}
					catch (UnauthorizedAccessException)
					{
						// Skip directories we can't access due to permissions
						// Errors are expected when browsing system directories
					}
				}
			}
			catch (Exception ex)
			{
				folderList.AddItem(new ListItem($"[red]Error: {EscapeMarkup(ex.Message)}[/]") { Tag = null });
			}

			folderList.SelectedIndex = 0;
			currentPath = path;

			// Update path display
			var pathCtrl = modal.FindControl<MarkupControl>("PathDisplay");
			if (pathCtrl != null)
			{
				pathCtrl.Text = $"[grey50]Folder:[/] [white]{EscapeMarkup(currentPath)}[/]";
			}
		}

		// Populate file list helper
		void PopulateFileList(string path)
		{
			fileList.ClearItems();

			try
			{
				var dirInfo = new DirectoryInfo(path);
				var files = dirInfo.GetFiles();

				// Apply filter if specified
				if (!string.IsNullOrEmpty(filter))
				{
					var patterns = filter.Split(';', StringSplitOptions.RemoveEmptyEntries);
					files = files.Where(f => patterns.Any(p =>
					{
						var pattern = p.Trim();
						// Handle wildcard patterns: *.txt, *.cs, *.*, etc.
						if (pattern == "*" || pattern == "*.*")
							return true;

						if (pattern.StartsWith("*."))
						{
							var extension = pattern.Substring(1); // Remove leading *
							return f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase);
						}

						// Fallback: exact filename match
						return f.Name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
					})).ToArray();
				}

				foreach (var file in files.OrderBy(f => f.Name))
				{
					var icon = GetFileIcon(file.Extension);
					var size = FormatFileSize(file.Length);
					fileList.AddItem(new ListItem($"{icon} {EscapeMarkup(file.Name)} [grey50]({size})[/]") { Tag = file.FullName });
				}
			}
			catch (Exception ex)
			{
				fileList.AddItem(new ListItem($"[red]Error: {EscapeMarkup(ex.Message)}[/]") { Tag = null });
			}

			if (fileList.Items.Count > 0)
				fileList.SelectedIndex = 0;
		}

		// Two-column layout for folder and file lists
		var grid = Ctl.HorizontalGrid()
			.Column(col => col.Flex(1).Add(folderList))
			.WithSplitterAfter(0)
			.Column(col => col.Flex(1).Add(fileList))
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();
		modal.AddControl(grid);

		// Bottom separator
		modal.AddControl(Ctl.RuleBuilder()
						.StickyBottom()
			.Build());

		// Filename label and input
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey50]Filename:[/]")
			.WithAlignment(HorizontalAlignment.Left)
			.WithMargin(1, 0, 1, 0)
			.StickyBottom()
			.Build());
		modal.AddControl(filenameInput);

		// Footer separator
		modal.AddControl(Ctl.RuleBuilder()
						.StickyBottom()
			.Build());

		// Footer with instructions
		modal.AddControl(Ctl.Markup()
			.AddLine("[grey70]Enter: Save  •  Backspace: Go Up  •  Escape: Cancel[/]")
			.WithAlignment(HorizontalAlignment.Center)
			.WithMargin(0, 0, 0, 0)
			.StickyBottom()
			.Build());

		// Initial population
		PopulateFolderList(currentPath);
		PopulateFileList(currentPath);

		// Handle folder list activation (navigate into folder)
		folderList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string folderPath && Directory.Exists(folderPath))
			{
				PopulateFolderList(folderPath);
				PopulateFileList(folderPath);
			}
		};

		// Handle file list activation (copy filename to input)
		fileList.ItemActivated += (sender, item) =>
		{
			if (item?.Tag is string filePath)
			{
				// Copy the filename to the input field (for overwriting)
				filenameInput.Input = Path.GetFileName(filePath);
				filenameInput.SetFocus(true, FocusReason.Programmatic);
			}
		};

		// Handle Enter key on filename input (PromptControl handles Enter internally)
		filenameInput.Entered += (sender, input) =>
		{
			var filename = input?.Trim();
			if (!string.IsNullOrEmpty(filename))
			{
				selectedPath = Path.Combine(currentPath, filename);
				modal.Close();
			}
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
			else if (e.KeyInfo.Key == ConsoleKey.Backspace && !filenameInput.HasFocus)
			{
				// Go up one directory (only when not in text input)
				var parent = Directory.GetParent(currentPath);
				if (parent != null)
				{
					PopulateFolderList(parent.FullName);
					PopulateFileList(parent.FullName);
				}
				e.Handled = true;
			}
		};

		// Complete task when modal closes
		modal.OnClosed += (s, e) => tcs.TrySetResult(selectedPath);

		// Add modal and activate
		windowSystem.AddWindow(modal);
		windowSystem.SetActiveWindow(modal);
		filenameInput.SetFocus(true, FocusReason.Programmatic);

		return tcs.Task;
	}

	private static string EscapeMarkup(string text)
	{
		return text.Replace("[", "[[").Replace("]", "]]");
	}

	private static string GetFileIcon(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".txt" or ".md" or ".log" => "[grey70]📄[/]",
			".cs" or ".js" or ".ts" or ".py" or ".java" => "[cyan1]📄[/]",
			".json" or ".xml" or ".yaml" or ".yml" => "[yellow]📄[/]",
			".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "[green]🖼[/]",
			".exe" or ".dll" => "[red]⚙[/]",
			_ => "[grey50]📄[/]"
		};
	}

	private static string FormatFileSize(long bytes)
	{
		if (bytes < 1024) return $"{bytes} B";
		if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
		if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
		return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
	}
}
