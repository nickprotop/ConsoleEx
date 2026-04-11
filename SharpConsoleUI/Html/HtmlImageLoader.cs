// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

#pragma warning disable CS1591

using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Html
{
	/// <summary>
	/// Fetches images from URLs and converts them to Cell arrays for embedding in HTML layout lines.
	/// </summary>
	public static class HtmlImageLoader
	{
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
		public static async Task<Cell[][]?> LoadAndRenderAsync(string url, int maxWidthChars, Color background)
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

			return RenderFromBuffer(pixelBuffer, maxWidthChars, background);
		}

		/// <summary>
		/// Synchronous version that fetches an image from a URL and renders it as Cell rows.
		/// Returns null if fetch fails or URL is invalid.
		/// </summary>
		public static Cell[][]? LoadAndRender(string url, int maxWidthChars, Color background)
		{
			url = NormalizeUrl(url);
			if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
			{
				var imageBytes = ParseDataUri(url);
				if (imageBytes.Length == 0) return null;
				using var stream = new MemoryStream(imageBytes);
				var pixelBuffer = PixelBuffer.FromStream(stream);
				return RenderFromBuffer(pixelBuffer, maxWidthChars, background);
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
			return RenderFromBuffer(buffer, maxWidthChars, background);
		}

		/// <summary>
		/// Renders an already-loaded PixelBuffer as Cell rows for embedding in HTML layout.
		/// </summary>
		public static Cell[][]? RenderFromBuffer(PixelBuffer buffer, int maxWidthChars, Color background)
		{
			if (maxWidthChars <= 0)
				return null;

			// Natural width in terminal columns, clamped to available space.
			// ImagePxToCharRatio converts pixel width to terminal column width.
			int naturalCols = Math.Max(1, (int)Math.Ceiling(buffer.Width / HtmlConstants.ImagePxToCharRatio));
			int targetWidth = Math.Min(naturalCols, maxWidthChars);
			if (targetWidth <= 0)
				targetWidth = 1;

			// Calculate target height in terminal rows.
			// Each terminal row represents 2 pixel rows via half-block rendering.
			int targetHeight = (int)Math.Round((double)buffer.Height * targetWidth / buffer.Width / 2.0);
			if (targetHeight < 1)
				targetHeight = 1;

			var cellGrid = HalfBlockRenderer.RenderScaled(buffer, targetWidth, targetHeight, background);

			// cellGrid is Cell[cols, rows] — convert to Cell[][] (array of rows)
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
