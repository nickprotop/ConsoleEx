// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Reading an image off the system clipboard, as encoded file bytes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Separate from the text path because the two share almost nothing: the bytes are binary
	/// rather than a string, the formats are negotiated per backend, and Windows hands over a
	/// headerless DIB that has to be given a file header before anything can decode it.
	/// </para>
	/// <para>
	/// Every path returns bytes that <see cref="Imaging.PixelBuffer.FromStream"/> accepts, which
	/// means PNG, JPEG, BMP or GIF. PNG is preferred wherever the clipboard offers it: it is
	/// lossless, every backend speaks it, and it is the format a screenshot tool puts there.
	/// </para>
	/// </remarks>
	public static partial class ClipboardHelper
	{
		#region Public API

		/// <summary>
		/// Reads an image from the clipboard as encoded file bytes, or null when the clipboard holds
		/// no image or cannot be read.
		/// </summary>
		/// <returns>
		/// PNG, JPEG, BMP or GIF bytes, decodable with <see cref="Imaging.PixelBuffer.FromStream"/>.
		/// Null when there is no image, the backend cannot read one, or the read failed.
		/// </returns>
		/// <remarks>
		/// May block, like <see cref="GetText"/>. Use <see cref="GetImageWithTimeout"/> from any path
		/// that runs on the UI thread.
		/// </remarks>
		public static byte[]? GetImage()
		{
			EnsureDetected();
			try
			{
				return _backend switch
				{
					ClipboardBackend.WindowsClip => ReadWindowsClipboardImage(),
					ClipboardBackend.WlClipboard => ReadWaylandImage(),
					ClipboardBackend.Xclip => ReadXclipImage(),
					ClipboardBackend.Pbcopy => ReadMacImage(),
					ClipboardBackend.X11 => _x11?.GetImage(ProcessTimeoutMs),

					// xsel cannot read a binary target at all: it is built around text selections and
					// mangles anything that is not. Returning null is honest; there is no partial
					// answer to give.
					ClipboardBackend.Xsel => null,

					// The in-process buffer holds text. An image was never put in it, and the text
					// buffer must never be offered as one.
					_ => null,
				};
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Reads an image from the clipboard without blocking the caller for more than
		/// <paramref name="timeoutMs"/> milliseconds.
		/// </summary>
		/// <param name="timeoutMs">How long to wait before giving up.</param>
		/// <returns>The image bytes, or null on timeout, on failure, or when there is no image.</returns>
		/// <remarks>
		/// <para>
		/// Use this from UI-thread paths. The Unix backends spawn a child process that can stall, and
		/// a stalled child must never hold the render loop (issue #42, the same hazard the text read
		/// has).
		/// </para>
		/// <para>
		/// Unlike <see cref="GetTextWithTimeout"/> there is no fallback value: a timeout returns null
		/// rather than the internal buffer, because that buffer holds text and offering it as an
		/// image would be a lie rather than a degraded answer.
		/// </para>
		/// </remarks>
		public static byte[]? GetImageWithTimeout(int timeoutMs = 500)
		{
			EnsureDetected();

			// In-process reads cannot stall on another program, so the thread hop buys nothing.
			if (_backend is ClipboardBackend.WindowsClip or ClipboardBackend.InternalFallback or ClipboardBackend.Unknown)
				return GetImage();

			// The X11 read already runs on its own thread and takes its own bound, as the text read
			// does: an X selection is served by whoever owns it, so this waits on another application
			// waking up rather than on a child process of ours.
			if (_backend == ClipboardBackend.X11)
				return _x11?.GetImage(Math.Max(timeoutMs, X11PasteTimeoutMs));

			byte[]? result = null;
			var done = new ManualResetEventSlim(false);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try { result = GetImage(); }
				catch { /* best-effort: a failed read is no image */ }
				finally { done.Set(); }
			});

			return done.Wait(timeoutMs) ? result : null;
		}

		#endregion

		#region Unix tools

		/// <summary>MIME types to ask a Unix clipboard tool for, best first.</summary>
		/// <remarks>
		/// PNG first because it is lossless and universally offered. BMP last because it is the
		/// bulkiest and the least likely to be what a producer actually put there.
		/// </remarks>
		private static readonly string[] ImageMimeTypes = { "image/png", "image/jpeg", "image/bmp" };

		/// <summary>wl-paste, asking for the best image type the clipboard actually offers.</summary>
		private static byte[]? ReadWaylandImage()
		{
			// --list-types first rather than attempting each type in turn: asking wl-paste for a type
			// the clipboard does not have prints an error and exits non-zero, so probing blind would
			// spawn up to three processes to learn what one call answers.
			string offered = RunProcessRead("wl-paste", "--list-types");
			string? type = PickImageType(offered);
			if (type == null)
				return null;

			return RunProcessReadBytes("wl-paste", "--no-newline", "--type", type);
		}

		/// <summary>xclip, which names its targets the same way.</summary>
		private static byte[]? ReadXclipImage()
		{
			string offered = RunProcessRead("xclip", "-selection", "clipboard", "-t", "TARGETS", "-o");
			string? type = PickImageType(offered);
			if (type == null)
				return null;

			return RunProcessReadBytes("xclip", "-selection", "clipboard", "-t", type, "-o");
		}

		/// <summary>
		/// The best supported image type in a tool's list of what the clipboard offers.
		/// </summary>
		/// <param name="offeredTypes">One MIME type or X target per line, as the tools print them.</param>
		/// <returns>The type to ask for, or null when none of them is an image we can decode.</returns>
		internal static string? PickImageType(string offeredTypes)
		{
			if (string.IsNullOrWhiteSpace(offeredTypes))
				return null;

			var offered = offeredTypes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (string wanted in ImageMimeTypes)
			{
				foreach (string line in offered)
				{
					if (string.Equals(line, wanted, StringComparison.OrdinalIgnoreCase))
						return line;
				}
			}
			return null;
		}

		/// <summary>
		/// Runs a tool and returns its raw standard output.
		/// </summary>
		/// <remarks>
		/// The byte twin of <see cref="RunProcessRead"/>, which decodes as UTF-8. Image bytes must
		/// never go through a string: the decode replaces every invalid sequence with U+FFFD, so the
		/// bytes that came back would not be the bytes that were sent and nothing would decode them.
		/// Bounded the same way, because a paste from an owner that never answers takes forever.
		/// </remarks>
		internal static byte[]? RunProcessReadBytes(string cmd, params string[] args)
		{
			Interlocked.Increment(ref _processReadCount);

			var psi = ToolStartInfo(cmd, args);
			using var p = Process.Start(psi);
			if (p == null)
				return null;

			p.ErrorDataReceived += static (_, _) => { };
			p.BeginErrorReadLine();

			using var buffer = new MemoryStream();
			var copy = p.StandardOutput.BaseStream.CopyToAsync(buffer);
			if (!copy.Wait(ProcessTimeoutMs))
			{
				try { p.Kill(entireProcessTree: true); }
				catch { }
				return null;
			}

			p.WaitForExit(ProcessTimeoutMs);

			// A tool that found nothing exits non-zero having printed nothing. Treating that as an
			// empty image would hand a zero-length array to a decoder.
			return buffer.Length > 0 ? buffer.ToArray() : null;
		}

		#endregion

		#region macOS

		/// <summary>
		/// The clipboard image on macOS, via AppleScript.
		/// </summary>
		/// <remarks>
		/// <para>
		/// pbpaste is text only; there is no flag that makes it emit binary. AppleScript can read the
		/// PNG class, and this writes it to a temporary file rather than parsing the
		/// <c>«data PNGf...»</c> hex that osascript prints.
		/// </para>
		/// <para>
		/// The hex form was the alternative and is worse here: osascript wraps and truncates long
		/// literals when printing them, so a screenshot-sized PNG comes back mangled, and the hex
		/// doubles the bytes that cross the pipe. A temporary file is exact at any size, and the
		/// cost is one file written and deleted.
		/// </para>
		/// </remarks>
		private static byte[]? ReadMacImage()
		{
			string path = Path.Combine(Path.GetTempPath(), $"scui-clip-{Guid.NewGuid():N}.png");
			try
			{
				// `the clipboard as «class PNGf»` raises when the clipboard holds no image, which is
				// the signal there is nothing to read. Nothing is written in that case.
				string script =
					"try\n" +
					$"set f to open for access POSIX file \"{path}\" with write permission\n" +
					"write (the clipboard as «class PNGf») to f\n" +
					"close access f\n" +
					"end try";

				RunProcessRead("osascript", "-e", script);

				if (!File.Exists(path))
					return null;

				byte[] bytes = File.ReadAllBytes(path);
				return bytes.Length > 0 ? bytes : null;
			}
			catch
			{
				return null;
			}
			finally
			{
				try { if (File.Exists(path)) File.Delete(path); }
				catch { /* a leftover temp file is not worth failing a paste over */ }
			}
		}

		#endregion

		#region Windows

		// The clipboard's own image formats. CF_DIB and CF_DIBV5 are headerless: the clipboard holds
		// everything from BITMAPINFOHEADER onward, and a BMP file is that preceded by a 14-byte
		// BITMAPFILEHEADER. "PNG" is a registered format name rather than a numbered one, and is what
		// browsers and modern screenshot tools put there, so it is asked for first: it needs no
		// reconstruction and carries alpha that a DIB may not.
		private const uint CF_DIB = 8;
		private const uint CF_DIBV5 = 17;

		/// <summary>Bytes in a BITMAPFILEHEADER, which a clipboard DIB does not include.</summary>
		private const int BitmapFileHeaderSize = 14;

		/// <summary>Bytes in a BITMAPINFOHEADER, the smallest DIB header.</summary>
		private const int BitmapInfoHeaderSize = 40;

		/// <summary>"BM", the two bytes a BMP file starts with.</summary>
		private const ushort BitmapFileMagic = 0x4D42;

		/// <summary>Reads an image from the Windows clipboard in-process, never spawning a shell.</summary>
		private static byte[]? ReadWindowsClipboardImage()
		{
			if (!OperatingSystem.IsWindows())
				return null;

			try
			{
				if (!OpenClipboardWithRetry())
					return null;
				try
				{
					uint png = RegisterClipboardFormatW("PNG");
					if (png != 0 && TryReadClipboardBytes(png, out byte[]? pngBytes))
						return pngBytes;

					// V5 before the older one: it carries the alpha channel and the colour space that
					// CF_DIB drops.
					if (TryReadClipboardBytes(CF_DIBV5, out byte[]? v5))
						return DibToBmp(v5!);

					if (TryReadClipboardBytes(CF_DIB, out byte[]? dib))
						return DibToBmp(dib!);

					return null;
				}
				finally
				{
					CloseClipboard();
				}
			}
			catch
			{
				return null;
			}
		}

		/// <summary>Copies one clipboard format's block out, bounded by the block's real size.</summary>
		/// <remarks>
		/// Sized by <c>GlobalSize</c> rather than by anything the data claims, for the reason the text
		/// read gives: this is another process's memory, and trusting a length from inside it is how a
		/// read runs off the end of the allocation.
		/// </remarks>
		private static bool TryReadClipboardBytes(uint format, out byte[]? bytes)
		{
			bytes = null;

			nint handle = GetClipboardData(format);
			if (handle == nint.Zero)
				return false;

			nint pointer = GlobalLock(handle);
			if (pointer == nint.Zero)
				return false;
			try
			{
				nuint size = GlobalSize(handle);
				if (size == 0 || size > int.MaxValue)
					return false;

				bytes = new byte[(int)size];
				Marshal.Copy(pointer, bytes, 0, (int)size);
				return true;
			}
			finally
			{
				GlobalUnlock(handle);
			}
		}

		/// <summary>
		/// Turns a clipboard DIB into BMP file bytes by prepending the file header it lacks.
		/// </summary>
		/// <param name="dib">The clipboard block, starting at the DIB header.</param>
		/// <returns>BMP bytes, or null when the block is too small or malformed to describe an image.</returns>
		/// <remarks>
		/// The pixel data offset is the one field that has to be computed rather than copied: it is
		/// the file header, plus the DIB header's own size, plus the colour table. Getting it wrong
		/// does not fail, it decodes the palette as pixels.
		/// </remarks>
		internal static byte[]? DibToBmp(byte[] dib)
		{
			if (dib.Length < BitmapInfoHeaderSize)
				return null;

			int headerSize = BitConverter.ToInt32(dib, 0);
			if (headerSize < BitmapInfoHeaderSize || headerSize > dib.Length)
				return null;

			int bitCount = BitConverter.ToInt16(dib, 14);
			int compression = BitConverter.ToInt32(dib, 16);
			int paletteEntries = BitConverter.ToInt32(dib, 32);

			// A palette is present at 8 bits and below. A zero count there means the default, which is
			// every colour the depth can express.
			int paletteBytes = 0;
			if (bitCount <= 8)
			{
				int entries = paletteEntries > 0 ? paletteEntries : 1 << bitCount;
				paletteBytes = entries * 4;
			}

			// BI_BITFIELDS puts three colour masks between the header and the pixels. BI_ALPHABITFIELDS
			// adds a fourth. A V5 header already includes the masks in its own size, so they are only
			// separate for the older header.
			const int BI_BITFIELDS = 3;
			const int BI_ALPHABITFIELDS = 6;
			if (headerSize == BitmapInfoHeaderSize)
			{
				if (compression == BI_BITFIELDS) paletteBytes += 12;
				else if (compression == BI_ALPHABITFIELDS) paletteBytes += 16;
			}

			int pixelOffset = BitmapFileHeaderSize + headerSize + paletteBytes;
			if (pixelOffset > BitmapFileHeaderSize + dib.Length)
				return null;

			var bmp = new byte[BitmapFileHeaderSize + dib.Length];
			BitConverter.TryWriteBytes(bmp.AsSpan(0), BitmapFileMagic);
			BitConverter.TryWriteBytes(bmp.AsSpan(2), bmp.Length);
			// Bytes 6..9 are two reserved words, left zero.
			BitConverter.TryWriteBytes(bmp.AsSpan(10), pixelOffset);
			dib.CopyTo(bmp, BitmapFileHeaderSize);
			return bmp;
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern uint RegisterClipboardFormatW(string lpszFormat);

		#endregion
	}
}
