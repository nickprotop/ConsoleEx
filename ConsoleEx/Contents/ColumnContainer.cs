using Spectre.Console;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ConsoleEx.Contents
{
	public class ColumnContainer : IContainer
	{
		private List<string>? _cachedContent;
		private ConsoleWindowSystem? _consoleWindowSystem;
		private List<IWIndowContent> _contents = new List<IWIndowContent>();
		private HorizontalGridContent _horizontalGridContent;
		private bool _isDirty;
		private int? _width;

		public ColumnContainer(HorizontalGridContent horizontalGridContent)
		{
			_horizontalGridContent = horizontalGridContent;
			_consoleWindowSystem = horizontalGridContent.Container?.GetConsoleWindowSystem;

			BackgroundColor = _consoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black;
			ForegroundColor = _consoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White;
		}

		public Color BackgroundColor { get; set; }
		public Color ForegroundColor { get; set; }

		public ConsoleWindowSystem? GetConsoleWindowSystem
		{ get => _consoleWindowSystem; set { _consoleWindowSystem = value; } }

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
				Invalidate();
			}
		}

		public void AddContent(IWIndowContent content)
		{
			content.Container = this;
			_contents.Add(content);
			Invalidate();
		}

		public List<IInteractiveContent> GetInteractiveContents()
		{
			List<IInteractiveContent> interactiveContents = new List<IInteractiveContent>();
			foreach (var content in _contents)
			{
				if (content is IInteractiveContent interactiveContent)
				{
					interactiveContents.Add(interactiveContent);
				}
			}
			return interactiveContents;
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

		public void Invalidate()
		{
			_isDirty = true;
			_cachedContent = null;
			_horizontalGridContent.Invalidate();
		}

		public void RemoveContent(IWIndowContent content)
		{
			if (_contents.Remove(content))
			{
				content.Container = null;
				content.Dispose();
				Invalidate();
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_isDirty && _cachedContent != null)
			{
				return _cachedContent;
			}

			_cachedContent = new List<string>();

			// Render each content and collect the lines
			foreach (var content in _contents)
			{
				var renderedContent = content.RenderContent(availableWidth, availableHeight);
				_cachedContent.AddRange(renderedContent);
			}

			_isDirty = false;
			return _cachedContent;
		}
	}
}
