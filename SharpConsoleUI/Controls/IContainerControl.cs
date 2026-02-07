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
    /// Interface for controls that contain child controls.
    /// All GUI frameworks expose children from containers - this is fundamental.
    /// Enables focus system to build flattened list of all focusable controls,
    /// including deeply nested controls within containers.
    /// </summary>
    public interface IContainerControl
    {
        /// <summary>
        /// Gets the direct child controls of this container.
        /// Does not recursively include grandchildren - recursion happens in caller.
        /// </summary>
        /// <returns>Read-only list of direct child controls (may include other containers)</returns>
        /// <remarks>
        /// IMPORTANT: For HorizontalGridControl, this should return columns AND splitters in order:
        /// [Column1, Splitter1, Column2, Splitter2, Column3]
        ///
        /// Splitters are IInteractiveControl and should be included in Tab navigation.
        /// </remarks>
        IReadOnlyList<IWindowControl> GetChildren();
    }
}
