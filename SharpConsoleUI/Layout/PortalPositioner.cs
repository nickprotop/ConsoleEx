// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Preferred placement direction for a portal overlay relative to its anchor.
	/// </summary>
	public enum PortalPlacement
	{
		/// <summary>Open below the anchor.</summary>
		Below,

		/// <summary>Open above the anchor.</summary>
		Above,

		/// <summary>Open to the right of the anchor.</summary>
		Right,

		/// <summary>Open to the left of the anchor.</summary>
		Left,

		/// <summary>Try below first, flip to above if insufficient space.</summary>
		BelowOrAbove,

		/// <summary>Try above first, flip to below if insufficient space.</summary>
		AboveOrBelow,

		/// <summary>Try right first, flip to left if insufficient space.</summary>
		RightOrLeft,

		/// <summary>Try left first, flip to right if insufficient space.</summary>
		LeftOrRight
	}

	/// <summary>
	/// Describes a portal positioning request.
	/// </summary>
	/// <param name="Anchor">The anchor rectangle (e.g. header bounds of dropdown, or menu item bounds).</param>
	/// <param name="ContentSize">The desired size of the portal content.</param>
	/// <param name="ScreenBounds">The available screen/window area to position within.</param>
	/// <param name="Placement">Preferred placement direction.</param>
	/// <param name="AlignmentOffset">Optional horizontal/vertical offset for alignment (e.g. centering dropdown under header).</param>
	public readonly record struct PortalPositionRequest(
		Rectangle Anchor,
		Size ContentSize,
		Rectangle ScreenBounds,
		PortalPlacement Placement,
		int AlignmentOffset = 0
	);

	/// <summary>
	/// The calculated portal position after applying placement and clamping logic.
	/// </summary>
	/// <param name="Bounds">The final positioned rectangle for the portal content.</param>
	/// <param name="ActualPlacement">The actual placement used (may differ from requested if flipped).</param>
	/// <param name="WasClamped">True if the bounds were clamped to fit within screen bounds.</param>
	public readonly record struct PortalPositionResult(
		Rectangle Bounds,
		PortalPlacement ActualPlacement,
		bool WasClamped
	);

	/// <summary>
	/// Static utility for calculating portal overlay positions with automatic flip
	/// and clamping logic. Extracts the shared positioning pattern from DropdownControl
	/// and MenuControl into a reusable component.
	/// </summary>
	public static class PortalPositioner
	{
		/// <summary>
		/// Calculates the optimal position for a portal overlay.
		/// </summary>
		/// <param name="request">The positioning request describing anchor, size, and constraints.</param>
		/// <returns>The calculated position result.</returns>
		public static PortalPositionResult Calculate(PortalPositionRequest request)
		{
			var (anchor, contentSize, screen, placement, alignOffset) = request;

			// Resolve composite placements (e.g. BelowOrAbove → Below or Above)
			var resolvedPlacement = ResolvePlacement(placement, anchor, contentSize, screen);

			// Calculate position based on resolved placement
			int x, y;

			switch (resolvedPlacement)
			{
				case PortalPlacement.Below:
					x = anchor.X + alignOffset;
					y = anchor.Bottom;
					break;

				case PortalPlacement.Above:
					x = anchor.X + alignOffset;
					y = anchor.Y - contentSize.Height;
					break;

				case PortalPlacement.Right:
					x = anchor.Right;
					y = anchor.Y + alignOffset;
					break;

				case PortalPlacement.Left:
					x = anchor.X - contentSize.Width;
					y = anchor.Y + alignOffset;
					break;

				default:
					x = anchor.X + alignOffset;
					y = anchor.Bottom;
					break;
			}

			// Clamp to screen bounds
			bool wasClamped = false;
			int width = contentSize.Width;
			int height = contentSize.Height;

			// Clamp horizontal
			if (x + width > screen.Right)
			{
				x = Math.Max(screen.X, screen.Right - width);
				wasClamped = true;
			}
			if (x < screen.X)
			{
				x = screen.X;
				wasClamped = true;
			}

			// Clamp vertical
			if (y + height > screen.Bottom)
			{
				height = screen.Bottom - y;
				wasClamped = true;
			}
			if (y < screen.Y)
			{
				int overflow = screen.Y - y;
				height -= overflow;
				y = screen.Y;
				wasClamped = true;
			}

			// Ensure non-negative dimensions
			width = Math.Max(0, width);
			height = Math.Max(0, height);

			return new PortalPositionResult(
				new Rectangle(x, y, width, height),
				resolvedPlacement,
				wasClamped
			);
		}

		/// <summary>
		/// Calculates the optimal position for a portal anchored at a single point
		/// (e.g. a cursor position) rather than a rectangle.
		/// </summary>
		/// <param name="anchor">The point anchor (e.g. cursor position).</param>
		/// <param name="contentSize">The desired size of the portal content.</param>
		/// <param name="screenBounds">The available screen/window area to position within.</param>
		/// <param name="placement">Preferred placement direction (default: BelowOrAbove).</param>
		/// <param name="minSize">Minimum dimensions to enforce on the result.</param>
		/// <returns>The calculated position result.</returns>
		public static PortalPositionResult CalculateFromPoint(
			Point anchor, Size contentSize, Rectangle screenBounds,
			PortalPlacement placement = PortalPlacement.BelowOrAbove,
			Size minSize = default)
		{
			// Point anchor → zero-width, 1-height rect (cursor occupies 1 row)
			var anchorRect = new Rectangle(anchor.X, anchor.Y, 0, 1);
			var result = Calculate(new PortalPositionRequest(anchorRect, contentSize, screenBounds, placement));

			// Enforce minimum dimensions
			if (minSize.Width > 0 || minSize.Height > 0)
			{
				var b = result.Bounds;
				int w = Math.Max(b.Width, minSize.Width);
				int h = Math.Max(b.Height, minSize.Height);
				if (w != b.Width || h != b.Height)
					return new PortalPositionResult(
						new Rectangle(b.X, b.Y, w, h), result.ActualPlacement, result.WasClamped);
			}
			return result;
		}

		/// <summary>
		/// Resolves a composite placement (e.g. BelowOrAbove) into a concrete direction
		/// based on available space.
		/// </summary>
		private static PortalPlacement ResolvePlacement(
			PortalPlacement placement, Rectangle anchor, Size contentSize, Rectangle screen)
		{
			switch (placement)
			{
				case PortalPlacement.BelowOrAbove:
				{
					int spaceBelow = screen.Bottom - anchor.Bottom;
					int spaceAbove = anchor.Y - screen.Y;
					return (contentSize.Height > spaceBelow && spaceAbove > spaceBelow)
						? PortalPlacement.Above
						: PortalPlacement.Below;
				}

				case PortalPlacement.AboveOrBelow:
				{
					int spaceAbove = anchor.Y - screen.Y;
					int spaceBelow = screen.Bottom - anchor.Bottom;
					return (contentSize.Height > spaceAbove && spaceBelow > spaceAbove)
						? PortalPlacement.Below
						: PortalPlacement.Above;
				}

				case PortalPlacement.RightOrLeft:
				{
					int spaceRight = screen.Right - anchor.Right;
					int spaceLeft = anchor.X - screen.X;
					return (contentSize.Width > spaceRight && spaceLeft > spaceRight)
						? PortalPlacement.Left
						: PortalPlacement.Right;
				}

				case PortalPlacement.LeftOrRight:
				{
					int spaceLeft = anchor.X - screen.X;
					int spaceRight = screen.Right - anchor.Right;
					return (contentSize.Width > spaceLeft && spaceRight > spaceLeft)
						? PortalPlacement.Right
						: PortalPlacement.Left;
				}

				default:
					return placement;
			}
		}
	}
}
