// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
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
        /// Hit-tests <paramref name="args"/> against cached link spans; if a link is under the cursor
        /// and a subscriber exists, raises <see cref="LinkClicked"/> and returns true. Otherwise false
        /// (caller falls through to normal click handling).
        /// </summary>
        private bool TryRaiseLinkClick(MouseEventArgs args)
        {
            var handler = LinkClicked;
            if (handler == null) return false;

            LinkSpan? hit;
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
            }
            if (hit == null) return false;

            handler.Invoke(this, new LinkClickedEventArgs(hit.Value.Url, hit.Value.Text, args));
            return true;
        }
    }
}
