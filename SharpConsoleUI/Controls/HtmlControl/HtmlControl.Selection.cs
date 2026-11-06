// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Html;
// System.Drawing.Point, not the layout Point: this is a mouse position.
using Point = System.Drawing.Point;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Dragging across the page to select text, and copying it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The control renders real pages, and until now there was no way to take a
	/// paragraph, a URL or a snippet of code out of one. Selection here follows
	/// <see cref="MarkupControl"/> so a reader does not have to learn two idioms
	/// in one application.
	/// </para>
	/// <para>
	/// Copied text is what the document said, not what the screen showed: a
	/// paragraph wrapped across a dozen screen lines comes back as one. See
	/// <see cref="HtmlTextExtractor"/>.
	/// </para>
	/// </remarks>
	public partial class HtmlControl : ISelectableControl
	{
		private static readonly Color DefaultSelectionForeground = Color.Black;
		private static readonly Color DefaultSelectionBackground = new Color(80, 140, 220);

		private bool _enableSelection = true;
		private Color? _selectionForegroundColor;
		private Color? _selectionBackgroundColor;

		private bool _copyEnabled = true;
		private ConsoleKey _copyKey = ConsoleKey.C;
		private ConsoleModifiers _copyModifiers = ConsoleModifiers.Control;

		// Anchor is where the press landed; end is where the pointer is now.
		// Either may precede the other, so they are ordered when read.
		private int _selAnchorLine = -1;
		private int _selAnchorColumn;
		private int _selEndLine = -1;
		private int _selEndColumn;

		// A press anchors without selecting: not a selection until the drag moves,
		// which is what lets a press that never moves stay a click.
		private bool _selecting;
		private bool _dragMoved;

		/// <summary>
		/// Whether dragging with the mouse selects text. Default <c>true</c>.
		/// </summary>
		/// <remarks>
		/// Differs from <see cref="MarkupControl"/>, which is opt-in. This control
		/// is a reading surface whose whole purpose is displaying prose, and a
		/// reader who cannot copy from it will conclude the feature is missing
		/// rather than look for a switch.
		/// <para>
		/// Set to <c>false</c> to restore the mouse handling exactly as it was
		/// before selection existed: a press is not consumed, and a click follows
		/// a link as it always did.
		/// </para>
		/// </remarks>
		public bool EnableSelection
		{
			get => _enableSelection;
			set
			{
				if (_enableSelection == value) return;
				_enableSelection = value;
				if (!value) ClearSelection();
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>Foreground for selected cells. Null uses the built-in default.</summary>
		public Color? SelectionForegroundColor
		{
			get => _selectionForegroundColor;
			set
			{
				if (_selectionForegroundColor == value) return;
				_selectionForegroundColor = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>Background for selected cells. Null uses the built-in default.</summary>
		public Color? SelectionBackgroundColor
		{
			get => _selectionBackgroundColor;
			set
			{
				if (_selectionBackgroundColor == value) return;
				_selectionBackgroundColor = value;
				OnPropertyChanged();
				Invalidate(Invalidation.Repaint);
			}
		}

		/// <summary>Whether the copy key copies the selection. Default <c>true</c>.</summary>
		public bool CopyEnabled
		{
			get => _copyEnabled;
			set { if (_copyEnabled != value) { _copyEnabled = value; OnPropertyChanged(); } }
		}

		/// <summary>The key that copies. Default <see cref="ConsoleKey.C"/>.</summary>
		public ConsoleKey CopyKey
		{
			get => _copyKey;
			set { if (_copyKey != value) { _copyKey = value; OnPropertyChanged(); } }
		}

		/// <summary>Modifiers for the copy key. Default <see cref="ConsoleModifiers.Control"/>.</summary>
		public ConsoleModifiers CopyModifiers
		{
			get => _copyModifiers;
			set { if (_copyModifiers != value) { _copyModifiers = value; OnPropertyChanged(); } }
		}

		/// <inheritdoc/>
		public event EventHandler<string>? SelectionChanged;

		/// <inheritdoc/>
		public bool HasSelection =>
			_selAnchorLine >= 0
			&& (_selAnchorLine != _selEndLine || _selAnchorColumn != _selEndColumn);

		/// <inheritdoc/>
		public string GetSelectedText()
		{
			if (!HasSelection) return string.Empty;

			Order(out int firstLine, out int firstColumn, out int lastLine, out int lastColumn);
			return HtmlTextExtractor.Extract(LayoutLines, firstLine, firstColumn, lastLine, lastColumn);
		}

		/// <inheritdoc/>
		public void ClearSelection()
		{
			if (_selAnchorLine < 0 && _selEndLine < 0) return;

			_selAnchorLine = -1;
			_selEndLine = -1;
			_selAnchorColumn = 0;
			_selEndColumn = 0;
			_selecting = false;
			_dragMoved = false;

			SelectionChanged?.Invoke(this, string.Empty);
			Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Puts the anchor and the end in document order.
		/// </summary>
		/// <remarks>
		/// An anchor is where the press landed, not where the selection starts: a
		/// drag to the left puts the end before it, and everything that reads a
		/// selection wants the two in order.
		/// </remarks>
		private void Order(out int firstLine, out int firstColumn, out int lastLine, out int lastColumn)
		{
			bool anchorFirst = _selAnchorLine < _selEndLine
				|| (_selAnchorLine == _selEndLine && _selAnchorColumn <= _selEndColumn);

			if (anchorFirst)
			{
				firstLine = _selAnchorLine;
				firstColumn = _selAnchorColumn;
				lastLine = _selEndLine;
				lastColumn = _selEndColumn;
			}
			else
			{
				firstLine = _selEndLine;
				firstColumn = _selEndColumn;
				lastLine = _selAnchorLine;
				lastColumn = _selAnchorColumn;
			}
		}

		/// <summary>
		/// Whether a cell on a line is inside the current selection.
		/// </summary>
		internal bool IsCellSelected(int lineIndex, int column)
		{
			if (!HasSelection) return false;

			Order(out int firstLine, out int firstColumn, out int lastLine, out int lastColumn);

			if (lineIndex < firstLine || lineIndex > lastLine) return false;
			if (lineIndex == firstLine && column < firstColumn) return false;
			if (lineIndex == lastLine && column >= lastColumn) return false;

			return true;
		}

		/// <summary>Anchors a selection where a press landed.</summary>
		private void BeginSelection(Point position, int renderWidth)
		{
			if (!TryHitTestText(position, renderWidth, out int line, out int column))
			{
				ClearSelection();
				return;
			}

			_selAnchorLine = line;
			_selAnchorColumn = column;
			_selEndLine = line;
			_selEndColumn = column;
			_selecting = true;
			_dragMoved = false;

			Invalidate(Invalidation.Repaint);
		}

		/// <summary>Extends a live selection to where the pointer is now.</summary>
		private void ExtendSelection(Point position, int renderWidth)
		{
			if (!_selecting) return;
			if (!TryHitTestText(position, renderWidth, out int line, out int column)) return;

			if (line == _selEndLine && column == _selEndColumn) return;

			_selEndLine = line;
			_selEndColumn = column;
			_dragMoved = true;

			// The window owns "which control holds the selection", and starting
			// one here clears whatever held it before. Without telling it,
			// ISelectableControl's single-owner promise is not kept and the
			// window-level copy path never sees this selection at all.
			if (HasSelection)
				this.GetParentWindow()?.SelectionManager.SetActiveSelection(this);

			SelectionChanged?.Invoke(this, GetSelectedText());
			Invalidate(Invalidation.Repaint);
		}

		/// <summary>
		/// Ends the gesture. Reports whether it was a drag rather than a click.
		/// </summary>
		/// <remarks>
		/// A press that never moved is a click, and the caller follows the link it
		/// landed on. This mirrors <c>MarkupControl</c>, which dispatches the link
		/// click from inside its own release handler rather than leaving a second
		/// handler to guess whether a selection happened.
		/// </remarks>
		private bool EndSelection()
		{
			// Keyed on whether anything is SELECTED, not on whether the pointer
			// moved. A drag out and back to its own anchor moves twice and selects
			// nothing, and treating that as a drag swallowed the release so the
			// link under it never fired.
			bool selected = _selecting && HasSelection;
			_selecting = false;

			if (!selected)
				ClearSelection();

			return selected;
		}

		/// <summary>
		/// Drops a selection whose coordinates no longer mean anything.
		/// </summary>
		/// <remarks>
		/// A selection is a pair of line indices into one layout. Replace the
		/// document or relay it out and those indices address different text, so
		/// the selection has to go rather than silently come to mean something
		/// else: it reported, highlighted and copied text the reader never chose.
		/// </remarks>
		internal void InvalidateSelectionForNewLayout() => ClearSelection();

		/// <summary>Copies the selection when the key matches.</summary>
		private bool TryCopySelection(ConsoleKeyInfo key)
		{
			if (!_copyEnabled || !HasSelection) return false;
			if (key.Key != _copyKey || key.Modifiers != _copyModifiers) return false;

			ClipboardHelper.SetText(GetSelectedText());
			return true;
		}
	}
}
