// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	public class ColumnContainer : IContainer
	{
		private Color? _backgroundColor;
		private List<string>? _cachedContent;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private List<IWIndowControl> _contents = new List<IWIndowControl>();
		private Color? _foregroundColor;
		private HorizontalGridControl _horizontalGridContent;
		private bool _isDirty;
		private int? _width;

		public ColumnContainer(HorizontalGridControl horizontalGridContent)
		{
			_horizontalGridContent = horizontalGridContent;
			_consoleWindowSystem = horizontalGridContent.Container?.GetConsoleWindowSystem;
		}

		public Color BackgroundColor
		{ get { return _backgroundColor ?? _consoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black; } set { _backgroundColor = value; Invalidate(true); } }

		public Color ForegroundColor
		{ get { return _foregroundColor ?? _consoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White; } set { _foregroundColor = value; Invalidate(true); } }

		public ConsoleWindowSystem? GetConsoleWindowSystem
		{ get => _consoleWindowSystem; set { _consoleWindowSystem = value; foreach (IWIndowControl control in _contents) { control.Invalidate(); }; Invalidate(true); } }

		public HorizontalGridControl HorizontalGridContent
		{
			get => _horizontalGridContent;
			set
			{
				_horizontalGridContent = value;
				_consoleWindowSystem = value.Container?.GetConsoleWindowSystem;

				_horizontalGridContent.Invalidate();
			}
		}

		public bool IsDirty
		{
			get => _isDirty;
			set
			{
				_isDirty = value;
			}
		}

		public int? Width
		{
			get => _width;
			set
			{
				_width = value;
				Invalidate(true);
			}
		}

		public void AddContent(IWIndowControl content)
		{
			content.Container = this;
			_contents.Add(content);
			Invalidate(true);
		}

		public int? GetActualWidth()
		{
			if (_cachedContent == null) return null;

			int maxLength = 0;
			foreach (var line in _cachedContent)
			{
				int length = AnsiConsoleHelper.StripAnsiStringLength(line);
				if (length > maxLength) maxLength = length;
			}
			return maxLength;
		}

		public List<IInteractiveControl> GetInteractiveContents()
		{
			List<IInteractiveControl> interactiveContents = new List<IInteractiveControl>();
			foreach (var content in _contents)
			{
				if (content is IInteractiveControl interactiveContent)
				{
					interactiveContents.Add(interactiveContent);
				}
			}
			return interactiveContents;
		}

		public void Invalidate(bool redrawAll, IWIndowControl? callerControl = null)
		{
			_isDirty = true;
			_cachedContent = null;
			_horizontalGridContent.Invalidate();
		}

		public void InvalidateOnlyColumnContents()
		{
			_isDirty = true;
			_cachedContent = null;
			foreach (var content in _contents)
			{
				content.Invalidate();
			}
		}

		public void RemoveContent(IWIndowControl content)
		{
			if (_contents.Remove(content))
			{
				content.Container = null;
				content.Dispose();
				Invalidate(true);
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_isDirty && _cachedContent != null)
			{
				return _cachedContent;
			}

			_cachedContent = new List<string>();

			foreach (var content in _contents)
			{
				content.Invalidate();
			}

			// Render each content and collect the lines
			foreach (var content in _contents)
			{
				var renderedContent = content.RenderContent(_width ?? availableWidth, availableHeight);
				_cachedContent.AddRange(renderedContent);
			}

			_isDirty = false;
			return _cachedContent;
		}
	}
}