// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	public partial class ChatTranscriptControl
	{
		#region Collapsed preview (fade-peek)

		private bool _collapsedPreview = true;
		private int _collapsedPreviewFadeWidth = ControlDefaults.ChatCollapsedPreviewFadeWidth;

		/// <summary>
		/// Gets or sets whether a collapsed message shows a one-line "peek" preview row directly below its
		/// header. When <c>true</c> (the default), each collapsed message renders the first line of its hidden
		/// content — faded left→right into a dim, clickable <c>expand…</c> cue — as a sibling row that clicking
		/// expands the message. When <c>false</c>, collapsed messages show only their header.
		/// </summary>
		/// <remarks>
		/// The peek row is a real child <see cref="MarkupControl"/> inserted after the message panel; it is
		/// added and removed automatically as the panel collapses and expands. Changing this value affects
		/// messages that collapse/expand afterwards; it does not retroactively add or remove peek rows on
		/// already-collapsed messages.
		/// </remarks>
		public bool CollapsedPreview
		{
			get => _collapsedPreview;
			set => SetProperty(ref _collapsedPreview, value);
		}

		/// <summary>
		/// Gets or sets the number of trailing columns over which a collapsed-message peek row fades its
		/// foreground from opaque to transparent before the dim <c>expand…</c> cue. Defaults to
		/// <see cref="ControlDefaults.ChatCollapsedPreviewFadeWidth"/> (10). Affects peek rows built afterwards.
		/// </summary>
		public int CollapsedPreviewFadeWidth
		{
			get => _collapsedPreviewFadeWidth;
			set => SetProperty(ref _collapsedPreviewFadeWidth, value);
		}

		/// <summary>The dim base colour of the peek preview text and the <c>expand…</c> cue.</summary>
		private static readonly Color PeekDimColor = Color.Grey;

		/// <summary>The trailing click cue appended to every peek row.</summary>
		private const string PeekExpandCue = "  expand…";

		private void OnMessageExpandedChanged(MessageEntry entry, bool expanded)
		{
			if (expanded)
				RemovePeek(entry);
			else
				MaybeAddPeek(entry);
		}

		/// <summary>
		/// Adds the collapsed-preview peek row for a message if one is warranted (preview enabled, none present,
		/// and the panel is collapsed). Inserted as a sibling immediately after the message panel.
		/// </summary>
		private void MaybeAddPeek(MessageEntry entry)
		{
			if (!_collapsedPreview || entry.PeekRow != null || entry.Panel.IsExpanded)
				return;

			var peek = new MarkupControl(BuildPeekLines(entry));
			peek.MouseClick += (_, __) => entry.Panel.IsExpanded = true; // click-to-expand
			entry.PeekRow = peek;

			int panelIndex = IndexOfPanel(entry.Panel);
			if (panelIndex < 0)
			{
				// Panel not in children (defensive) — append rather than lose the row.
				AddControl(peek);
			}
			else
			{
				InsertControl(panelIndex + 1, peek);
			}

			ApplyGutter(entry);        // inset the peek + collapse the panel's bottom gap now that it has a peek
			ApplyFooterSpacer(entry);  // when there's no footer, the peek is bottommost and owns the trailing blank line
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>Removes a message's peek row if present.</summary>
		private void RemovePeek(MessageEntry entry)
		{
			if (entry.PeekRow == null)
				return;

			RemoveControl(entry.PeekRow);
			entry.PeekRow = null;
			ApplyGutter(entry);        // restore the panel's bottom margin if nothing else follows it
			ApplyFooterSpacer(entry);  // move the trailing spacer back to the footer (or nowhere) now the peek is gone
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Rebuilds the content of an existing peek row from the message's current <see cref="MessageEntry.Buffer"/>.
		/// No-op when the message has no peek row. Called after the body content changes (streaming / update) so a
		/// message collapsed while its content is still arriving shows an up-to-date preview.
		/// </summary>
		private void RefreshPeek(MessageEntry entry)
		{
			if (entry.PeekRow == null)
				return;

			entry.PeekRow.SetContent(BuildPeekLines(entry));
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Builds the single-line peek markup: the first plain-text line of the hidden content, truncated to
		/// fit, faded opaque→transparent over the trailing <see cref="CollapsedPreviewFadeWidth"/> columns, then
		/// the dim <c>expand…</c> cue.
		/// </summary>
		private List<string> BuildPeekLines(MessageEntry entry)
		{
			string first = FirstPlainLine(entry.Buffer.ToString());

			int cueWidth = UnicodeWidth.GetStringWidth(PeekExpandCue);
			int available = Math.Max(0, ContentViewportWidth - cueWidth);
			first = TruncateToWidth(first, available);

			return new List<string> { BuildFadeMarkup(first) + BuildCueMarkup() };
		}

		/// <summary>Locates the message panel in the transcript's children and returns its index, or -1.</summary>
		private int IndexOfPanel(CollapsiblePanel panel)
		{
			var children = Children; // fresh list from base SPC
			for (int i = 0; i < children.Count; i++)
			{
				if (ReferenceEquals(children[i], panel))
					return i;
			}
			return -1;
		}

		/// <summary>
		/// Returns the first line (up to the first newline) of <paramref name="content"/> stripped to plain
		/// text — markup tags removed — using the markup parser so wide/combining runes are handled correctly.
		/// </summary>
		private static string FirstPlainLine(string content)
		{
			string normalized = content.Replace("\r\n", "\n");
			int nl = normalized.IndexOf('\n');
			string line = nl >= 0 ? normalized.Substring(0, nl) : normalized;
			if (line.Length == 0)
				return string.Empty;

			// Parse to cells (which drops markup tags), then join the visible runes. Skip wide-continuation
			// cells so a wide rune contributes its single character once, not a duplicated blank.
			var cells = MarkupParser.Parse(line, PeekDimColor, Color.Transparent);
			var sb = new StringBuilder(cells.Count);
			foreach (var cell in cells)
			{
				if (cell.IsWideContinuation)
					continue;
				sb.Append(cell.Character.ToString());
			}
			return sb.ToString();
		}

		/// <summary>Truncates plain text to at most <paramref name="maxWidth"/> display columns.</summary>
		private static string TruncateToWidth(string text, int maxWidth)
		{
			if (maxWidth <= 0)
				return string.Empty;
			if (UnicodeWidth.GetStringWidth(text) <= maxWidth)
				return text;

			var sb = new StringBuilder();
			int width = 0;
			foreach (var rune in text.EnumerateRunes())
			{
				int rw = UnicodeWidth.GetRuneWidth(rune);
				if (rw == 0)
				{
					sb.Append(rune.ToString());
					continue;
				}
				if (width + rw > maxWidth)
					break;
				sb.Append(rune.ToString());
				width += rw;
			}
			return sb.ToString();
		}

		/// <summary>
		/// Wraps <paramref name="plain"/> in dim-colour markup, ramping the foreground alpha to transparent over
		/// the trailing <see cref="CollapsedPreviewFadeWidth"/> display columns (opaque → 00).
		/// </summary>
		private string BuildFadeMarkup(string plain)
		{
			if (plain.Length == 0)
				return string.Empty;

			int totalWidth = UnicodeWidth.GetStringWidth(plain);
			int fade = Math.Min(Math.Max(0, _collapsedPreviewFadeWidth), totalWidth);
			int fadeStart = totalWidth - fade; // display column where the ramp begins

			var sb = new StringBuilder();
			int col = 0; // display column of the current rune
			foreach (var rune in plain.EnumerateRunes())
			{
				string ch = MarkupEscape(rune.ToString());
				int rw = UnicodeWidth.GetRuneWidth(rune);

				byte alpha;
				if (fade == 0 || col < fadeStart)
				{
					alpha = 0xFF;
				}
				else
				{
					// Linear ramp opaque→transparent across the fade tail.
					int into = col - fadeStart; // 0 .. fade-1
					alpha = (byte)(255 - (into + 1) * 255 / fade);
				}

				sb.Append($"[#{PeekDimColor.R:X2}{PeekDimColor.G:X2}{PeekDimColor.B:X2}{alpha:X2}]{ch}[/]");
				col += Math.Max(1, rw);
			}
			return sb.ToString();
		}

		/// <summary>Builds the dim, non-fading <c>expand…</c> cue markup.</summary>
		private static string BuildCueMarkup()
		{
			return $"[#{PeekDimColor.R:X2}{PeekDimColor.G:X2}{PeekDimColor.B:X2}]{MarkupEscape(PeekExpandCue)}[/]";
		}

		private static string MarkupEscape(string text) => text.Replace("[", "[[").Replace("]", "]]");

		#endregion

		#region Peek test seams

		/// <summary>Returns the peek preview row control for the message with the given id, or <c>null</c>.</summary>
		internal MarkupControl? PeekRowForTest(ChatMessageId id) => Require(id).PeekRow;

		/// <summary>Returns the plain first-line text used to build the peek row, or <c>null</c> when no peek exists.</summary>
		internal string? PeekTextForTest(ChatMessageId id)
		{
			var entry = Require(id);
			return entry.PeekRow == null ? null : FirstPlainLine(entry.Buffer.ToString());
		}

		#endregion
	}
}
