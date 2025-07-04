﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
	public class MarkupControl : IWIndowControl
	{
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private List<string> _content;
		private Alignment _justify = Alignment.Left;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private bool _wrap = true;

		public MarkupControl(List<string> lines)
		{
			_content = lines;
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		public int? ActualWidth
		{
			get
			{
				if (_contentCache.Content == null) return null;
				int maxLength = 0;
				foreach (var line in _contentCache.Content)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _justify; set { _justify = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public IContainer? Container { get; set; }

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public string Text
		{
			get => string.Join("\n", _content);
			set
			{
				_content = value.Split('\n').ToList();
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public int? Width
		{ get => _width; set { _width = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public bool Wrap
		{ get => _wrap; set { _wrap = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); Container?.Invalidate(true); } }

		public void Dispose()
		{
			_contentCache.Dispose();
			Container = null;
		}

		public void Invalidate()
		{
			_contentCache.Invalidate(InvalidationReason.ContentChanged);
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// Calculate the natural size based on content
			int maxWidth = 0;
			foreach (var line in _content)
			{
				int length = AnsiConsoleHelper.StripSpectreLength(line);
				if (length > maxWidth) maxWidth = length;
			}
			return new System.Drawing.Size(maxWidth, _content.Count);
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			var renderedContent = new List<string>();

			int maxContentWidth = 0;
			foreach (var line in _content)
			{
				int length = AnsiConsoleHelper.StripSpectreLength(line);
				if (length > maxContentWidth) maxContentWidth = length;
			}

			int paddingLeft = 0;
			if (Alignment == Alignment.Center)
			{
				paddingLeft = ContentHelper.GetCenter(availableWidth ?? 80, maxContentWidth);
			}

			foreach (var line in _content)
			{
				var ansiLines = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{line}", _width ?? availableWidth ?? 50, availableHeight, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
				renderedContent.AddRange(ansiLines);
			}

			for (int i = 0; i < renderedContent.Count; i++)
			{
				renderedContent[i] = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi($"{new string(' ', paddingLeft)}", paddingLeft, 1, false, Container?.BackgroundColor, null).FirstOrDefault() + renderedContent[i];
			}

			return renderedContent;
		}

		public void SetContent(List<string> lines)
		{
			_content = lines;
			_contentCache.Invalidate(InvalidationReason.PropertyChanged);
			Container?.Invalidate(true);
		}
	}
}