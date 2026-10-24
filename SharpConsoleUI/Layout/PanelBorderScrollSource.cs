// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Layout;

/// <summary>
/// Lets a <see cref="ScrollablePanelControl"/> drive a window-border scrollbar on one axis.
/// </summary>
/// <remarks>
/// <para>
/// A thin view over the panel, holding no state of its own. The panel remains the single source
/// of truth for its offset and extent, so the border thumb cannot drift from the content the way
/// a cached copy would.
/// </para>
/// <para>
/// Created through <see cref="ForVertical"/> / <see cref="ForHorizontal"/> and handed to
/// <see cref="Window.DelegateScrollbarToBorder"/>.
/// </para>
/// </remarks>
public sealed class PanelBorderScrollSource : IBorderScrollSource
{
	private readonly ScrollablePanelControl _panel;
	private readonly bool _vertical;

	private PanelBorderScrollSource(ScrollablePanelControl panel, bool vertical)
	{
		_panel = panel ?? throw new ArgumentNullException(nameof(panel));
		_vertical = vertical;
	}

	/// <summary>A source for the panel's vertical scrolling, for the right border.</summary>
	/// <param name="panel">The panel to read.</param>
	/// <returns>A source the window can delegate its vertical border scrollbar to.</returns>
	public static PanelBorderScrollSource ForVertical(ScrollablePanelControl panel)
		=> new(panel, vertical: true);

	/// <summary>A source for the panel's horizontal scrolling, for the bottom border.</summary>
	/// <param name="panel">The panel to read.</param>
	/// <returns>A source the window can delegate its horizontal border scrollbar to.</returns>
	public static PanelBorderScrollSource ForHorizontal(ScrollablePanelControl panel)
		=> new(panel, vertical: false);

	/// <summary>
	/// Hides the panel's own scrollbar so the border is the only one drawn, and reports what to
	/// restore on release. Delegation that left the interior bar in place would draw two and
	/// reclaim nothing, which is the whole point of delegating.
	/// </summary>
	/// <remarks>
	/// Only the VERTICAL axis has an interior bar to suppress; the panel's horizontal scrollbar
	/// shares <see cref="ScrollablePanelControl.ShowScrollbar"/> with it, so a horizontal
	/// delegation leaves the flag alone rather than silently killing the vertical bar too.
	/// </remarks>
	internal bool SuppressOwnScrollbar()
	{
		if (!_vertical)
			return false;

		bool previous = _panel.ShowScrollbar;
		_panel.ShowScrollbar = false;
		return previous;
	}

	/// <summary>Restores the panel's own scrollbar when the border is handed back.</summary>
	/// <param name="shown">The value <see cref="SuppressOwnScrollbar"/> returned.</param>
	internal void RestoreOwnScrollbar(bool shown)
	{
		if (_vertical)
			_panel.ShowScrollbar = shown;
	}

	/// <inheritdoc/>
	public int Offset => _vertical ? _panel.VerticalScrollOffset : _panel.HorizontalScrollOffset;

	/// <inheritdoc/>
	public int Extent => _vertical ? _panel.TotalContentHeight : _panel.TotalContentWidth;

	/// <inheritdoc/>
	public int Viewport => _vertical ? _panel.ContentViewportHeight : _panel.ContentViewportWidth;

	/// <inheritdoc/>
	/// <remarks>
	/// Expressed as a relative scroll because that is the panel's public verb, and it already
	/// clamps to the valid range. Going through it keeps the clamp in one place.
	/// </remarks>
	public void ScrollTo(int offset)
	{
		int delta = offset - Offset;
		if (delta == 0)
			return;

		if (_vertical)
			_panel.ScrollVerticalBy(delta);
		else
			_panel.ScrollHorizontalBy(delta);
	}
}
