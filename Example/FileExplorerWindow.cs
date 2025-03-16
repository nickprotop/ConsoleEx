using SharpConsoleUI;
using SharpConsoleUI.Controls;
using Spectre.Console;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TreeNode = SharpConsoleUI.Controls.TreeNode;

namespace SharpConsoleUI.Example
{
	public class FileExplorerWindow
	{
		private readonly TreeControl _fileTree;
		private readonly ColumnContainer _itemsColumn;
		private readonly MarkupControl _statusControl;
		private readonly ConsoleWindowSystem _system;
		private readonly Window _window;
		private ListControl _fileListControl;

		public FileExplorerWindow(ConsoleWindowSystem system)
		{
			_system = system;

			// Create the main window
			_window = new Window(system)
			{
				Title = "File Explorer Example",
				Width = 70,
				Height = 24,
				Top = 0,
				Left = 0
			};

			// Add buttons for controlling the tree
			var buttonContainer = new HorizontalGridControl()
			{
				Alignment = Alignment.Left,
				StickyPosition = StickyPosition.Top
			};

			_window.AddControl(buttonContainer);

			_window.AddControl(new RuleControl() { StickyPosition = StickyPosition.Top });

			// Create status display
			_statusControl = new MarkupControl(new List<string> { "No folder selected" })
			{
				Alignment = Alignment.Left,
				StickyPosition = StickyPosition.Bottom
			};

			// Create the tree control
			_fileTree = new TreeControl()
			{
				Margin = new Margin(1, 1, 1, 1),
				Alignment = Alignment.Left,
				HighlightBackgroundColor = Color.Blue,
				HighlightForegroundColor = Color.White,
				Guide = TreeGuide.Line
			};

			// Set up event handlers
			_fileTree.OnSelectedNodeChanged = (tree, node) =>
			{
				if (node != null)
				{
					DirectoryInfo? dirInfo = node.Tag as DirectoryInfo;
					if (dirInfo != null)
					{
						_statusControl.SetContent(new List<string> { $"Selected: [green]{node.Text}[/] - [yellow]{dirInfo.FullName}[/]" });

						// Update the file list when a folder is selected
						UpdateFileList(dirInfo);
					}
					else
					{
						_statusControl.SetContent(new List<string> { $"Selected: [green]{node.Text}[/]" });
					}
				}
				else
				{
					_statusControl.SetContent(new List<string> { "No folder selected" });
				}
			};

			_fileTree.OnNodeExpandCollapse = (tree, node) =>
			{
				if (node.IsExpanded)
				{
					DirectoryInfo? dirInfo = node.Tag as DirectoryInfo;
					if (dirInfo != null)
					{
						_statusControl.SetContent(new List<string> { $"Loading: [yellow]{dirInfo.FullName}[/]" });

						// Clear any placeholder nodes
						node.ClearChildren();

						// Load subfolders when expanding
						LoadSubfolders(node, dirInfo);

						// Update the file list for the expanded folder
						UpdateFileList(dirInfo);

						_statusControl.SetContent(new List<string> { $"Expanded: [yellow]{node.Text}[/]" });
					}
				}
				else
				{
					_statusControl.SetContent(new List<string> { $"Collapsed: [yellow]{node.Text}[/]" });
				}
			};

			// Create the file list control
			_fileListControl = new ListControl()
			{
				Margin = new Margin(1, 1, 1, 1),
				Alignment = Alignment.Strecth,
				MaxVisibleItems = null,
				FillHeight = true,
				BackgroundColor = _window.BackgroundColor,
				FocusedBackgroundColor = _window.BackgroundColor,
				IsSelectable = false
			};

			// Handle file selection
			_fileListControl.SelectedItemChanged += (sender, item) =>
			{
				if (item != null && item.Tag is FileInfo fileInfo)
				{
					_statusControl.SetContent(new List<string> { $"File: [green]{fileInfo.Name}[/] - [yellow]{fileInfo.FullName}[/] - {FormatFileSize(fileInfo.Length)}" });
				}
			};

			// Populate tree with drives and folders
			PopulateTreeWithDrives();

			// Add controls to window
			HorizontalGridControl mainPanel = new HorizontalGridControl();

			ColumnContainer fileTreeColumn = new ColumnContainer(mainPanel)
			{
				Width = 30
			};
			fileTreeColumn.AddContent(_fileTree);
			mainPanel.AddColumn(fileTreeColumn);

			_itemsColumn = new ColumnContainer(mainPanel);

			// Add title for the files section
			_itemsColumn.AddContent(new MarkupControl(new List<string> { "[bold]Files in Selected Folder[/]" })
			{
				Alignment = Alignment.Center
			});

			// Add the file list control to the items column
			_itemsColumn.AddContent(_fileListControl);

			// Initialize with an empty file list message
			CreateEmptyFileList();

			mainPanel.AddColumn(_itemsColumn);
			mainPanel.AddSplitter(0, new SplitterControl());

			_window.AddControl(mainPanel);

			_window.AddControl(new RuleControl() { StickyPosition = StickyPosition.Bottom });
			_window.AddControl(_statusControl);

			ColumnContainer expandButtonColumn = new ColumnContainer(buttonContainer);
			ColumnContainer collapseButtonColumn = new ColumnContainer(buttonContainer);
			ColumnContainer refreshButtonColumn = new ColumnContainer(buttonContainer);

			var expandButton = new ButtonControl()
			{
				Width = 12,
				Text = "Expand All"
			};
			expandButton.OnClick = (sender) =>
			{
				_fileTree.ExpandAll();
			};
			expandButtonColumn.AddContent(expandButton);

			var collapseButton = new ButtonControl()
			{
				Width = 12,
				Text = "Collapse All"
			};
			collapseButton.OnClick = (sender) =>
			{
				_fileTree.CollapseAll();
			};
			collapseButtonColumn.AddContent(collapseButton);

			var refreshButton = new ButtonControl()
			{
				Width = 12,
				Text = "Refresh"
			};
			refreshButton.OnClick = (sender) =>
			{
				_fileTree.Clear();
				PopulateTreeWithDrives();
				CreateEmptyFileList();
			};
			refreshButtonColumn.AddContent(refreshButton);

			buttonContainer.AddColumn(expandButtonColumn);
			buttonContainer.AddColumn(collapseButtonColumn);
			buttonContainer.AddColumn(refreshButtonColumn);

			// Add the window to the console system
			system.AddWindow(_window);
		}

