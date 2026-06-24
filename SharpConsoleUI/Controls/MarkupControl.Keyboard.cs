// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Focus-gate + keyboard navigation surface for <see cref="MarkupControl"/>: the control is only
	/// focusable when it is enabled and its content contains at least one clickable link. When focused,
	/// Left/Right move between links (no wrap), Enter activates the focused link, and other keys bubble
	/// to the host (so Up/Down/PageUp/PageDown scroll the containing panel).
	/// </summary>
	public partial class MarkupControl
	{
		private bool _isEnabled = true;
		private int? _cachedLinkCount;   // content-derived, paint-independent

		/// <summary>Index of the currently focused link in document order, or -1 when none is focused.</summary>
		private int _focusedLinkIndex = -1;

		/// <summary>Gets or sets whether this control is enabled for input.</summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); Container?.Invalidate(Invalidation.Repaint); }
		}

		/// <summary>Number of clickable links in the current content (cached; derived from content, not paint).</summary>
		private int LinkCount
		{
			get
			{
				// Compute AND write the cache under _contentLock so a concurrent content mutation
				// (which clears the cache under the same lock) can't be overwritten by a stale value.
				// CountLinks is a pure static, safe to call while holding the lock.
				lock (_contentLock)
				{
					if (_cachedLinkCount.HasValue) return _cachedLinkCount.Value;
					int total = 0;
					foreach (var line in _content)
						total += Parsing.MarkupParser.CountLinks(line);
					_cachedLinkCount = total;
					return total;
				}
			}
		}

		/// <summary>Invalidates the cached link count after a content change.</summary>
		private void InvalidateLinkCount()
		{
			lock (_contentLock) { _cachedLinkCount = null; }
		}

		/// <summary>Gets whether this control can receive keyboard focus: only when visible, enabled, AND it has links.</summary>
		public bool CanReceiveFocus => Visible && _isEnabled && LinkCount > 0;

		/// <summary>Gets whether this control currently has focus.</summary>
		public bool HasFocus => ComputeHasFocus();

		/// <summary>We do not consume Tab; arrow keys navigate links. Default false so Tab traverses focus.</summary>
		public bool WantsTabKey => false;

		/// <summary>
		/// One-time wiring (called from the constructor) to initialize the focused link when this control
		/// gains focus, and to refresh the highlight when it loses focus.
		/// </summary>
		private void InitKeyboardNav()
		{
			GotFocus += OnGotFocus;
			LostFocus += OnLostFocus;
		}

		private void OnGotFocus(object? sender, EventArgs e)
		{
			// Initialize the focused link to the first VISIBLE link (R3); fall back to index 0.
			_focusedLinkIndex = FindFirstVisibleLinkIndex();
			Container?.Invalidate(Invalidation.Repaint);
		}

		private void OnLostFocus(object? sender, EventArgs e)
		{
			// Keep the index (so re-entry can resume), but invalidate to clear the highlight.
			Container?.Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Picks the first flattened link whose rendered row is within the last painted clip rectangle.
		/// Falls back to 0 when nothing is determinable (no paint yet / no visible link) — scroll-into-view
		/// then brings the chosen link into view.
		/// </summary>
		private int FindFirstVisibleLinkIndex()
		{
			var links = FlattenLinks();
			if (links.Count == 0) return -1;

			lock (_selectionLock)
			{
				// _lastClipRect / _cachedRowAbsoluteY are co-written in PaintDOM under _selectionLock.
				if (_lastClipRectValid)
				{
					for (int i = 0; i < links.Count; i++)
					{
						int row = links[i].row;
						if (row >= 0 && row < _cachedRowAbsoluteY.Length)
						{
							int y = _cachedRowAbsoluteY[row];
							if (y >= _lastClipRectY && y < _lastClipRectBottom)
								return i;
						}
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// Flattens the cached per-row link spans into document order: each entry is the link's rendered
		/// row index and the span. Returns empty when not yet painted.
		/// </summary>
		private List<(int row, Parsing.LinkSpan span)> FlattenLinks()
		{
			var result = new List<(int, Parsing.LinkSpan)>();
			lock (_selectionLock)
			{
				for (int row = 0; row < _cachedRowLinks.Count; row++)
				{
					var spans = _cachedRowLinks[row];
					if (spans == null) continue;
					foreach (var span in spans)
						result.Add((row, span));
				}
			}
			return result;
		}

		/// <summary>
		/// Keyboard handling for link navigation. Left/Right move between links (no wrap; at the first/last
		/// link the key bubbles by returning false). Enter activates the focused link. All other keys bubble.
		/// </summary>
		/// <param name="key">The key to process.</param>
		/// <returns>True if the key was consumed; false to let it bubble to the host.</returns>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !ComputeHasFocus()) return false;
			var links = FlattenLinks();
			if (links.Count == 0) return false;

			switch (key.Key)
			{
				case ConsoleKey.RightArrow:
					if (_focusedLinkIndex >= links.Count - 1) return false; // last link → bubble (no wrap)
					SetFocusedLinkIndex(_focusedLinkIndex + 1, links);
					return true;

				case ConsoleKey.LeftArrow:
					if (_focusedLinkIndex <= 0) return false;               // first link/none → bubble
					SetFocusedLinkIndex(_focusedLinkIndex - 1, links);
					return true;

				case ConsoleKey.Enter:
					if (_focusedLinkIndex >= 0 && _focusedLinkIndex < links.Count)
					{
						var span = links[_focusedLinkIndex].span;
						RaiseLinkClicked(span.Url, span.Text, null);
						return true;
					}
					return false;

				default:
					return false; // Up/Down/PageUp/PageDown/etc. bubble to the host scroller
			}
		}

		/// <summary>Sets the focused link, scrolls it into view, and invalidates for repaint.</summary>
		private void SetFocusedLinkIndex(int i, IReadOnlyList<(int row, Parsing.LinkSpan span)> links)
		{
			_focusedLinkIndex = i;
			ScrollFocusedLinkIntoView(links[i].row);
			Container?.Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Walks the ancestor chain to the nearest <see cref="IScrollableContainer"/> and asks it to bring
		/// the focused link's row into view, using the region API against the scroller's direct child.
		/// No-op when not yet painted or when no scrollable ancestor exists.
		/// </summary>
		private void ScrollFocusedLinkIntoView(int linkRow)
		{
			// Guard: need a valid paint (absolute Y of rows) to compute offsets.
			bool painted;
			lock (_selectionLock) { painted = _cachedRowLinks.Count > 0; }
			if (!painted) return;

			// Find the nearest scrollable ancestor and the direct child of that scroller on our path.
			// FocusManager.ResolveParentWindowControl handles transparent containers (ColumnContainer/HGrid)
			// so the "direct child" matches what the scroller knows in its child-slot layout.
			IWindowControl? current = this;
			IScrollableContainer? scroller = null;
			IWindowControl? directChild = null;

			while (current != null)
			{
				var parent = FocusManager.ResolveParentWindowControl(current);
				if (parent == null) break;
				if (parent is IScrollableContainer sc)
				{
					scroller = sc;
					directChild = current;
					break;
				}
				current = parent;
			}

			if (scroller == null || directChild == null) return;

			// The focused link's absolute screen Y mirrors PaintDOM: startY = bounds.Y + Margin.Top, row = startY + linkRow.
			int linkAbsoluteY = ActualY + Margin.Top + linkRow;
			int regionTop = linkAbsoluteY - directChild.ActualY;
			scroller.ScrollChildRegionIntoView(directChild, regionTop, 1);
		}
	}
}
