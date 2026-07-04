// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ChatTranscriptControl
	{
		#region Message rail configuration

		private bool _messageRailEnabled = true;
		private char _messageRailGlyph = ControlDefaults.ChatMessageRailGlyph;
		private int _messageRailGutterWidth = ControlDefaults.ChatMessageRailGutterWidth;
		private Color? _messageRailColor;

		/// <summary>
		/// Gets or sets whether a message that has a footer shows a dim role-tinted left rail down its
		/// body and footer. Defaults to <c>true</c>. The rail is footer-gated: plain (no-footer) messages
		/// are unaffected.
		/// </summary>
		public bool MessageRailEnabled
		{
			get => _messageRailEnabled;
			set { if (SetProperty(ref _messageRailEnabled, value)) ReapplyAllGutters(); }
		}

		/// <summary>
		/// Gets or sets the glyph painted down a railed message's left gutter. Defaults to
		/// <c>'│'</c> (U+2502).
		/// </summary>
		public char MessageRailGlyph
		{
			get => _messageRailGlyph;
			set => SetProperty(ref _messageRailGlyph, value);
		}

		/// <summary>
		/// Gets or sets the reserved left gutter width, in columns, for a railed message (rail glyph plus
		/// gap). Defaults to <c>2</c>.
		/// </summary>
		public int MessageRailGutterWidth
		{
			get => _messageRailGutterWidth;
			set { if (SetProperty(ref _messageRailGutterWidth, value)) ReapplyAllGutters(); }
		}

		/// <summary>
		/// Gets or sets an explicit rail color. When <c>null</c> (the default), the rail derives a dimmed
		/// version of the message's role color.
		/// </summary>
		public Color? MessageRailColor
		{
			get => _messageRailColor;
			set => SetProperty(ref _messageRailColor, value);
		}

		#endregion

		#region Gutter margin

		/// <summary>
		/// Applies (or clears) the left gutter margin on a message's CHILDREN — its body
		/// <see cref="MarkupControl"/>, actions row, and status row — never the panel (so the header the
		/// panel draws stays flush at column 0). The gutter width is <see cref="MessageRailGutterWidth"/>
		/// when the rail is enabled AND the message has a footer, otherwise <c>0</c>.
		/// </summary>
		/// <remarks>
		/// Recomputes from <see cref="MessageEntry.HasFooter"/> on every call, so calling it after any
		/// footer mutation self-corrects (footer gone → 0).
		/// </remarks>
		private void ApplyGutter(MessageEntry entry)
		{
			int g = (_messageRailEnabled && entry.HasFooter) ? _messageRailGutterWidth : 0;

			// Inset the CHILDREN, never the panel (the header the panel draws stays flush at col 0). The
			// collapsed peek row (when present) is part of the same bracketed unit, so it is inset too.
			if (entry.Body != null)
				entry.Body.Margin = WithLeft(entry.Body.Margin, g);
			if (entry.PeekRow != null)
				entry.PeekRow.Margin = WithLeft(entry.PeekRow.Margin, g);
			if (entry.ActionsToolbar != null)
				entry.ActionsToolbar.Margin = WithLeft(entry.ActionsToolbar.Margin, g);
			if (entry.StatusBar != null)
				entry.StatusBar.Margin = WithLeft(entry.StatusBar.Margin, g);

			// The role style gives the panel a bottom margin as the gap BETWEEN messages. But the footer and
			// peek rows are siblings that follow the panel, so that margin would fall INSIDE the message
			// (between the body/header and the footer/peek). When the message has a footer or peek, collapse
			// the panel's bottom margin to 0 so the unit is contiguous; the between-message gap is then
			// provided by the footer's own bottom spacer. Restore the role default when neither is present.
			bool hasUnit = entry.HasFooter || entry.PeekRow != null;
			int panelBottom = hasUnit ? 0 : GetRoleStyle(entry.Role).Margin.Bottom;
			entry.Panel.Margin = WithBottom(entry.Panel.Margin, panelBottom);
		}

		/// <summary>Returns a copy of <paramref name="m"/> with a new left value, preserving top/right/bottom.</summary>
		private static Margin WithLeft(Margin m, int left) => new Margin(left, m.Top, m.Right, m.Bottom);

		/// <summary>
		/// Re-applies the gutter margin to every message. Called when a rail config property that affects the
		/// gutter (<see cref="MessageRailEnabled"/> / <see cref="MessageRailGutterWidth"/>) changes at runtime,
		/// so already-added messages pick up (or drop) the inset — otherwise a disabled rail would leave stale
		/// empty gutters on existing messages.
		/// </summary>
		private void ReapplyAllGutters()
		{
			foreach (var entry in _order)
				ApplyGutter(entry);
		}

		#endregion

		#region Rail painting

		/// <summary>
		/// Paints the transcript chrome (via the base <see cref="ScrollablePanelControl"/>), then, when the
		/// message rail is enabled, overlays the dim role-tinted left rail down each footered message's
		/// body and footer. The rail is drawn AFTER the base paint (which arranges and paints the children),
		/// so it lands on top of the reserved gutter column the children were inset past.
		/// </summary>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			base.PaintDOM(buffer, bounds, clipRect, defaultFg, defaultBg);

			if (_messageRailEnabled)
				PaintMessageRails(buffer, clipRect, defaultBg);
		}

		/// <summary>
		/// Draws the vertical rail glyph in the reserved gutter column of every footered message, spanning
		/// from below the message's panel header (its body) down through the bottom of its last footer sibling
		/// row. The header row stays flush at column 0 and is excluded (the span starts one header height below
		/// the panel top). Row positions come from the transcript's own stacked child-slot layout (the same
		/// source paint and hit-testing use) rather than the children's arranged coordinates, because those
		/// children are arranged by the layout engine AFTER this paint pass runs. Each cell is clipped to
		/// <paramref name="clipRect"/> so a rail scrolled past the viewport edge is not overdrawn.
		/// </summary>
		private void PaintMessageRails(CharacterBuffer buffer, LayoutRect clipRect, Color defaultBg)
		{
			var slots = GetVisibleChildLayout(ContentViewportWidth);
			if (slots.Count == 0)
				return;

			// Map content-space rows/columns to buffer coordinates. The content origin sits at the panel's
			// arranged position plus its margin and content inset (border + padding); the vertical scroll
			// offset shifts content up. ActualX/ActualY were just set by base.PaintDOM (SetActualBounds).
			int originX = ActualX + Margin.Left + ContentInsetLeftInternal;
			int originY = ActualY + Margin.Top + ContentInsetTopInternal;
			int scroll = VerticalScrollOffsetInternal;
			int railX = originX; // column 0 of the content area is the reserved gutter (children inset past it)

			// The rail must stay inside the SCROLLBAR-REDUCED content viewport so a partially-scrolled
			// message never bleeds onto the panel chrome (border / horizontal-scrollbar row) or past the
			// content box. ContentViewportHeight/Width are already reduced for any reserved scrollbar; the
			// clip rect handles the outer window bounds. We honor BOTH.
			int contentTop = originY;
			int contentBottom = originY + ContentViewportHeight;
			int contentRight = originX + ContentViewportWidth;

			if (railX < originX || railX >= contentRight)
				return;
			if (railX < clipRect.X || railX >= clipRect.X + clipRect.Width)
				return;

			foreach (var entry in _order)
			{
				if (!entry.HasFooter)
					continue;

				// Find this message's panel slot and the bottom of its last footer sibling row. A slot's Height
				// includes the control's own margins, so subtract each footer row's bottom margin — the footer
				// spacer (Margin.Bottom on the bottommost row) is an empty gap BELOW the message and must not
				// be railed, or the rail would leak into the blank line before the next message.
				int panelTop = int.MaxValue, headerHeight = 0, bottom = int.MinValue;
				foreach (var slot in slots)
				{
					if (ReferenceEquals(slot.Control, entry.Panel))
					{
						panelTop = slot.Top;
						headerHeight = entry.Panel.HeaderHeight;
						int b = slot.Top + slot.Height;
						if (b > bottom) bottom = b;
					}
					else if (ReferenceEquals(slot.Control, entry.PeekRow)
						|| ReferenceEquals(slot.Control, entry.ActionsToolbar)
						|| ReferenceEquals(slot.Control, entry.StatusBar))
					{
						// Peek row + footer rows are all part of the bracketed unit. Subtract the row's own
						// bottom margin (the footer spacer) so the rail doesn't leak into the blank gap below.
						int marginBottom = (slot.Control as BaseControl)?.Margin.Bottom ?? 0;
						int b = slot.Top + slot.Height - marginBottom;
						if (b > bottom) bottom = b;
					}
				}
				if (panelTop == int.MaxValue)
					continue;

				int top = panelTop + headerHeight; // skip the header row(s); the header stays flush at col 0
				if (bottom <= top)
					continue;

				Color color = ResolveRailColor(entry, defaultBg);

				for (int row = top; row < bottom; row++)
				{
					int y = originY + (row - scroll);
					if (y < contentTop || y >= contentBottom)
						continue; // outside the scrollbar-reduced content viewport (partial scroll / chrome)
					if (y < clipRect.Y || y >= clipRect.Y + clipRect.Height)
						continue; // outside the outer window clip
					buffer.SetNarrowCell(railX, y, _messageRailGlyph, color, defaultBg);
				}
			}
		}

		/// <summary>
		/// Resolves the rail color for a message: the explicit <see cref="MessageRailColor"/> when set, else a
		/// dimmed version of the message's role border color (blended ~50% toward the background). When the
		/// role has no accent (<see cref="Themes.ColorRole.Default"/> → null border) a neutral dim grey is used.
		/// </summary>
		private Color ResolveRailColor(MessageEntry entry, Color defaultBg)
		{
			if (_messageRailColor.HasValue)
				return _messageRailColor.Value;

			var role = GetRoleStyle(entry.Role).ColorRole;
			Color? roleColor = ColorResolver.ColorRoleBorder(role, Container, outline: false);
			Color baseColor = roleColor ?? RailNeutralFallback;
			// Dim the role color ~50% toward the background so the rail reads as a subtle bracket.
			return Color.Blend(baseColor.WithAlpha(ControlDefaults.ChatMessageRailDimAlpha), defaultBg);
		}

		/// <summary>
		/// The dim, theme-derived role color used for a message's rail and its footer separator line, resolved
		/// against the control's effective background (usable outside the paint pass — e.g. when configuring the
		/// footer rows' above-line color).
		/// </summary>
		private Color ResolveRailColor(MessageEntry entry)
			=> ResolveRailColor(entry, ColorResolver.ResolveBackground(null, Container));

		/// <summary>Neutral dim rail color used when the role has no accent (ColorRole.Default → null border).</summary>
		private static readonly Color RailNeutralFallback = new Color(150, 150, 150);

		#endregion

		#region Gutter test seams

		/// <summary>Returns the left margin currently applied to the message's body (0 when it has none) (test-only seam).</summary>
		internal int BodyLeftMarginForTest(ChatMessageId id) => Require(id).Body?.Margin.Left ?? 0;

		/// <summary>Returns the left margin of the message's <see cref="CollapsiblePanel"/> — must stay 0 so the header is flush (test-only seam).</summary>
		internal int PanelLeftMarginForTest(ChatMessageId id) => Require(id).Panel.Margin.Left;

		/// <summary>Returns the bottom margin of the message's <see cref="CollapsiblePanel"/> — 0 when it has a footer/peek so the unit is contiguous (test-only seam).</summary>
		internal int PanelBottomMarginForTest(ChatMessageId id) => Require(id).Panel.Margin.Bottom;

		#endregion
	}
}
