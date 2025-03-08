using ConsoleEx;
using ConsoleEx.Controls;
using Spectre.Console;
using System;

namespace ConsoleEx.Example
{
	public class FileExplorerWindow
	{
		private readonly TreeControl _fileTree;
		private readonly MarkupControl _statusControl;
		private readonly ConsoleWindowSystem _system;
		private readonly Window _window;

		public FileExplorerWindow(ConsoleWindowSystem system)
		{
			_system = system;

			// Create the main window
			_window = new Window(system)
			{
				Title = "File Explorer Example",
				Width = 70,
				Height = 24,
				Top = 3,
				Left = 5
			};

			// Add a title and rule
			_window.AddContent(new MarkupControl(new List<string> { "[bold]File Explorer[/]" })
			{
				Alignment = Alignment.Center,
				StickyPosition = StickyPosition.Top
			});

			_window.AddContent(new RuleControl() { StickyPosition = StickyPosition.Top });

			// Add buttons for controlling the tree
			var buttonContainer = new HorizontalGridControl()
			{
				Alignment = Alignment.Left,
				StickyPosition = StickyPosition.Top
			};

			_window.AddContent(buttonContainer);

			// Create status display
			_statusControl = new MarkupControl(new List<string> { "No file selected" })
			{
				Alignment = Alignment.Left,
				StickyPosition = StickyPosition.Bottom
			};

			// Create the tree control
			_fileTree = new TreeControl()
			{
				Alignment = Alignment.Left,
				HighlightBackgroundColor = Color.Blue,
				HighlightForegroundColor = Color.White,
				Guide = TreeGuide.Line // Use double lines for the tree structure
			};

			// Set up event handlers
			_fileTree.OnSelectedNodeChanged = (tree, node) =>
			{
				if (node != null)
				{
					_statusControl.SetContent(new List<string> { $"Selected: [green]{node.Text}[/]" });
				}
				else
				{
					_statusControl.SetContent(new List<string> { "No file selected" });
				}
			};

			_fileTree.OnNodeExpandCollapse = (tree, node) =>
			{
				// You can perform actions when nodes are expanded/collapsed
				if (node.IsExpanded)
				{
					_statusControl.SetContent(new List<string> { $"Expanded: [yellow]{node.Text}[/]" });
				}
				else
				{
					_statusControl.SetContent(new List<string> { $"Collapsed: [yellow]{node.Text}[/]" });
				}
			};

			// Populate tree with sample data
			PopulateTreeWithSampleData();

			// Add controls to window
			HorizontalGridControl mainPanel = new HorizontalGridControl();

			ColumnContainer fileTreeColumn = new ColumnContainer(mainPanel)
			{
				Width = 30
			};
			fileTreeColumn.AddContent(_fileTree);
			mainPanel.AddColumn(fileTreeColumn);

			ColumnContainer itemsColumn = new ColumnContainer(mainPanel);
			itemsColumn.AddContent(new MarkupControl(new List<string> { "Items" })
			{
				Alignment = Alignment.Center
			});
			mainPanel.AddColumn(itemsColumn);

			mainPanel.AddSplitter(0, new SplitterControl());

			_window.AddContent(mainPanel);

			_window.AddContent(new RuleControl() { StickyPosition = StickyPosition.Bottom });
			_window.AddContent(_statusControl);

			ColumnContainer expandButtonColumn = new ColumnContainer(buttonContainer);
			ColumnContainer collapseButtonColumn = new ColumnContainer(buttonContainer);

			var expandButton = new ButtonControl()
			{
				Width = 15,
				Text = "Expand All"
			};
			expandButton.OnClick = (sender) =>
			{
				_fileTree.ExpandAll();
			};

			expandButtonColumn.AddContent(expandButton);

			var collapseButton = new ButtonControl()
			{
				Width = 15,
				Text = "Collapse All"
			};
			collapseButton.OnClick = (sender) =>
			{
				_fileTree.CollapseAll();
			};

			collapseButtonColumn.AddContent(collapseButton);

			buttonContainer.AddColumn(expandButtonColumn);
			buttonContainer.AddColumn(collapseButtonColumn);

			// Add the window to the console system
			system.AddWindow(_window);
		}

		// Helper method to get the window
		public Window GetWindow() => _window;

		private void PopulateTreeWithSampleData()
		{
			// Create root nodes
			var documentsNode = _fileTree.AddRootNode("Documents");
			documentsNode.TextColor = Color.Cyan1;

			var picturesNode = _fileTree.AddRootNode("Pictures");
			picturesNode.TextColor = Color.Cyan1;

			var musicNode = _fileTree.AddRootNode("Music");
			musicNode.TextColor = Color.Cyan1;

			// Add children to Documents
			var workNode = documentsNode.AddChild("Work");
			workNode.TextColor = Color.Yellow;

			var personalNode = documentsNode.AddChild("Personal");
			personalNode.TextColor = Color.Yellow;

			// Add sub-folders to Work
			var projectsNode = workNode.AddChild("Projects");
			projectsNode.AddChild("Project A");
			projectsNode.AddChild("Project B");
			projectsNode.AddChild("Project C");

			var reportsNode = workNode.AddChild("Reports");
			reportsNode.AddChild("Q1 Report.docx");
			reportsNode.AddChild("Q2 Report.docx");

			// Add files to Personal
			personalNode.AddChild("Resume.docx");
			personalNode.AddChild("Family Budget.xlsx");
			personalNode.AddChild("Shopping List.txt");

			// Add children to Pictures
			var vacationNode = picturesNode.AddChild("Vacation");
			vacationNode.TextColor = Color.Yellow;
			vacationNode.AddChild("Beach.jpg");
			vacationNode.AddChild("Mountains.jpg");
			vacationNode.AddChild("City.jpg");

			var familyNode = picturesNode.AddChild("Family");
			familyNode.TextColor = Color.Yellow;
			familyNode.AddChild("Birthday Party.jpg");
			familyNode.AddChild("Christmas.jpg");

			// Add children to Music
			var rockNode = musicNode.AddChild("Rock");
			rockNode.TextColor = Color.Yellow;
			rockNode.AddChild("Favorite Song.mp3");

			var jazzNode = musicNode.AddChild("Jazz");
			jazzNode.TextColor = Color.Yellow;
			jazzNode.AddChild("Smooth Jazz.mp3");
			jazzNode.AddChild("Fast Jazz.mp3");
		}
	}
}