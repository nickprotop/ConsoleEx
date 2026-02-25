using System.Drawing;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Implemented by portal content controls that need custom absolute positioning.
	/// When Window.CreatePortal() receives a content that implements this interface,
	/// it uses GetPortalBounds() to position the overlay instead of defaulting to (0,0).
	/// </summary>
	public interface IHasPortalBounds
	{
		/// <summary>
		/// Returns the absolute position and size (window-relative coordinates)
		/// where this portal overlay should be rendered.
		/// </summary>
		Rectangle GetPortalBounds();

		/// <summary>
		/// When true, the portal is automatically dismissed when the user clicks outside its bounds.
		/// </summary>
		bool DismissOnOutsideClick => false;
	}
}
