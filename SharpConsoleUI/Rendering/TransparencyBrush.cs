// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Rendering
{
	/// <summary>
	/// Controls how a transparent window composites against content below it.
	/// </summary>
	public enum TransparencyStyle
	{
		/// <summary>
		/// Gaussian bg+fg blend (PerceivedCellColor) with character bubble-up
		/// and configurable power-curve fade. Richer color impression than default.
		/// </summary>
		Acrylic,

		/// <summary>
		/// Like Acrylic for background (Gaussian color impression from below)
		/// but NO character bubble-up. You see tinted color fields, not text.
		/// WinUI Mica analog.
		/// </summary>
		Mica,

		/// <summary>
		/// Simple flat overlay. Composites bg only — no fg influence,
		/// no bubble-up, no block character guard. Just a colored filter.
		/// </summary>
		Tinted,

		/// <summary>
		/// Full control via a user-provided per-cell compositing delegate.
		/// </summary>
		Custom
	}

	/// <summary>
	/// Defines the compositing style for a transparent window, overriding the default
	/// true-transparency behavior. The brush does NOT own the color or alpha — it only
	/// controls how the compositing is performed.
	/// </summary>
	public sealed record TransparencyBrush
	{
		/// <summary>
		/// The compositing style.
		/// </summary>
		public TransparencyStyle Style { get; init; }

		/// <summary>
		/// Fade exponent for character foreground (Acrylic only).
		/// Lower values = more aggressive fade. Default 0.12.
		/// </summary>
		public float FadeExponent { get; init; } = 0.12f;

		/// <summary>
		/// Estimated text glyph coverage 0-255 for PerceivedCellColor (Acrylic/Mica).
		/// Controls how much foreground color bleeds into the perceived background.
		/// Default 90 (~35% coverage).
		/// </summary>
		public byte TextCoverage { get; init; } = 90;

		/// <summary>
		/// Per-cell compositing callback (Custom only).
		/// Receives (topCell, cellBelow, overlayAlpha) and returns the composited Cell.
		/// </summary>
		public Func<Cell, Cell, byte, Cell>? CompositeFunc { get; init; }

		/// <summary>
		/// Creates an Acrylic brush — Gaussian bg blend + character bubble-up + configurable fade.
		/// </summary>
		public static TransparencyBrush Acrylic(float fadeExponent = 0.12f, byte textCoverage = 90)
			=> new() { Style = TransparencyStyle.Acrylic, FadeExponent = fadeExponent, TextCoverage = textCoverage };

		/// <summary>
		/// Creates a Mica brush — Gaussian bg blend, no character bubble-up.
		/// </summary>
		public static TransparencyBrush Mica(byte textCoverage = 90)
			=> new() { Style = TransparencyStyle.Mica, TextCoverage = textCoverage };

		/// <summary>
		/// Creates a Tinted brush — simple bg-only overlay.
		/// </summary>
		public static TransparencyBrush Tinted()
			=> new() { Style = TransparencyStyle.Tinted };

		/// <summary>
		/// Creates a Custom brush with a user-provided compositing function.
		/// </summary>
		public static TransparencyBrush WithCustom(Func<Cell, Cell, byte, Cell> compositeFunc)
			=> new() { Style = TransparencyStyle.Custom, CompositeFunc = compositeFunc };
	}
}
