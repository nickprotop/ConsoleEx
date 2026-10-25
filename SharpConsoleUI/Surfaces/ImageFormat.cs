// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Surfaces;

/// <summary>Which image encoding <see cref="SurfaceExport.ToImage"/> produces.</summary>
/// <remarks>
/// <para>
/// Measured on a 174x50 desktop at 8x16 (1392x800 pixels), encoding the same pixels:
/// </para>
/// <list type="table">
///   <listheader><term>Format</term><description>Encode / size</description></listheader>
///   <item><term>Png</term><description>70 ms / 50 KB</description></item>
///   <item><term>Jpeg (q75)</term><description>9 ms / 113 KB</description></item>
///   <item><term>Tga</term><description>4 ms / 569 KB</description></item>
/// </list>
/// <para>
/// PNG is smallest because terminal output is large areas of flat colour, which is the case
/// deflate handles best and JPEG handles worst. That advantage shrinks as a surface gains
/// photographic content.
/// </para>
/// </remarks>
public enum ImageFormat
{
	/// <summary>Lossless, smallest for terminal content, slowest to encode. The default.</summary>
	/// <remarks>
	/// The only format whose output is byte-identical run to run, so the only one usable for
	/// golden-image comparison. Pair it with <see cref="FontSource.Embedded"/>.
	/// </remarks>
	Png = 0,

	/// <summary>Lossless and fast, at roughly ten times the size. For streaming frames.</summary>
	/// <remarks>
	/// Run-length encoded. The right choice when a frame is produced, consumed and discarded -
	/// a browser host or a screen recording - where encode latency bounds the frame rate and
	/// the bytes never reach a disk.
	/// </remarks>
	Tga = 1,

	/// <summary>Lossy, fast, small on photographic content. For attachments.</summary>
	/// <remarks>
	/// Loses the crisp glyph edges terminal output is made of, so text acquires visible
	/// ringing at low quality. Larger than PNG on a plain terminal surface; smaller once the
	/// surface carries an image or a gradient.
	/// </remarks>
	Jpeg = 2,
}
