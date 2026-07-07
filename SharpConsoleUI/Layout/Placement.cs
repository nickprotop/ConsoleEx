// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Predefined "snap" zones that tile a window against the usable desktop, mirroring the
	/// half/quadrant snapping familiar from tiling window managers.
	/// </summary>
	public enum SnapZone
	{
		/// <summary>The entire usable desktop (equivalent to maximizing).</summary>
		Full,

		/// <summary>The left half of the usable desktop.</summary>
		LeftHalf,

		/// <summary>The right half of the usable desktop.</summary>
		RightHalf,

		/// <summary>The top half of the usable desktop.</summary>
		TopHalf,

		/// <summary>The bottom half of the usable desktop.</summary>
		BottomHalf,

		/// <summary>The top-left quadrant of the usable desktop.</summary>
		TopLeft,

		/// <summary>The top-right quadrant of the usable desktop.</summary>
		TopRight,

		/// <summary>The bottom-left quadrant of the usable desktop.</summary>
		BottomLeft,

		/// <summary>The bottom-right quadrant of the usable desktop.</summary>
		BottomRight
	}

	/// <summary>
	/// The corner, edge, or center of the usable desktop that a placed window is aligned to.
	/// </summary>
	public enum Anchor
	{
		/// <summary>Centered horizontally and vertically.</summary>
		Center,

		/// <summary>Aligned to the top-left corner.</summary>
		TopLeft,

		/// <summary>Aligned to the top edge, centered horizontally.</summary>
		Top,

		/// <summary>Aligned to the top-right corner.</summary>
		TopRight,

		/// <summary>Aligned to the left edge, centered vertically.</summary>
		Left,

		/// <summary>Aligned to the right edge, centered vertically.</summary>
		Right,

		/// <summary>Aligned to the bottom-left corner.</summary>
		BottomLeft,

		/// <summary>Aligned to the bottom edge, centered horizontally.</summary>
		Bottom,

		/// <summary>Aligned to the bottom-right corner.</summary>
		BottomRight
	}

	/// <summary>
	/// Convenience window sizes expressed as fractions of the usable desktop.
	/// </summary>
	public enum SizePreset
	{
		/// <summary>Small: 40% of the usable desktop in each dimension (<see cref="Placement.SmallFraction"/>).</summary>
		Small,

		/// <summary>Medium: 60% of the usable desktop in each dimension (<see cref="Placement.MediumFraction"/>).</summary>
		Medium,

		/// <summary>Large: 85% of the usable desktop in each dimension (<see cref="Placement.LargeFraction"/>).</summary>
		Large
	}

	/// <summary>
	/// The internal discriminator describing which factory produced a <see cref="Placement"/>.
	/// </summary>
	public enum PlacementKind
	{
		/// <summary>A <see cref="SnapZone"/>-based placement (see <see cref="Placement.Snap"/>).</summary>
		Snap,

		/// <summary>A centered placement using a <see cref="SizePreset"/> (see <see cref="Placement.Center(SizePreset)"/>).</summary>
		CenterPreset,

		/// <summary>A centered placement with an explicit width and height (see <see cref="Placement.Center(int, int)"/>).</summary>
		CenterExplicit,

		/// <summary>An anchored placement with an explicit width, height, and margin (see <see cref="Placement.Anchor(Anchor, int, int, int)"/>).</summary>
		Anchor,

		/// <summary>An anchored placement sized by desktop fraction (see <see cref="Placement.Fraction"/>).</summary>
		Fraction
	}

	/// <summary>
	/// An immutable, value-comparable description of where a window should be positioned and sized
	/// relative to the live usable desktop. A <see cref="Placement"/> carries only intent; the concrete
	/// pixel/cell bounds are computed by <see cref="SharpConsoleUI.Core.WindowPlacementService.Resolve"/>
	/// against the current desktop geometry, so the same placement re-resolves correctly after a resize.
	/// </summary>
	/// <remarks>
	/// Create instances via the factory statics (<see cref="Snap"/>, <see cref="Center(SizePreset)"/>,
	/// <see cref="Center(int, int)"/>, <see cref="Anchor(Anchor, int, int, int)"/>, <see cref="Fraction"/>,
	/// or <see cref="Maximized"/>). Because this is a <c>readonly struct</c> it has structural value
	/// equality, so two placements built from identical arguments compare equal.
	/// </remarks>
	public readonly struct Placement : System.IEquatable<Placement>
	{
		/// <summary>The desktop fraction used by <see cref="SizePreset.Small"/> (40%).</summary>
		public const double SmallFraction = 0.4;

		/// <summary>The desktop fraction used by <see cref="SizePreset.Medium"/> (60%).</summary>
		public const double MediumFraction = 0.6;

		/// <summary>The desktop fraction used by <see cref="SizePreset.Large"/> (85%).</summary>
		public const double LargeFraction = 0.85;

		private Placement(PlacementKind kind, SnapZone zone, Anchor anchor, SizePreset preset,
			int width, int height, int margin, double fractionX, double fractionY)
		{
			Kind = kind;
			Zone = zone;
			AnchorValue = anchor;
			Preset = preset;
			Width = width;
			Height = height;
			Margin = margin;
			FractionX = fractionX;
			FractionY = fractionY;
		}

		/// <summary>Gets the discriminator identifying which factory produced this placement.</summary>
		public PlacementKind Kind { get; }

		/// <summary>Gets the snap zone (valid when <see cref="Kind"/> is <see cref="PlacementKind.Snap"/>).</summary>
		public SnapZone Zone { get; }

		/// <summary>Gets the anchor (valid when <see cref="Kind"/> is <see cref="PlacementKind.Anchor"/> or <see cref="PlacementKind.Fraction"/>).</summary>
		public Anchor AnchorValue { get; }

		/// <summary>Gets the size preset (valid when <see cref="Kind"/> is <see cref="PlacementKind.CenterPreset"/>).</summary>
		public SizePreset Preset { get; }

		/// <summary>Gets the explicit width in cells (valid for <see cref="PlacementKind.CenterExplicit"/> and <see cref="PlacementKind.Anchor"/>).</summary>
		public int Width { get; }

		/// <summary>Gets the explicit height in cells (valid for <see cref="PlacementKind.CenterExplicit"/> and <see cref="PlacementKind.Anchor"/>).</summary>
		public int Height { get; }

		/// <summary>Gets the margin in cells from the anchored edge (valid for <see cref="PlacementKind.Anchor"/>).</summary>
		public int Margin { get; }

		/// <summary>Gets the horizontal desktop fraction (valid when <see cref="Kind"/> is <see cref="PlacementKind.Fraction"/>).</summary>
		public double FractionX { get; }

		/// <summary>Gets the vertical desktop fraction (valid when <see cref="Kind"/> is <see cref="PlacementKind.Fraction"/>).</summary>
		public double FractionY { get; }

		/// <summary>
		/// Gets a placement that fills the entire usable desktop. Equivalent to <c>Snap(SnapZone.Full)</c>.
		/// </summary>
		public static Placement Maximized => Snap(SnapZone.Full);

		/// <summary>
		/// Creates a placement that tiles the window into the given <paramref name="zone"/> of the usable desktop.
		/// </summary>
		/// <param name="zone">The half or quadrant zone to snap into.</param>
		/// <returns>A snap placement for the specified zone.</returns>
		public static Placement Snap(SnapZone zone)
			=> new Placement(PlacementKind.Snap, zone, Layout.Anchor.Center, SizePreset.Medium, 0, 0, 0, 0, 0);

		/// <summary>
		/// Creates a centered placement sized as a fraction of the usable desktop per the given preset.
		/// </summary>
		/// <param name="preset">The size preset (<see cref="SizePreset.Small"/>, <see cref="SizePreset.Medium"/>, or <see cref="SizePreset.Large"/>).</param>
		/// <returns>A centered placement sized from the preset.</returns>
		public static Placement Center(SizePreset preset)
			=> new Placement(PlacementKind.CenterPreset, SnapZone.Full, Layout.Anchor.Center, preset, 0, 0, 0, 0, 0);

		/// <summary>
		/// Creates a centered placement with an explicit size in cells. The size is clamped to the usable desktop.
		/// </summary>
		/// <param name="width">The desired window width in cells.</param>
		/// <param name="height">The desired window height in cells.</param>
		/// <returns>A centered placement with the given size.</returns>
		public static Placement Center(int width, int height)
			=> new Placement(PlacementKind.CenterExplicit, SnapZone.Full, Layout.Anchor.Center, SizePreset.Medium, width, height, 0, 0, 0);

		/// <summary>
		/// Creates an anchored placement with an explicit size in cells and an optional margin from the anchored edge(s).
		/// </summary>
		/// <param name="anchor">The corner, edge, or center to align the window to.</param>
		/// <param name="width">The desired window width in cells.</param>
		/// <param name="height">The desired window height in cells.</param>
		/// <param name="margin">The margin in cells from the anchored edge(s). Defaults to 0.</param>
		/// <returns>An anchored placement with the given size and margin.</returns>
		public static Placement Anchor(Anchor anchor, int width, int height, int margin = 0)
			=> new Placement(PlacementKind.Anchor, SnapZone.Full, anchor, SizePreset.Medium, width, height, margin, 0, 0);

		/// <summary>
		/// Creates an anchored placement sized as a fraction of the usable desktop in each dimension.
		/// </summary>
		/// <param name="anchor">The corner, edge, or center to align the window to.</param>
		/// <param name="fractionX">The window width as a fraction (0..1) of the usable desktop width.</param>
		/// <param name="fractionY">The window height as a fraction (0..1) of the usable desktop height.</param>
		/// <returns>A fraction-sized anchored placement.</returns>
		public static Placement Fraction(Anchor anchor, double fractionX, double fractionY)
			=> new Placement(PlacementKind.Fraction, SnapZone.Full, anchor, SizePreset.Medium, 0, 0, 0, fractionX, fractionY);

		/// <summary>
		/// Gets the desktop fraction associated with a <see cref="SizePreset"/>.
		/// </summary>
		/// <param name="preset">The preset to resolve.</param>
		/// <returns>The fraction (0..1) of the usable desktop in each dimension.</returns>
		public static double FractionFor(SizePreset preset) => preset switch
		{
			SizePreset.Small => SmallFraction,
			SizePreset.Large => LargeFraction,
			_ => MediumFraction
		};

		/// <inheritdoc/>
		public bool Equals(Placement other)
			=> Kind == other.Kind
			&& Zone == other.Zone
			&& AnchorValue == other.AnchorValue
			&& Preset == other.Preset
			&& Width == other.Width
			&& Height == other.Height
			&& Margin == other.Margin
			&& FractionX.Equals(other.FractionX)
			&& FractionY.Equals(other.FractionY);

		/// <inheritdoc/>
		public override bool Equals(object? obj) => obj is Placement other && Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			var hash = new System.HashCode();
			hash.Add(Kind);
			hash.Add(Zone);
			hash.Add(AnchorValue);
			hash.Add(Preset);
			hash.Add(Width);
			hash.Add(Height);
			hash.Add(Margin);
			hash.Add(FractionX);
			hash.Add(FractionY);
			return hash.ToHashCode();
		}

		/// <summary>Determines whether two placements are equal by value.</summary>
		/// <param name="left">The first placement.</param>
		/// <param name="right">The second placement.</param>
		/// <returns><c>true</c> if the placements are equal; otherwise <c>false</c>.</returns>
		public static bool operator ==(Placement left, Placement right) => left.Equals(right);

		/// <summary>Determines whether two placements differ by value.</summary>
		/// <param name="left">The first placement.</param>
		/// <param name="right">The second placement.</param>
		/// <returns><c>true</c> if the placements differ; otherwise <c>false</c>.</returns>
		public static bool operator !=(Placement left, Placement right) => !left.Equals(right);
	}
}
