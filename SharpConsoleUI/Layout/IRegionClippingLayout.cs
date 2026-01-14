namespace SharpConsoleUI.Layout;

/// <summary>
/// Interface for layout containers that support region-specific clipping.
/// This allows different clip rectangles to be applied to children based on their properties,
/// such as preventing scrollable content from painting over sticky controls.
/// </summary>
public interface IRegionClippingLayout
{
	/// <summary>
	/// Gets the paint clip rectangle for a specific child node.
	/// This allows the layout to restrict where each child can paint based on its position type.
	/// </summary>
	/// <param name="child">The child node to get the clip rectangle for.</param>
	/// <param name="parentClipRect">The parent's clip rectangle.</param>
	/// <returns>A clip rectangle that restricts where the child can paint.</returns>
	LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect);
}
