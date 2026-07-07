// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Resolves a declarative <see cref="Placement"/> into concrete, absolute window bounds computed
	/// against the live <em>usable</em> desktop (the screen area excluding the top and bottom status bars).
	/// </summary>
	/// <remarks>
	/// The service holds a lazy <see cref="Func{TResult}"/> getter for the owning
	/// <see cref="ConsoleWindowSystem"/> rather than a direct reference, matching the constructor-order-safe
	/// wiring used by the other state services. Because geometry is read at <see cref="Resolve"/> time, the
	/// same placement re-resolves correctly whenever the desktop size changes (e.g. on a terminal resize).
	/// </remarks>
	public sealed class WindowPlacementService
	{
		private readonly Func<ConsoleWindowSystem> _getSystem;

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowPlacementService"/> class.
		/// </summary>
		/// <param name="getSystem">A lazy getter returning the owning console window system.</param>
		public WindowPlacementService(Func<ConsoleWindowSystem> getSystem)
		{
			_getSystem = getSystem ?? throw new ArgumentNullException(nameof(getSystem));
		}

		/// <summary>
		/// Resolves the given <paramref name="placement"/> to absolute window bounds against the live usable desktop.
		/// </summary>
		/// <param name="placement">The declarative placement to resolve.</param>
		/// <returns>
		/// A <see cref="Rectangle"/> in absolute screen coordinates. Zone/anchor math is computed in
		/// usable-desktop space (origin 0,0) and then offset vertically by <see cref="ConsoleWindowSystem.DesktopUpperLeft"/>.Y
		/// so the result sits below the top status bar. The width and height are always at least 1 and never exceed
		/// the usable desktop, and the origin is clamped so the window stays fully on-desktop.
		/// </returns>
		/// <remarks>
		/// <para>Zone math (usable desktop <c>W</c>×<c>H</c>, remainder of odd sizes goes to the LEFT/TOP):</para>
		/// <list type="bullet">
		/// <item><description><c>Full</c> → (0, 0, W, H)</description></item>
		/// <item><description><c>LeftHalf</c> → (0, 0, W/2 + W%2, H) — the wider side on odd widths is the left</description></item>
		/// <item><description><c>RightHalf</c> → (W/2 + W%2, 0, W/2, H)</description></item>
		/// <item><description><c>TopHalf</c> → (0, 0, W, H/2 + H%2) — the taller side on odd heights is the top</description></item>
		/// <item><description><c>BottomHalf</c> → (0, H/2 + H%2, W, H/2)</description></item>
		/// <item><description>Quadrants combine the corresponding horizontal and vertical half splits.</description></item>
		/// </list>
		/// </remarks>
		public Rectangle Resolve(Placement placement)
		{
			var sys = _getSystem();
			var desk = sys.DesktopDimensions;          // usable size (status-bar-aware)
			int originY = sys.DesktopUpperLeft.Y;       // top-status offset

			int W = Math.Max(1, desk.Width);
			int H = Math.Max(1, desk.Height);

			// Odd-size split points: the wider/taller side (the remainder) goes to the LEFT/TOP.
			int leftW = W / 2 + W % 2;   // width of the left half (>= right half)
			int rightX = leftW;          // x where the right half begins
			int rightW = W / 2;          // width of the right half
			int topH = H / 2 + H % 2;    // height of the top half (>= bottom half)
			int bottomY = topH;          // y where the bottom half begins
			int bottomH = H / 2;         // height of the bottom half

			int x, y, w, h;

			switch (placement.Kind)
			{
				case PlacementKind.Snap:
					(x, y, w, h) = placement.Zone switch
					{
						SnapZone.Full => (0, 0, W, H),
						SnapZone.LeftHalf => (0, 0, leftW, H),
						SnapZone.RightHalf => (rightX, 0, rightW, H),
						SnapZone.TopHalf => (0, 0, W, topH),
						SnapZone.BottomHalf => (0, bottomY, W, bottomH),
						SnapZone.TopLeft => (0, 0, leftW, topH),
						SnapZone.TopRight => (rightX, 0, rightW, topH),
						SnapZone.BottomLeft => (0, bottomY, leftW, bottomH),
						SnapZone.BottomRight => (rightX, bottomY, rightW, bottomH),
						_ => (0, 0, W, H)
					};
					break;

				case PlacementKind.CenterPreset:
					{
						double frac = Placement.FractionFor(placement.Preset);
						w = (int)Math.Round(W * frac);
						h = (int)Math.Round(H * frac);
						(w, h) = ClampSize(w, h, W, H);
						(x, y) = CenterOrigin(w, h, W, H);
						break;
					}

				case PlacementKind.CenterExplicit:
					(w, h) = ClampSize(placement.Width, placement.Height, W, H);
					(x, y) = CenterOrigin(w, h, W, H);
					break;

				case PlacementKind.Anchor:
					(w, h) = ClampSize(placement.Width, placement.Height, W, H);
					(x, y) = AnchorOrigin(placement.AnchorValue, w, h, W, H, placement.Margin);
					break;

				case PlacementKind.Fraction:
					w = (int)Math.Round(W * placement.FractionX);
					h = (int)Math.Round(H * placement.FractionY);
					(w, h) = ClampSize(w, h, W, H);
					(x, y) = AnchorOrigin(placement.AnchorValue, w, h, W, H, 0);
					break;

				default:
					(x, y, w, h) = (0, 0, W, H);
					break;
			}

			// Final safety: ensure the window stays fully within the usable desktop.
			(w, h) = ClampSize(w, h, W, H);
			x = Math.Max(0, Math.Min(x, W - w));
			y = Math.Max(0, Math.Min(y, H - h));

			return new Rectangle(x, originY + y, w, h);
		}

		/// <summary>
		/// Clamps a desired size to the usable desktop, flooring each dimension at 1.
		/// Uses <see cref="Math.Max(int, int)"/> before <see cref="Math.Min(int, int)"/> so it never
		/// invokes a min &gt; max range.
		/// </summary>
		private static (int w, int h) ClampSize(int w, int h, int W, int H)
		{
			w = Math.Max(1, Math.Min(w, W));
			h = Math.Max(1, Math.Min(h, H));
			return (w, h);
		}

		/// <summary>Computes the centered top-left origin for a window of size <paramref name="w"/>×<paramref name="h"/>.</summary>
		private static (int x, int y) CenterOrigin(int w, int h, int W, int H)
			=> ((W - w) / 2, (H - h) / 2);

		/// <summary>
		/// Computes the top-left origin for an anchored window, applying <paramref name="margin"/> from the
		/// anchored edge(s). Edge-parallel axes are centered.
		/// </summary>
		private static (int x, int y) AnchorOrigin(Anchor anchor, int w, int h, int W, int H, int margin)
		{
			int leftX = margin;
			int centerX = (W - w) / 2;
			int rightX = W - w - margin;

			int topY = margin;
			int centerY = (H - h) / 2;
			int bottomY = H - h - margin;

			return anchor switch
			{
				Anchor.Center => (centerX, centerY),
				Anchor.TopLeft => (leftX, topY),
				Anchor.Top => (centerX, topY),
				Anchor.TopRight => (rightX, topY),
				Anchor.Left => (leftX, centerY),
				Anchor.Right => (rightX, centerY),
				Anchor.BottomLeft => (leftX, bottomY),
				Anchor.Bottom => (centerX, bottomY),
				Anchor.BottomRight => (rightX, bottomY),
				_ => (centerX, centerY)
			};
		}
	}
}
