// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Clickable-link support for <see cref="MarkupControl"/>. Link spans recorded during markup
	/// parsing are cached per rendered row (alongside the selection layout cache, guarded by the
	/// same lock) and hit-tested on click. Raises <see cref="LinkClicked"/> when a click lands on a link.
	/// </summary>
	public partial class MarkupControl
	{
		// Per-rendered-row link spans, captured in PaintDOM. Guarded by _selectionLock (shared with
		// the selection layout cache so both stay consistent under the same paint).
		private List<List<LinkSpan>> _cachedRowLinks = new();

		// Focused-link highlight colors. Like selection, these use DEFINITE colors (not an fg/bg swap),
		// because a swap is unsafe when the link cell's background is Color.Transparent (the default
		// effective bg) — swapping would make the foreground transparent and render the link invisible.
		private Color? _focusedLinkForegroundColor = null;
		private Color? _focusedLinkBackgroundColor = null;

		// Default focused-link highlight pair (high contrast, theme-independent): black on amber.
		private static readonly Color DefaultFocusedLinkForeground = Color.Black;
		private static readonly Color DefaultFocusedLinkBackground = new Color(235, 175, 60);

		/// <summary>
		/// Gets or sets the foreground color used to highlight the keyboard-focused link.
		/// Null uses a default high-contrast color. (Used only when the control has focus and a link is focused.)
		/// </summary>
		public Color? FocusedLinkForegroundColor
		{
			get => _focusedLinkForegroundColor;
			set => SetProperty(ref _focusedLinkForegroundColor, value);
		}

		/// <summary>
		/// Gets or sets the background color used to highlight the keyboard-focused link.
		/// Null uses a default high-contrast color.
		/// </summary>
		public Color? FocusedLinkBackgroundColor
		{
			get => _focusedLinkBackgroundColor;
			set => SetProperty(ref _focusedLinkBackgroundColor, value);
		}

		// Absolute screen Y of each rendered row + the last clip rectangle, captured in PaintDOM under
		// _selectionLock. Used to pick the first VISIBLE link on focus-gain (keyboard nav, R3).
		private int[] _cachedRowAbsoluteY = System.Array.Empty<int>();
		private int _lastClipRectY = 0;
		private int _lastClipRectBottom = 0;
		private bool _lastClipRectValid = false;

		/// <summary>
		/// Occurs when a click lands on a rendered link. The argument carries the link
		/// <see cref="LinkClickedEventArgs.Url"/> and visible <see cref="LinkClickedEventArgs.Text"/>.
		/// </summary>
		public event EventHandler<LinkClickedEventArgs>? LinkClicked;

		/// <summary>Stores per-row link spans from the latest paint. Call inside PaintDOM with the same rows as the selection cache.</summary>
		private void UpdateLinkLayoutCache(List<List<LinkSpan>> rowLinks)
		{
			lock (_selectionLock) { _cachedRowLinks = rowLinks; }
		}

		/// <summary>
		/// Records each rendered row's absolute screen Y and the active clip rectangle so focus-gain can pick
		/// the first VISIBLE link (keyboard nav, R3). Call inside PaintDOM alongside the link/selection caches.
		/// </summary>
		private void UpdateLinkVisibilityCache(int rowCount, int startY, int clipTop, int clipBottom)
		{
			lock (_selectionLock)
			{
				_cachedRowAbsoluteY = new int[rowCount];
				for (int i = 0; i < rowCount; i++)
					_cachedRowAbsoluteY[i] = startY + i;
				_lastClipRectY = clipTop;
				_lastClipRectBottom = clipBottom;
				_lastClipRectValid = true;
			}
		}

		/// <summary>
		/// Returns a copy of <paramref name="cellLine"/> with the focused link's cells recolored to the
		/// definite focused-link highlight colors, preserving all other cell flags (Rule A / Rule 12 —
		/// recolor only). Definite colors (not an fg/bg swap) guarantee a readable, non-transparent pair
		/// even when the link cell's background is <see cref="Color.Transparent"/>. The span is in
		/// display-column coordinates over the painted row.
		/// </summary>
		private List<Cell> ApplyFocusHighlight(List<Cell> cellLine, LinkSpan span)
		{
			int from = System.Math.Clamp(span.StartCol, 0, cellLine.Count);
			int to = System.Math.Clamp(span.EndCol, 0, cellLine.Count);
			if (to <= from) return cellLine;

			Color focusFg = _focusedLinkForegroundColor ?? DefaultFocusedLinkForeground;
			Color focusBg = _focusedLinkBackgroundColor ?? DefaultFocusedLinkBackground;

			var result = new List<Cell>(cellLine.Count);
			for (int c = 0; c < cellLine.Count; c++)
			{
				var cell = cellLine[c];
				if (c >= from && c < to)
				{
					// Definite highlight colors, keep all flags (wide-continuation, combiners, decorations).
					cell.Foreground = focusFg;
					cell.Background = focusBg;
				}
				result.Add(cell);
			}
			return result;
		}

		/// <summary>Single raise path for <see cref="LinkClicked"/>, shared by the mouse and keyboard paths.</summary>
		private void RaiseLinkClicked(string url, string text, MouseEventArgs? mouse)
		{
			LinkClicked?.Invoke(this, new LinkClickedEventArgs(url, text, mouse));
		}

		/// <summary>
		/// Sets <see cref="_focusedLinkIndex"/> to the flattened (document-order) index of the link hit by a
		/// click, so subsequent keyboard arrows continue from the clicked link.
		/// </summary>
		private void SyncFocusedIndexToHit(int hitRow, LinkSpan hitSpan)
		{
			lock (_selectionLock)
			{
				// Intentionally re-walks _cachedRowLinks inline rather than calling the lock-taking
				// FlattenLinks(): this runs inside TryRaiseLinkClick's existing _selectionLock scope, so
				// reusing the helper would churn the lock and break the atomicity of the hit + sync. Do not
				// "simplify" this into a FlattenLinks() call (it would re-enter the lock).
				int flatIndex = 0;
				for (int row = 0; row < _cachedRowLinks.Count; row++)
				{
					var spans = _cachedRowLinks[row];
					if (spans == null) continue;
					for (int s = 0; s < spans.Count; s++)
					{
						if (row == hitRow && spans[s].StartCol == hitSpan.StartCol && spans[s].EndCol == hitSpan.EndCol)
						{
							_focusedLinkIndex = flatIndex;
							return;
						}
						flatIndex++;
					}
				}
			}
		}

		/// <summary>
		/// Hit-tests <paramref name="args"/> against cached link spans; if a link is under the cursor
		/// and a subscriber exists, raises <see cref="LinkClicked"/> and returns true. Otherwise false
		/// (caller falls through to normal click handling).
		/// </summary>
		private bool TryRaiseLinkClick(MouseEventArgs args)
		{
			LinkSpan? hit;
			int hitRow = -1;
			lock (_selectionLock)
			{
				// _cachedRowLinks, _cachedRows, and _cachedRowPaintX are co-written in a single
				// PaintDOM pass, so they are always equal-length. The bounds checks below are
				// defensive (guarding against a hit-test before the first paint), not handling drift.
				if (_cachedRowLinks.Count == 0) return false;
				if (!TryHitTest(args.Position.X, args.Position.Y, out int row, out _)) return false;
				if (row < 0 || row >= _cachedRowLinks.Count) return false;
				int originX = (row < _cachedRowPaintX.Length) ? _cachedRowPaintX[row] : 0;
				hit = LinkHitTester.FindAt(_cachedRowLinks[row], originX, args.Position.X);
				hitRow = row;
			}
			if (hit == null) return false;

			// Sync the keyboard-focused link to the clicked one so subsequent arrows continue from here.
			// Done even when there is no LinkClicked subscriber (focus tracking is independent of listeners).
			SyncFocusedIndexToHit(hitRow, hit.Value);

			// Preserve existing behavior: with no subscriber, return false so the caller falls through to
			// the plain MouseClick path.
			if (LinkClicked == null) return false;

			RaiseLinkClicked(hit.Value.Url, hit.Value.Text, args);
			return true;
		}
	}
}
