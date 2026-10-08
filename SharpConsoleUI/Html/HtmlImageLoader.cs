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
		public static async Task<Cell[][]?> LoadAndRenderAsync(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
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
			else
			{
				return null;
			}

			using var stream = new MemoryStream(imageBytes);
			var pixelBuffer = PixelBuffer.FromStream(stream);

			return RenderFromBuffer(pixelBuffer, maxWidthChars, background, graphicsProtocol);
		}

		/// <summary>
		/// Synchronous version that fetches an image from a URL and renders it as Cell rows.
		/// Returns null if fetch fails or URL is invalid.
		/// </summary>
		public static Cell[][]? LoadAndRender(string url, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
		{
			url = NormalizeUrl(url);
			if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				var imageBytes = ParseDataUri(url);
				if (imageBytes.Length == 0) return null;
				using var stream = new MemoryStream(imageBytes);
				var pixelBuffer = PixelBuffer.FromStream(stream);
				return RenderFromBuffer(pixelBuffer, maxWidthChars, background, graphicsProtocol);
			}

			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				return null;

			// Run the whole async fetch+render off the captured SynchronizationContext.
			// Blocking on async work directly on the UI thread (.GetAwaiter().GetResult())
			// deadlocks once a UI SynchronizationContext is installed; Task.Run escapes it.
			return Task.Run(() => LoadAndRenderAsync(url, maxWidthChars, background, graphicsProtocol))
				.GetAwaiter().GetResult();
		}

		/// <summary>
		/// Renders an already-loaded PixelBuffer as Cell rows for embedding in HTML layout.
		/// Uses Kitty graphics protocol when available, otherwise falls back to half-block rendering.
		/// </summary>
		public static Cell[][]? RenderFromBuffer(PixelBuffer buffer, int maxWidthChars, Color background,
			IGraphicsProtocol? graphicsProtocol = null)
		{
			if (maxWidthChars <= 0)
				return null;

			// Natural width in terminal columns, clamped to available space.
			int naturalCols = Math.Max(1, (int)Math.Ceiling(buffer.Width / HtmlConstants.ImagePxToCharRatio));
			int targetWidth = Math.Min(naturalCols, maxWidthChars);
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
			try
			{
				using var stream = new MemoryStream(bytes);
				return PixelBuffer.FromStream(stream);
			}
			catch
			{
				return null;
			}
		}

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
