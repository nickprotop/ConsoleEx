// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Imaging
{
	/// <summary>
	/// Renders images using the Kitty graphics protocol with virtual placements.
	/// Transmits image data as PNG via <see cref="IGraphicsProtocol"/> and writes
	/// U+10EEEE placeholder cells into the <see cref="CharacterBuffer"/>.
	/// The terminal replaces placeholders with actual image pixels.
	/// PNG encoding happens asynchronously to avoid blocking the UI thread.
	/// </summary>
	internal sealed class KittyImageRenderer : IImageRenderer
	{
		private static uint _nextImageId;

		private readonly IGraphicsProtocol _protocol;
		private uint _currentImageId;
		private bool _hasTransmittedImage;

		// PNG cache — encode once per source, reuse across resizes
		private PixelBuffer? _cachedPngSource;
		private volatile byte[]? _cachedPngData;
		private volatile bool _isEncoding;

		// Transmission tracking — retransmit when dimensions change
		private int _transmittedCols;
		private int _transmittedRows;

		// Container reference for async invalidation
		private IContainer? _container;

		// Set when the image cannot be shown, and drawn in its place. A failure that leaves the area
		// blank is indistinguishable from a bug in the terminal, the protocol or the layout, so the
		// reason is put on the screen where the picture would have been.
		private volatile string? _failure;

		public KittyImageRenderer(IGraphicsProtocol protocol)
		{
			_protocol = protocol;
		}

		/// <inheritdoc />
		public void Paint(
			CharacterBuffer buffer,
			LayoutRect destRect,
			LayoutRect clipRect,
			PixelBuffer source,
			ImageScaleMode scaleMode,
			int cropOffsetX,
			int cropOffsetY,
			int renderCols,
			int renderRows,
			Color windowBackground)
		{
			int displayWidth = Math.Min(renderCols - cropOffsetX, destRect.Width);
			int displayHeight = Math.Min(renderRows - cropOffsetY, destRect.Height);

			// Check if we need to encode a new PNG (source changed)
			bool sourceChanged = !ReferenceEquals(_cachedPngSource, source);
			if (sourceChanged)
			{
				// Invalidate cached PNG
				_cachedPngData = null;
				_cachedPngSource = source;

				// A new image gets a new attempt: the old one's failure says nothing about it.
				_failure = null;

				// Delete old transmitted image
				if (_hasTransmittedImage)
				{
					_protocol.DeleteImage(_currentImageId);
					_hasTransmittedImage = false;
				}
			}

			// An encode that failed is reported in place of the image, and not retried: the same
			// source would fail the same way, and a retry loop would encode on every frame.
			if (_failure is { } failed && _cachedPngData == null)
			{
				RenderMessage(buffer, destRect, clipRect, displayWidth, displayHeight,
					windowBackground, failed);
				return;
			}

			// If PNG not yet encoded, start async encoding and show loading placeholder
			if (_cachedPngData == null)
			{
				if (!_isEncoding)
				{
					_isEncoding = true;
					var sourceRef = source;
					Task.Run(() =>
					{
						// EVERY EXIT CLEARS THE LATCH. _isEncoding is what stops a second encode
						// starting while one runs; left set, the control never retries and never
						// paints, so a throw here used to mean a permanently blank image and an
						// exception swallowed by an unobserved task.
						try
						{
							_cachedPngData = KittyProtocol.EncodePng(sourceRef);
							_failure = null;
						}
						catch (Exception ex)
						{
							_failure = $"Image failed to encode: {ex.Message}";
						}
						finally
						{
							_isEncoding = false;
							_container?.Invalidate(Invalidation.Repaint);
						}
					});
				}

				// Render "Loading..." placeholder
				RenderLoadingPlaceholder(buffer, destRect, clipRect, displayWidth, displayHeight, windowBackground);
				return;
			}

			// PNG is ready — transmit if needed (new image or dimensions changed)
			bool needsTransmit = !_hasTransmittedImage
				|| _transmittedCols != renderCols
				|| _transmittedRows != renderRows;

			if (needsTransmit)
			{
				// A placement the protocol will refuse must be caught HERE. The transmit itself
				// reports nothing, so without this the refusal is silent: the placeholder cells go
				// out referring to an image the terminal was never sent, and the area is simply
				// blank with nothing anywhere saying why.
				if (renderCols > ImagingDefaults.KittyMaxPlacementCells ||
					renderRows > ImagingDefaults.KittyMaxPlacementCells)
				{
					_failure = $"Image too large to display: {renderCols}x{renderRows} cells " +
						$"exceeds the {ImagingDefaults.KittyMaxPlacementCells} cell limit.";
					RenderMessage(buffer, destRect, clipRect, displayWidth, displayHeight,
						windowBackground, _failure);
					return;
				}

				if (_hasTransmittedImage)
					_protocol.DeleteImage(_currentImageId);

				_currentImageId = Interlocked.Increment(ref _nextImageId);
				_protocol.TransmitImage(_currentImageId, _cachedPngData, renderCols, renderRows);

				_transmittedCols = renderCols;
				_transmittedRows = renderRows;
				_hasTransmittedImage = true;
			}

			// Write placeholder cells — image ID encoded as foreground color per Kitty spec
			Color idFg = KittyProtocol.ImageIdToForegroundColor(_currentImageId);

			for (int cy = 0; cy < displayHeight && destRect.Y + cy < destRect.Bottom; cy++)
			{
				int y = destRect.Y + cy;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				for (int cx = 0; cx < displayWidth && destRect.X + cx < destRect.Right; cx++)
				{
					int x = destRect.X + cx;
					if (x < clipRect.X || x >= clipRect.Right) continue;

					int imageRow = cropOffsetY + cy;
					int imageCol = cropOffsetX + cx;
					string combiners = KittyProtocol.BuildPlaceholderCombiners(imageRow, imageCol);

					var cell = new Cell(ImagingDefaults.KittyPlaceholder, idFg, windowBackground)
					{
						Combiners = combiners
					};
					buffer.SetCell(x, y, cell);
				}
			}
		}

		/// <summary>
		/// Renders a centered "Loading..." text in the image area while PNG encoding is in progress.
		/// </summary>
		private static void RenderLoadingPlaceholder(
			CharacterBuffer buffer,
			LayoutRect destRect,
			LayoutRect clipRect,
			int displayWidth,
			int displayHeight,
			Color windowBackground)
			=> RenderMessage(buffer, destRect, clipRect, displayWidth, displayHeight,
				windowBackground, "Loading\u2026");

		/// <summary>
		/// Fills the image area and centers a single line of text in it.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Used for the loading state and for a failure. A message longer than the area is cut to
		/// fit rather than spilling into whatever is drawn beside the image.
		/// </para>
		/// <para>
		/// The text is measured and written in DISPLAY COLUMNS, not characters: an exception message
		/// can carry a path or a quoted name with wide or combining characters in it, and measuring
		/// that with string length would centre it wrongly and overrun the area.
		/// </para>
		/// </remarks>
		private static void RenderMessage(
			CharacterBuffer buffer,
			LayoutRect destRect,
			LayoutRect clipRect,
			int displayWidth,
			int displayHeight,
			Color windowBackground,
			string message)
		{
			Color fg = new Color(160, 160, 160);

			var cells = MarkupParser.Parse(MarkupParser.Escape(message), fg, windowBackground);
			if (cells.Count > displayWidth)
				cells = cells.GetRange(0, Math.Max(0, displayWidth));

			int textX = destRect.X + Math.Max(0, (displayWidth - cells.Count) / 2);
			int textY = destRect.Y + displayHeight / 2;

			for (int cy = 0; cy < displayHeight && destRect.Y + cy < destRect.Bottom; cy++)
			{
				int y = destRect.Y + cy;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				for (int cx = 0; cx < displayWidth && destRect.X + cx < destRect.Right; cx++)
				{
					int x = destRect.X + cx;
					if (x < clipRect.X || x >= clipRect.Right) continue;

					int idx = x - textX;
					if (y == textY && idx >= 0 && idx < cells.Count)
						buffer.SetCell(x, y, cells[idx]);
					else
						buffer.SetNarrowCell(x, y, ' ', fg, windowBackground);
				}
			}
		}

		/// <summary>
		/// Stores the container reference for async invalidation callbacks.
		/// Called by the renderer when the container is available.
		/// </summary>
		internal void SetContainer(IContainer? container)
		{
			_container = container;
		}

		/// <inheritdoc />
		public void OnSourceChanged()
		{
			if (_hasTransmittedImage)
			{
				_protocol.DeleteImage(_currentImageId);
				_hasTransmittedImage = false;
			}

			_cachedPngData = null;
			_cachedPngSource = null;
			_transmittedCols = 0;
			_transmittedRows = 0;

			// A new image gets a new attempt: the old one's failure says nothing about it.
			_failure = null;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (_hasTransmittedImage)
			{
				_protocol.DeleteImage(_currentImageId);
				_hasTransmittedImage = false;
			}
		}
	}
}
