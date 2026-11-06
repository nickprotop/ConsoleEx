// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Fetches images from URLs and converts them to Cell arrays for embedding in HTML layout lines.
	/// Supports Kitty graphics protocol when available, with half-block fallback.
	/// </summary>
	public static class HtmlImageLoader
	{
		private static uint _nextImageId;

		internal static readonly HttpClient HttpClient = new(new HttpClientHandler
		{
			MaxAutomaticRedirections = 5,
			AllowAutoRedirect = true,
		})
		{
			Timeout = TimeSpan.FromSeconds(15),
			DefaultRequestHeaders =
			{
				{ "User-Agent", "SharpConsoleUI/1.0 (HtmlControl; +https://github.com/nickprotop/ConsoleEx)" }
			}
		};

		/// <summary>
		/// Fetches an image from a URL and renders it as Cell rows for embedding in HTML layout.
		/// Returns null if fetch fails or URL is invalid.
		/// </summary>
		public static Task<Cell[][]?> LoadAndRenderAsync(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
			=> LoadAndRenderAsync(url, maxWidthChars, background, graphicsProtocol, widthIsResolved: false);

		/// <summary>
		/// Fetches an image and renders it, saying whether the column count is already resolved.
		/// </summary>
		/// <param name="url">A <c>data:</c>, <c>http:</c>, <c>https:</c> or <c>file:</c> URL.</param>
		/// <param name="maxWidthChars">
		/// The columns available, or the exact columns to use when
		/// <paramref name="widthIsResolved"/> is true.
		/// </param>
		/// <param name="background">The colour behind the image.</param>
		/// <param name="graphicsProtocol">The terminal's graphics protocol, if it has one.</param>
		/// <param name="widthIsResolved">True when the width comes from the page rather than the image.</param>
		/// <returns>One Cell row per terminal row, or null if the image cannot be loaded.</returns>
		public static async Task<Cell[][]?> LoadAndRenderAsync(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol, bool widthIsResolved)
		{
			url = NormalizeUrl(url);
			byte[] imageBytes;

			if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				imageBytes = ParseDataUri(url);
				if (imageBytes.Length == 0)
					return null;
			}
			else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
					 url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				var response = await HttpClient.GetAsync(url).ConfigureAwait(false);
				var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
				// Skip non-image content types (HTML error pages, SVG, etc.)
				if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
					contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
					return null;
				const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB
				if (response.Content.Headers.ContentLength > MaxImageBytes)
					return null;
				imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
				if (imageBytes.Length > MaxImageBytes)
					return null;
			}
			else if (IsLocalFileUrl(url))
			{
				imageBytes = ReadLocalFile(url);
				if (imageBytes.Length == 0)
					return null;
			}
			else
			{
				return null;
			}

			using var stream = new MemoryStream(imageBytes);
			var pixelBuffer = PixelBuffer.FromStream(stream);

			return RenderFromBuffer(pixelBuffer, maxWidthChars, background, graphicsProtocol, widthIsResolved);
		}

		/// <summary>
		/// Synchronous version that fetches an image from a URL and renders it as Cell rows.
		/// Returns null if fetch fails or URL is invalid.
		/// </summary>
		public static Cell[][]? LoadAndRender(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
			=> LoadAndRender(url, maxWidthChars, background, graphicsProtocol, widthIsResolved: false);

		/// <summary>
		/// Fetches an image synchronously, saying whether the column count is already resolved.
		/// </summary>
		/// <param name="url">A <c>data:</c>, <c>http:</c>, <c>https:</c> or <c>file:</c> URL.</param>
		/// <param name="maxWidthChars">
		/// The columns available, or the exact columns to use when
		/// <paramref name="widthIsResolved"/> is true.
		/// </param>
		/// <param name="background">The colour behind the image.</param>
		/// <param name="graphicsProtocol">The terminal's graphics protocol, if it has one.</param>
		/// <param name="widthIsResolved">True when the width comes from the page rather than the image.</param>
		/// <returns>One Cell row per terminal row, or null if the image cannot be loaded.</returns>
		public static Cell[][]? LoadAndRender(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol, bool widthIsResolved)
		{
			url = NormalizeUrl(url);
			if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				var imageBytes = ParseDataUri(url);
				if (imageBytes.Length == 0) return null;
				using var stream = new MemoryStream(imageBytes);
				var pixelBuffer = PixelBuffer.FromStream(stream);
				return RenderFromBuffer(pixelBuffer, maxWidthChars, background, graphicsProtocol, widthIsResolved);
			}

			if (IsLocalFileUrl(url))
			{
				var local = DecodeLocalFile(url);
				return local != null
					? RenderFromBuffer(local, maxWidthChars, background, graphicsProtocol, widthIsResolved)
					: null;
			}

			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				return null;

			// Run the whole async fetch+render off the captured SynchronizationContext.
			// Blocking on async work directly on the UI thread (.GetAwaiter().GetResult())
			// deadlocks once a UI SynchronizationContext is installed; Task.Run escapes it.
			return Task.Run(() => LoadAndRenderAsync(url, maxWidthChars, background, graphicsProtocol, widthIsResolved))
				.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Renders an already-loaded PixelBuffer as Cell rows for embedding in HTML layout.
		/// Uses Kitty graphics protocol when available, otherwise falls back to half-block rendering.
		/// </summary>
		public static Cell[][]? RenderFromBuffer(PixelBuffer buffer, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
			=> RenderFromBuffer(buffer, maxWidthChars, background, graphicsProtocol, widthIsResolved: false);

		/// <summary>
		/// Renders an already-loaded PixelBuffer as Cell rows, saying whether the column
		/// count has already been resolved from a width the page declared.
		/// </summary>
		/// <param name="buffer">The decoded image.</param>
		/// <param name="maxWidthChars">
		/// The columns available, or - when <paramref name="widthIsResolved"/> is true - the
		/// exact number of columns this image is to occupy.
		/// </param>
		/// <param name="background">The colour behind the image, for its odd last pixel row.</param>
		/// <param name="graphicsProtocol">The terminal's graphics protocol, if it has one.</param>
		/// <param name="widthIsResolved">
		/// True when <paramref name="maxWidthChars"/> is the width to use rather than a limit
		/// to fit within. An overload, not a default, so an already-compiled caller keeps the
		/// behaviour it was built against.
		/// </param>
		/// <returns>One Cell row per terminal row, or null if the image cannot be sized.</returns>
		public static Cell[][]? RenderFromBuffer(PixelBuffer buffer, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol, bool widthIsResolved)
		{
			if (maxWidthChars <= 0)
				return null;

			// Two kinds of caller. One has already worked out how many columns this
			// image gets, from a width the page declared, and passes that: scaling
			// it again by the image's own pixels would apply the viewport fraction
			// twice and shrink every sized image to a smudge.
			//
			// The other has no declared width, so the image's own pixel width IS
			// the declaration and goes through the same viewport fraction: a 640px
			// image is nearly two thirds of a 1024px page, so it takes nearly two
			// thirds of the columns. Reading its pixel count as a column count
			// instead made every real photograph wider than the terminal, and so
			// made every one of them fill it, thumbnail and hero alike.
			int targetWidth = widthIsResolved
				? maxWidthChars
				: HtmlBlockFlow.ScaleToColumns(buffer.Width, maxWidthChars);
			if (targetWidth <= 0)
				targetWidth = 1;

			// Calculate target height in terminal rows.
			// Each terminal row represents 2 pixel rows via half-block rendering.
			int targetHeight = (int)Math.Round((double)buffer.Height * targetWidth / buffer.Width / 2.0);
			if (targetHeight < 1)
				targetHeight = 1;

			bool kitty = graphicsProtocol != null && graphicsProtocol.SupportsKittyGraphics;
			var key = new RenderKey(targetWidth, targetHeight, background, kitty ? graphicsProtocol : null);

			// Every relayout (image batches, resize, theme change) renders each image again;
			// reuse the rows instead of rescaling — and, for Kitty, instead of PNG-encoding and
			// transmitting a fresh copy of the image to the terminal under a new id each time.
			var renders = _renderCache.GetValue(buffer, _ => new Dictionary<RenderKey, Cell[][]>());
			Cell[][]? rows;
			lock (renders)
			{
				if (!renders.TryGetValue(key, out rows))
				{
					rows = kitty
						? RenderKitty(buffer, targetWidth, targetHeight, background, graphicsProtocol!)
						: RenderHalfBlock(buffer, targetWidth, targetHeight, background);
					renders[key] = rows;
				}
			}

			// Hand out copies so the cached rows can't be altered through a layout line.
			var copy = new Cell[rows.Length][];
			for (int i = 0; i < rows.Length; i++)
				copy[i] = (Cell[])rows[i].Clone();
			return copy;
		}

		private readonly record struct RenderKey(int Width, int Height, Color Background, IGraphicsProtocol? KittyProtocol);

		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PixelBuffer, Dictionary<RenderKey, Cell[][]>> _renderCache = new();

		/// <summary>
		/// Renders using half-block characters (universal fallback).
		/// </summary>
		private static Cell[][] RenderHalfBlock(PixelBuffer buffer, int targetWidth, int targetHeight, Color background)
		{
			var cellGrid = HalfBlockRenderer.RenderScaled(buffer, targetWidth, targetHeight, background);
			return CellGridToRows(cellGrid);
		}

		/// <summary>
		/// Renders using Kitty graphics protocol: transmits the image and returns placeholder cells.
		/// </summary>
		private static Cell[][] RenderKitty(PixelBuffer buffer, int targetWidth, int targetHeight,
			Color background, IGraphicsProtocol protocol)
		{
			// Encode PNG at source resolution — Kitty scales to fit
			var pngData = KittyProtocol.EncodePng(buffer);

			// Allocate image ID and transmit
			uint imageId = Interlocked.Increment(ref _nextImageId);
			protocol.TransmitImage(imageId, pngData, targetWidth, targetHeight);

			// Build placeholder cell rows
			Color idFg = KittyProtocol.ImageIdToForegroundColor(imageId);
			var result = new Cell[targetHeight][];

			for (int row = 0; row < targetHeight; row++)
			{
				var rowCells = new Cell[targetWidth];
				for (int col = 0; col < targetWidth; col++)
				{
					string combiners = KittyProtocol.BuildPlaceholderCombiners(row, col);
					rowCells[col] = new Cell(ImagingDefaults.KittyPlaceholder, idFg, background)
					{
						Combiners = combiners
					};
				}
				result[row] = rowCells;
			}

			return result;
		}

		/// <summary>
		/// Converts a Cell[cols, rows] grid to a Cell[][] array of rows.
		/// </summary>
		private static Cell[][] CellGridToRows(Cell[,] cellGrid)
		{
			int cols = cellGrid.GetLength(0);
			int rows = cellGrid.GetLength(1);
			var result = new Cell[rows][];

			for (int row = 0; row < rows; row++)
			{
				var rowCells = new Cell[cols];
				for (int col = 0; col < cols; col++)
				{
					rowCells[col] = cellGrid[col, row];
				}
				result[row] = rowCells;
			}

			return result;
		}

		/// <summary>
		/// True when the URL names a file on this machine.
		/// </summary>
		/// <remarks>
		/// A <c>file:</c> URL only; a bare path is not treated as one, because a
		/// relative <c>src</c> on a fetched page resolves against the page's own
		/// host and must not silently become a local read.
		/// </remarks>
		/// <remarks>
		/// The scheme is matched on the string before the URI is parsed, because on
		/// Unix <c>Uri.TryCreate</c> accepts a bare absolute path as an absolute URI
		/// and reports <c>IsFile</c> for it. Relying on <c>IsFile</c> alone would
		/// make <c>src="/etc/hostname"</c> - a perfectly ordinary server-relative
		/// path on a fetched page - a read of this machine's disk.
		/// </remarks>
		public static bool IsLocalFileUrl(string? url) =>
			!string.IsNullOrEmpty(url)
			&& url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
			&& Uri.TryCreate(url, UriKind.Absolute, out var uri)
			&& uri.IsFile;

		/// <summary>
		/// Reads a <c>file:</c> URL's bytes, or an empty array if it cannot be read.
		/// </summary>
		/// <remarks>
		/// Capped at the same 10 MB as a download: the cost of decoding is the same
		/// wherever the bytes came from, and a local disk can hand over a far larger
		/// file far faster than the network would.
		/// </remarks>
		private static byte[] ReadLocalFile(string url)
		{
			const long MaxImageBytes = 10 * 1024 * 1024;

			try
			{
				if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsFile)
					return Array.Empty<byte>();

				var path = uri.LocalPath;
				var info = new FileInfo(path);
				if (!info.Exists || info.Length == 0 || info.Length > MaxImageBytes)
					return Array.Empty<byte>();

				return File.ReadAllBytes(path);
			}
			catch
			{
				// Missing, unreadable, a directory, or a path this platform rejects.
				// The caller shows the alt text, which is what a failed download does.
				return Array.Empty<byte>();
			}
		}

		/// <summary>
		/// Decodes an image from a <c>file:</c> URL. Returns null if it cannot be read or decoded.
		/// </summary>
		/// <remarks>
		/// Bounded the same way <see cref="DecodeDataUri"/> is, and for the same
		/// reason: layout runs during paint, on the UI thread, so a decode that
		/// does not finish would freeze the application rather than one control.
		/// </remarks>
		public static PixelBuffer? DecodeLocalFile(string url)
		{
			var bytes = ReadLocalFile(url);
			if (bytes.Length == 0)
				return null;

			try
			{
				var decode = Task.Run(() =>
				{
					using var stream = new MemoryStream(bytes);
					return PixelBuffer.FromStream(stream);
				});

				return decode.Wait(TimeSpan.FromSeconds(InlineDecodeTimeoutSeconds)) && decode.IsCompletedSuccessfully
					? decode.Result
					: null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Normalizes protocol-relative URLs (//example.com) to https://.
		/// </summary>
		private static string NormalizeUrl(string url)
		{
			if (url.StartsWith("//"))
				return "https:" + url;
			return url;
		}

		/// <summary>
		/// Decodes a <c>data:</c> URI image. Returns null if it isn't a decodable base64 raster image.
		/// </summary>
		internal static PixelBuffer? DecodeDataUri(string dataUri)
		{
			var bytes = ParseDataUri(dataUri);
			if (bytes.Length == 0)
				return null;

			// BOUNDED, because this runs on the UI thread. Layout decodes inline images in place
			// (HtmlBlockFlow), and layout happens during paint, so a decoder that takes a long
			// time over hostile input freezes the whole application rather than one control —
			// there is no cancellation token down here to cut it short.
			//
			// Formats with a known non-terminating path are excluded at the decoder instead (see
			// PixelBuffer's decoder options), so nothing is expected to reach this timeout. It is
			// the backstop for the next such bug, in a format we do still decode: the image is
			// dropped and its alt text shown, which is what a failed decode already does.
			try
			{
				var decode = Task.Run(() =>
				{
					using var stream = new MemoryStream(bytes);
					return PixelBuffer.FromStream(stream);
				});

				return decode.Wait(TimeSpan.FromSeconds(InlineDecodeTimeoutSeconds)) && decode.IsCompletedSuccessfully
					? decode.Result
					: null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// How long an inline <c>data:</c> image may take to decode before it is abandoned. Far
		/// longer than any legitimate decode of a 10 MB image, so it never fires in normal use.
		/// </summary>
		private const int InlineDecodeTimeoutSeconds = 5;

		private static byte[] ParseDataUri(string dataUri)
		{
			const int MaxDataUriBytes = 10 * 1024 * 1024; // 10 MB encoded

			// Format: data:[<mediatype>][;base64],<data>
			var commaIndex = dataUri.IndexOf(',');
			if (commaIndex < 0)
				return Array.Empty<byte>();

			var header = dataUri.Substring(0, commaIndex);
			var data = dataUri.Substring(commaIndex + 1);

			if (data.Length > MaxDataUriBytes)
				return Array.Empty<byte>();

			if (header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					return Convert.FromBase64String(data);
				}
				catch (FormatException)
				{
					return Array.Empty<byte>();
				}
			}

			return Array.Empty<byte>();
		}
	}
}
