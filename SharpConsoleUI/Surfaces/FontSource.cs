// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Surfaces;

/// <summary>
/// Where <see cref="SurfaceExport.ToImage"/> looks for the font it draws with.
/// </summary>
/// <remarks>
/// <para>
/// The real choice is between a picture that matches what this machine shows and a picture
/// that is identical on every machine. Both are legitimate - they serve different jobs - so
/// this states which one is wanted rather than guessing.
/// </para>
/// <para>
/// Only <see cref="Strict"/> and <see cref="File"/> can fail to produce text. The others end
/// at the embedded face, because an image with no text would still hash consistently and so
/// would pass a golden-image check it ought to have failed.
/// </para>
/// </remarks>
public enum FontSource
{
	/// <summary>
	/// Installed fonts first, the embedded face as a safety net, and installed fonts again
	/// for scripts the chosen face lacks. The default.
	/// </summary>
	/// <remarks>
	/// Uses a font this machine has, so the picture resembles local output, while still
	/// rendering text on a machine that has none.
	/// <para>
	/// This is NOT the font your terminal is configured with. A terminal's font lives in
	/// that emulator's own settings, and there is no cross-platform way to ask which one is
	/// in use, so what gets picked is a common monospace face found on disk. The layout,
	/// colours and content are exact; the letterforms may not be the ones on your screen.
	/// </para>
	/// <para>
	/// The search knows the usual Linux, macOS and Windows locations, but a distribution
	/// that puts fonts elsewhere simply falls through to the embedded face. Output may
	/// therefore differ between machines; for a reproducible image ask for
	/// <see cref="Embedded"/> explicitly.
	/// </para>
	/// </remarks>
	Automatic = 0,

	/// <summary>
	/// The embedded face only, ignoring anything installed.
	/// </summary>
	/// <remarks>
	/// The reproducible option: byte-identical on every platform, including a bare container
	/// with no fonts at all. This is what golden-image testing wants. Covers Latin, Greek,
	/// Cyrillic, box-drawing and common symbols. CJK is not included - no system fallback is
	/// consulted, since that would reintroduce the machine dependence this mode removes - and
	/// any codepoint it cannot draw is reported rather than silently blanked.
	/// </remarks>
	Embedded = 1,

	/// <summary>
	/// Installed fonts only, with no embedded fallback.
	/// </summary>
	/// <remarks>
	/// For callers who want to know what this machine can actually render, and would rather
	/// see the gap than have it filled in: an error when no font is found, and uncovered
	/// codepoints reported instead of substituted. Most callers want
	/// <see cref="Automatic"/>, which behaves identically wherever a font exists and only
	/// differs when none does.
	/// <para>
	/// Note that "installed" means "found in a known location". Expect this to fail in a
	/// container, and on any system that keeps its fonts somewhere unusual.
	/// </para>
	/// </remarks>
	Strict = 2,

	/// <summary>
	/// The file named by <see cref="ExportOptions.FontPath"/>.
	/// </summary>
	/// <remarks>
	/// Implied when <see cref="ExportOptions.FontPath"/> is set, so it rarely needs naming.
	/// A font that cannot be loaded is an error rather than a silent fallback: an explicit
	/// choice that was ignored would make the image a lie about what produced it.
	/// </remarks>
	File = 3,
}
