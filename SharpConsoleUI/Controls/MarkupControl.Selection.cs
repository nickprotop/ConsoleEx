// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Text;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Opt-in mouse text selection and plain-text copy for <see cref="MarkupControl"/>.
	/// Selection is expressed in display-cell coordinates over the grid produced by the last paint,
	/// so the copied text always matches what is visually highlighted. Markup tags are stripped
	/// (the rendered cells already hold the visible glyphs). Participates in the owning window's
	/// <see cref="Core.SelectionManager"/> so only one control holds a selection at a time.
	/// </summary>
	public partial class MarkupControl
	{
		// --- Opt-in + colors ---
		private bool _enableSelection = false;
		private Color? _selectionForegroundColor = null;
		private Color? _selectionBackgroundColor = null;

		// --- Copy shortcut configuration ---
		private bool _copyEnabled = true;
		private ConsoleKey _copyKey = ConsoleKey.C;
		private ConsoleModifiers _copyModifiers = ConsoleModifiers.Control;

		// Default selection colors (used when not explicitly set). Mirrors a typical highlight pair.
		private static readonly Color DefaultSelectionForeground = Color.Black;
		private static readonly Color DefaultSelectionBackground = new Color(80, 140, 220);

		// --- Selection state (display-cell coordinates over the cached grid) ---
		private bool _hasSelection = false;
		private int _selAnchorRow = 0;
		private int _selAnchorCol = 0;
		private int _selEndRow = 0;
		private int _selEndCol = 0;
		private bool _isDragging = false;

		// --- Cached layout from the last PaintDOM (for hit-testing) ---
		private readonly object _selectionLock = new();
		private List<List<Cell>> _cachedRows = new();
		private List<int> _cachedRowSourceLine = new();
		private int[] _cachedRowPaintX = System.Array.Empty<int>();
		private int _cachedOriginY = 0;

		/// <summary>
		/// Gets or sets whether mouse text selection (and Ctrl+C copy at the window level) is enabled.
		/// Default: <c>false</c> — when disabled the control behaves exactly as a display-only markup control.
		/// </summary>
		public bool EnableSelection
		{
			get => _enableSelection;
			set
			{
				if (_enableSelection == value) return;
				_enableSelection = value;
				if (!value) ClearSelection();
				OnPropertyChanged();
				Container?.Invalidate(true);
			}
		}

		/// <summary>Gets or sets the foreground color used for selected text. Null uses a default highlight color.</summary>
		public Color? SelectionForegroundColor
		{
			get => _selectionForegroundColor;
			set => SetProperty(ref _selectionForegroundColor, value);
		}

		/// <summary>Gets or sets the background color used for selected text. Null uses a default highlight color.</summary>
		public Color? SelectionBackgroundColor
		{
			get => _selectionBackgroundColor;
			set => SetProperty(ref _selectionBackgroundColor, value);
		}

		#region ICopyableControl

		/// <summary>
		/// Gets or sets whether the keyboard copy shortcut (default Ctrl+C) is enabled. Default: <c>true</c>.
		/// Programmatic copy via <see cref="CopyToClipboard"/> / <see cref="CopySelectionToClipboard"/>
		/// is unaffected by this setting.
		/// </summary>
		public bool CopyEnabled
		{
			get => _copyEnabled;
			set { _copyEnabled = value; OnPropertyChanged(); }
		}

		/// <summary>Gets or sets the key that triggers a copy. Default: <see cref="ConsoleKey.C"/>.</summary>
		public ConsoleKey CopyKey
		{
			get => _copyKey;
			set { _copyKey = value; OnPropertyChanged(); }
		}

		/// <summary>Gets or sets the modifier keys required for the copy shortcut. Default: <see cref="ConsoleModifiers.Control"/>.</summary>
		public ConsoleModifiers CopyModifiers
		{
			get => _copyModifiers;
			set { _copyModifiers = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// Copies the current selection (plain text, markup stripped) to the clipboard.
		/// Returns <c>true</c> if something was copied.
		/// </summary>
		public bool CopySelectionToClipboard()
		{
			if (!_hasSelection) return false;
			var text = GetSelectedText();
			if (string.IsNullOrEmpty(text)) return false;
			Helpers.ClipboardHelper.SetText(text);
			return true;
		}

		/// <summary>
		/// Copies the control's entire content (plain text, markup stripped) to the clipboard,
		/// regardless of the current selection. Returns <c>true</c> if something was copied.
		/// </summary>
		public bool CopyToClipboard()
		{
			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }
			var plain = string.Join("\n", snapshot.Select(line => Parsing.MarkupParser.Remove(line)));
			if (string.IsNullOrEmpty(plain)) return false;
			Helpers.ClipboardHelper.SetText(plain);
			return true;
		}

		#endregion

		#region ISelectableControl

		/// <summary>Gets whether this control currently has a non-empty text selection.</summary>
		public bool HasSelection => _hasSelection;

		/// <summary>
		/// Occurs when the selection changes. Argument is the selected plain text, or empty when cleared.
		/// </summary>
		public event EventHandler<string>? SelectionChanged;

		/// <summary>
		/// Occurs when the selection changes, carrying both the selection state and the selected text.
		/// Richer companion to <see cref="SelectionChanged"/>; both fire together.
		/// </summary>
		public event EventHandler<TextSelectionChangedEventArgs>? TextSelectionChanged;

		/// <summary>Raises both selection-changed events with the current state.</summary>
		private void RaiseSelectionChanged(string selectedText)
		{
			SelectionChanged?.Invoke(this, selectedText);
			TextSelectionChanged?.Invoke(this, new TextSelectionChangedEventArgs(selectedText.Length > 0, selectedText));
		}

		/// <summary>
		/// Gets the selected text as plain text (markup stripped). Rows belonging to the same logical
		/// (source) line — i.e. produced by soft-wrapping — are joined without a line break; a newline
		/// is inserted only at genuine logical-line boundaries.
		/// </summary>
		public string GetSelectedText()
		{
			lock (_selectionLock)
			{
				if (!_hasSelection || _cachedRows.Count == 0)
					return string.Empty;

				var (startRow, startCol, endRow, endCol) = GetOrderedSelectionBounds();
				startRow = Math.Clamp(startRow, 0, _cachedRows.Count - 1);
				endRow = Math.Clamp(endRow, 0, _cachedRows.Count - 1);

				var sb = new StringBuilder();
				int? previousSourceLine = null;

				for (int row = startRow; row <= endRow; row++)
				{
					var cells = _cachedRows[row];
					int from = (row == startRow) ? startCol : 0;
					int to = (row == endRow) ? endCol : cells.Count;
					from = Math.Clamp(from, 0, cells.Count);
					to = Math.Clamp(to, 0, cells.Count);

					int sourceLine = (row < _cachedRowSourceLine.Count) ? _cachedRowSourceLine[row] : row;
					if (previousSourceLine != null)
					{
						// New logical line → real newline; same logical line (soft wrap) → no break.
						if (sourceLine != previousSourceLine.Value)
							sb.Append('\n');
					}
					previousSourceLine = sourceLine;

					for (int c = from; c < to; c++)
					{
						var cell = cells[c];
						if (cell.IsWideContinuation) continue;
						sb.Append(cell.Character.ToString());
					}
				}

				return sb.ToString();
			}
		}

		#region IDragAutoScrollTarget

		private int _lastDragRelativeY = 0;

		/// <inheritdoc/>
		public bool IsDragSelecting => _isDragging;

		/// <inheritdoc/>
		public bool IsViewportReady
		{
			get { lock (_selectionLock) { return _lastClipRectValid; } }
		}

		/// <inheritdoc/>
		public int LastDragRelativeY => _lastDragRelativeY;

		/// <inheritdoc/>
		public int ViewportHeightRows
		{
			get { lock (_selectionLock) { return _lastClipRectValid ? _lastClipRectBottom - _lastClipRectY : 0; } }
		}

		/// <inheritdoc/>
		public void AutoScrollStep(int rows)
		{
			if (rows == 0) return;

			// Resolve the nearest scrollable ancestor AND the direct child of that scroller on our
			// path (same idiom as MarkupControl.Keyboard.cs). The SPC scrolls by its DIRECT child,
			// which may differ from `this` when nested in a transparent container.
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

			if (scroller != null && directChild != null)
			{
				int revealRow = Math.Max(0, _selEndRow + rows);
				scroller.ScrollChildRegionIntoView(directChild, revealRow, 1);
				return;
			}

			(this as IWindowControl)?.GetParentWindow()?.ScrollBy(rows);
		}

		/// <inheritdoc/>
		public void ExtendSelectionToRevealedEdge(int direction)
		{
			lock (_selectionLock)
			{
				if (_cachedRows.Count == 0) return;
				int next = Math.Clamp(_selEndRow + direction, 0, _cachedRows.Count - 1);
				_selEndRow = next;
				_selEndCol = _cachedRows[next].Count;
				_hasSelection = true;
			}
			NotifySelectionActive();
			Container?.Invalidate(true);
		}

		#endregion

		/// <summary>Clears the current selection. Does not notify the window manager (re-entrancy guard).</summary>
		public void ClearSelection()
		{
			bool had;
			lock (_selectionLock)
			{
				had = _hasSelection;
				_hasSelection = false;
				_isDragging = false;
			}
			Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
			if (had)
			{
				RaiseSelectionChanged(string.Empty);
				Container?.Invalidate(true);
			}
		}

		#endregion

		/// <summary>
		/// Registers this control as the window's active selection (clearing any other control's
		/// selection) and raises <see cref="SelectionChanged"/> with the current text.
		/// </summary>
		private void NotifySelectionActive()
		{
			if (!_hasSelection) return;
			this.GetParentWindow()?.SelectionManager.SetActiveSelection(this);
			RaiseSelectionChanged(GetSelectedText());
		}

		#region Layout cache (populated by PaintDOM)

		/// <summary>Stores the rendered grid + paint origin from PaintDOM so mouse events can hit-test.</summary>
		private void UpdateSelectionLayoutCache(List<List<Cell>> rows, List<int> rowSourceLine, int originX, int originY, int contentWidth)
		{
			lock (_selectionLock)
			{
				_cachedRows = rows;
				_cachedRowSourceLine = rowSourceLine;
				_cachedRowPaintX = new int[rows.Count];
				for (int i = 0; i < rows.Count; i++)
					_cachedRowPaintX[i] = originX; // refined per-row via SetRowPaintOffset (alignment)
				_cachedOriginY = originY;
			}
		}

		/// <summary>Records the actual painted X offset for a row (accounts for alignment padding).</summary>
		private void SetRowPaintOffset(int rowIndex, int paintX)
		{
			lock (_selectionLock)
			{
				if (rowIndex >= 0 && rowIndex < _cachedRowPaintX.Length)
					_cachedRowPaintX[rowIndex] = paintX;
			}
		}

		/// <summary>
		/// Returns a copy of <paramref name="cellLine"/> with selected cells recolored to the selection
		/// colors, or the original list when nothing on this row is selected. Preserves cell flags.
		/// </summary>
		private List<Cell> ApplySelectionHighlight(int rowIndex, List<Cell> cellLine)
		{
			if (!_enableSelection || !_hasSelection)
				return cellLine;

			var (startRow, startCol, endRow, endCol) = GetOrderedSelectionBounds();
			if (rowIndex < startRow || rowIndex > endRow)
				return cellLine;

			int from = (rowIndex == startRow) ? startCol : 0;
			int to = (rowIndex == endRow) ? endCol : cellLine.Count;
			from = Math.Clamp(from, 0, cellLine.Count);
			to = Math.Clamp(to, 0, cellLine.Count);
			if (to <= from)
				return cellLine;

			Color selFg = _selectionForegroundColor ?? DefaultSelectionForeground;
			Color selBg = _selectionBackgroundColor ?? DefaultSelectionBackground;

			var result = new List<Cell>(cellLine.Count);
			for (int c = 0; c < cellLine.Count; c++)
			{
				var cell = cellLine[c];
				if (c >= from && c < to)
				{
					// Preserve all flags (Rule A), swap only the colors.
					cell.Foreground = selFg;
					cell.Background = selBg;
				}
				result.Add(cell);
			}
			return result;
		}

		#endregion

		#region Mouse selection

		/// <summary>
		/// Handles selection-related mouse events. Returns true via <paramref name="handled"/> ownership:
		/// the method result indicates whether the event was a selection event (and thus consumed here).
		/// </summary>
		private bool TryProcessSelectionMouse(MouseEventArgs args, out bool handled)
		{
			handled = false;

			// Right-click is surfaced to the app (e.g. to show a context menu) — not consumed here.
			if (args.HasFlag(MouseFlags.Button3Clicked))
				return false;

			// Triple-click: select the whole display row.
			if (args.HasFlag(MouseFlags.Button1TripleClicked))
			{
				if (TryHitTest(args.Position.X, args.Position.Y, out int row, out _))
				{
					lock (_selectionLock)
					{
						int count = (row < _cachedRows.Count) ? _cachedRows[row].Count : 0;
						_selAnchorRow = row; _selAnchorCol = 0;
						_selEndRow = row; _selEndCol = count;
						_hasSelection = count > 0;
						_isDragging = false;
					}
					Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
					NotifySelectionActive();
					Container?.Invalidate(true);
				}
				handled = true;
				return true;
			}

			// Double-click: select the word under the cursor.
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				if (TryHitTest(args.Position.X, args.Position.Y, out int row, out int col))
				{
					SelectWordAt(row, col);
					Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
					NotifySelectionActive();
					Container?.Invalidate(true);
				}
				MouseDoubleClick?.Invoke(this, args);
				handled = true;
				return true;
			}

			// Drag: extend the selection. Checked before Button1Pressed because SGR mouse format
			// emits Button1Pressed|Button1Dragged together for motion-while-held.
			if (args.HasFlag(MouseFlags.Button1Dragged) && _isDragging)
			{
				// Store the drag Y in CLIP-RELATIVE rows (relative to the visible viewport top), so
				// autoscroll edge detection is host-agnostic. args.Position.Y is control-relative in
				// content space; for a control hosted directly in a SCROLLED window the control's
				// AbsoluteBounds.Y (ActualY) is shifted by -ScrollOffset, so the raw value is offset by
				// the scroll amount. Converting to absolute (+ActualY +Margin.Top) then subtracting the
				// cached clip top (_lastClipRectY) yields a value measured from the visible viewport
				// top. For SPC-hosted controls ActualY == clip top, so this is a no-op there. (#45-adjacent)
				lock (_selectionLock)
				{
					_lastDragRelativeY = args.Position.Y + ActualY + Margin.Top - _lastClipRectY;
				}
				ExtendSelectionTo(args.Position.X, args.Position.Y);
				NotifySelectionActive();
				Container?.Invalidate(true);
				handled = true;
				return true;
			}

			// Press: anchor a new selection, or extend as an SGR drag continuation.
			if (args.HasFlag(MouseFlags.Button1Pressed))
			{
				if (_isDragging)
				{
					ExtendSelectionTo(args.Position.X, args.Position.Y);
				}
				else if (TryHitTest(args.Position.X, args.Position.Y, out int row, out int col))
				{
					lock (_selectionLock)
					{
						_selAnchorRow = row; _selAnchorCol = col;
						_selEndRow = row; _selEndCol = col;
						_hasSelection = false; // not a selection until the drag moves
						_isDragging = true;
					}
					Container?.GetConsoleWindowSystem?.RegisterDragAutoScroll(this);
				}
				NotifySelectionActive();
				Container?.Invalidate(true);
				handled = true;
				return true;
			}

			// Release / click: finish the drag. A bare click (no movement) clears the selection.
			if (args.HasAnyFlag(MouseFlags.Button1Released, MouseFlags.Button1Clicked))
			{
				bool wasSelecting;
				lock (_selectionLock)
				{
					wasSelecting = _hasSelection;
					_isDragging = false;
				}
				Container?.GetConsoleWindowSystem?.UnregisterDragAutoScroll(this);
				if (!wasSelecting)
				{
					ClearSelection();
					// Link click takes priority; only surface a plain click if no link was hit.
					if (!TryRaiseLinkClick(args))
						MouseClick?.Invoke(this, args);
				}
				handled = true;
				return true;
			}

			// Not a selection event — let the standard handler run (enter/leave/move, etc.).
			return false;
		}

		private bool TryHitTest(int mouseX, int mouseY, out int row, out int col)
		{
			lock (_selectionLock)
			{
				row = 0; col = 0;
				if (_cachedRows.Count == 0) return false;

				row = Math.Clamp(mouseY - _cachedOriginY, 0, _cachedRows.Count - 1);
				int originX = (row < _cachedRowPaintX.Length) ? _cachedRowPaintX[row] : 0;
				int rowLen = _cachedRows[row].Count;
				col = Math.Clamp(mouseX - originX, 0, rowLen);
				return true;
			}
		}

		private void ExtendSelectionTo(int mouseX, int mouseY)
		{
			if (!TryHitTest(mouseX, mouseY, out int row, out int col))
				return;
			lock (_selectionLock)
			{
				_selEndRow = row;
				_selEndCol = col;
				_hasSelection = (_selAnchorRow != _selEndRow) || (_selAnchorCol != _selEndCol);
			}
		}

		private void SelectWordAt(int row, int col)
		{
			lock (_selectionLock)
			{
				if (row < 0 || row >= _cachedRows.Count) { _hasSelection = false; return; }
				var cells = _cachedRows[row];

				// Build a plain string for this row + a map from char index -> starting cell index.
				var sb = new StringBuilder();
				var charToCell = new List<int>();
				for (int c = 0; c < cells.Count; c++)
				{
					if (cells[c].IsWideContinuation) continue;
					charToCell.Add(c);
					sb.Append(cells[c].Character.ToString());
				}
				string text = sb.ToString();
				if (text.Length == 0) { _hasSelection = false; return; }

				// Map the clicked cell column to a char index.
				int charIndex = 0;
				for (int i = 0; i < charToCell.Count; i++)
				{
					if (charToCell[i] <= col) charIndex = i; else break;
				}
				charIndex = Math.Clamp(charIndex, 0, text.Length - 1);

				var (wStart, wEnd) = WordBoundaryHelper.FindWordAt(text, charIndex);
				int startCell = (wStart < charToCell.Count) ? charToCell[wStart] : cells.Count;
				int endCell = (wEnd < charToCell.Count) ? charToCell[wEnd] : cells.Count;

				_selAnchorRow = row; _selAnchorCol = startCell;
				_selEndRow = row; _selEndCol = endCell;
				_hasSelection = endCell > startCell;
				_isDragging = false;
			}
		}

		private (int startRow, int startCol, int endRow, int endCol) GetOrderedSelectionBounds()
		{
			if (_selAnchorRow < _selEndRow || (_selAnchorRow == _selEndRow && _selAnchorCol <= _selEndCol))
				return (_selAnchorRow, _selAnchorCol, _selEndRow, _selEndCol);
			return (_selEndRow, _selEndCol, _selAnchorRow, _selAnchorCol);
		}

		#endregion
	}
}
