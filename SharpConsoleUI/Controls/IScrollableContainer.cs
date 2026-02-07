// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// Interface for containers that can scroll to bring children into view.
    /// Used by BringIntoFocus to notify parent containers when nested child receives focus.
    /// </summary>
    public interface IScrollableContainer
    {
        /// <summary>
        /// Scrolls the container to bring the specified child control into view.
        /// Should also show/highlight scrollbars if applicable.
        /// </summary>
        /// <param name="child">The child control to bring into view (may be deeply nested)</param>
        /// <remarks>
        /// Implementation should use child.AbsoluteBounds to calculate position,
        /// which works correctly for deeply nested children (grandchildren, etc).
        /// </remarks>
        void ScrollChildIntoView(IWindowControl child);
    }
}
