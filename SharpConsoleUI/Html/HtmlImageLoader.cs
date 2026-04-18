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

		internal static readonly HttpClient HttpClient = new()
		{
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
				imageBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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

			var response = Task.Run(async () => await HttpClient.GetAsync(url).ConfigureAwait(false))
				.GetAwaiter().GetResult();
			var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
			if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
			    contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
				return null;
			var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
			using var ms = new MemoryStream(bytes);
			var buffer = PixelBuffer.FromStream(ms);
			return RenderFromBuffer(buffer, maxWidthChars, background, graphicsProtocol);
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

			if (graphicsProtocol != null && graphicsProtocol.SupportsKittyGraphics)
				return RenderKitty(buffer, targetWidth, targetHeight, background, graphicsProtocol);

			return RenderHalfBlock(buffer, targetWidth, targetHeight, background);
		}

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

		private static byte[] ParseDataUri(string dataUri)
		{
			// Format: data:[<mediatype>][;base64],<data>
			var commaIndex = dataUri.IndexOf(',');
			if (commaIndex < 0)
				return Array.Empty<byte>();

			var header = dataUri.Substring(0, commaIndex);
			var data = dataUri.Substring(commaIndex + 1);

			if (header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
			{
				return Convert.FromBase64String(data);
			}

			// URL-encoded binary data is not commonly used for images; skip
			return Array.Empty<byte>();
		}
	}
}