		// Helper method to get the window
		public Window GetWindow() => _window;

		private void AddPlaceholderIfHasSubfolders(TreeNode node, string path)
		{
			try
			{
				// Check if the directory has any subdirectories
				bool hasSubfolders = Directory.EnumerateDirectories(path).Any();

				if (hasSubfolders)
				{
					// Add a placeholder node so the expand icon is shown
					var placeholder = node.AddChild("Loading...");
					placeholder.TextColor = Color.Grey;
				}
			}
			catch
			{
				// If we can't check, add a placeholder just in case
				var placeholder = node.AddChild("Loading...");
				placeholder.TextColor = Color.Grey;
			}
		}

		// Creates an empty file list
		private void CreateEmptyFileList()
		{
			_fileListControl.ClearItems();
			_fileListControl.AddItem("No folder selected", "ℹ", Color.Grey);
		}

		// Format file size in human-readable format
		private string FormatFileSize(long bytes)
		{
			string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
			int order = 0;
			double size = bytes;

			while (size >= 1024 && order < suffixes.Length - 1)
			{
				order++;
				size /= 1024;
			}

			// Format with appropriate decimal places
			if (order == 0) return $"{size:0} {suffixes[order]}";
			return $"{size:0.##} {suffixes[order]}";
		}

		// Get an appropriate color based on file extension
		private Color GetFileColor(string extension)
		{
			switch (extension.ToLowerInvariant())
			{
				case ".exe":
				case ".bat":
				case ".cmd":
				case ".ps1":
					return Color.Green;

				case ".dll":
				case ".lib":
				case ".obj":
					return Color.Blue;

				case ".jpg":
				case ".jpeg":
				case ".png":
				case ".gif":
				case ".bmp":
					return Color.Magenta1;

				case ".txt":
				case ".log":
				case ".md":
					return Color.Yellow;

				case ".doc":
				case ".docx":
				case ".xls":
				case ".xlsx":
				case ".pdf":
					return Color.Cyan1;

				case ".zip":
				case ".rar":
				case ".7z":
				case ".tar":
				case ".gz":
					return Color.Red;

				default:
					return Color.White;
			}
		}

		// Get an appropriate icon based on file extension
		private string GetFileIcon(string extension)
		{
			switch (extension.ToLowerInvariant())
			{
				case ".exe":
				case ".bat":
				case ".cmd":
				case ".ps1":
					return "▶";

				case ".dll":
				case ".lib":
				case ".obj":
					return "◆";

				case ".jpg":
				case ".jpeg":
				case ".png":
				case ".gif":
				case ".bmp":
					return "□";

				case ".txt":
				case ".log":
				case ".md":
					return "📄";

				case ".doc":
				case ".docx":
				case ".xls":
				case ".xlsx":
				case ".pdf":
					return "📑";

				case ".zip":
				case ".rar":
				case ".7z":
				case ".tar":
				case ".gz":
					return "📦";

				default:
					return "◯";
			}
		}

		private void LoadSubfolders(TreeNode parentNode, DirectoryInfo directory)
		{
			try
			{
				// Get subdirectories
				var subdirectories = directory.GetDirectories()
					.OrderBy(d => d.Name)
					.ToList();

				foreach (var subdir in subdirectories)
				{
					try
					{
						if ((subdir.Attributes & FileAttributes.Hidden) == 0) // Skip hidden folders
						{
							var folderNode = parentNode.AddChild(subdir.Name);
							folderNode.TextColor = Color.Yellow;
							folderNode.Tag = subdir;

							// Add a placeholder child if this folder has subfolders
							AddPlaceholderIfHasSubfolders(folderNode, subdir.FullName);

							// Initialize as collapsed
							folderNode.IsExpanded = false;
						}
					}
					catch (UnauthorizedAccessException)
					{
						// Handle access denied
						var folderNode = parentNode.AddChild($"{subdir.Name} [Access Denied]");
						folderNode.TextColor = Color.Red;
					}
					catch
					{
						// Skip folders we can't process
					}
				}
			}
			catch (Exception ex)
			{
				_statusControl.SetContent(new List<string> { $"Error loading folders: [red]{ex.Message}[/]" });
			}
		}

		private void PopulateTreeWithDrives()
		{
			try
			{
				// Get all drives
				var drives = DriveInfo.GetDrives();

				// Add each drive as a root node
				foreach (var drive in drives)
				{
					if (drive.IsReady)
					{
						var driveNode = _fileTree.AddRootNode($"{drive.Name} [{drive.DriveType}]");
						driveNode.TextColor = Color.Cyan1;
						driveNode.Tag = new DirectoryInfo(drive.RootDirectory.FullName);

						// Add placeholder child to show expand icon
						AddPlaceholderIfHasSubfolders(driveNode, drive.RootDirectory.FullName);

						// Collapse the drive node by default
						driveNode.IsExpanded = false;
					}
					else
					{
						var driveNode = _fileTree.AddRootNode($"{drive.Name} [Not Ready]");
						driveNode.TextColor = Color.Grey;
					}
				}
			}
			catch (Exception ex)
			{
				_statusControl.SetContent(new List<string> { $"Error: [red]{ex.Message}[/]" });
			}
		}

		// Updates the file list based on the selected folder
		private void UpdateFileList(DirectoryInfo directory)
		{
			if (directory == null)
			{
				CreateEmptyFileList();
				return;
			}

			try
			{
				_fileListControl.ClearItems();

				// Get files in the directory
				var files = directory.GetFiles()
					.Where(f => (f.Attributes & FileAttributes.Hidden) == 0) // Skip hidden files
					.OrderBy(f => f.Name)
					.ToList();

				if (files.Count == 0)
				{
					// No files found in this folder
					_fileListControl.AddItem("No files in this folder", "ℹ", Color.Grey);
				}
				else
				{
					// Add each file to the list
					foreach (var file in files)
					{
						string extension = Path.GetExtension(file.Name);
						string sizeInfo = FormatFileSize(file.Length);
						string modified = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");

						// Create a formatted display with multiple lines of information
						string displayText = $"{file.Name}\nSize: {sizeInfo} | Modified: {modified}";

						// Create list item with icon based on file type
						var listItem = new ListItem(displayText, GetFileIcon(extension), GetFileColor(extension));

						// Store the FileInfo object in the Tag for later use
						listItem.Tag = file;

						// Add the item to the list
						_fileListControl.AddItem(listItem);
					}
				}
			}
			catch (Exception ex)
			{
				// Handle errors
				_fileListControl.ClearItems();
				_fileListControl.AddItem($"Error: {ex.Message}", "⚠", Color.Red);
				_statusControl.SetContent(new List<string> { $"Error loading files: [red]{ex.Message}[/]" });
			}
		}
	}
}