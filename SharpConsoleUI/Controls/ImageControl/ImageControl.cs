// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Displays a PixelBuffer as a half-block image in a console window.
	/// Each character cell represents 2 vertical pixels.
	/// </summary>
	public class ImageControl : BaseControl
	{
		private PixelBuffer? _source;
		private ImageScaleMode _scaleMode = ImagingDefaults.DefaultScaleMode;

		// Render cache
		private Cell[,]? _cachedCells;
		private int _cachedCols;
		private int _cachedRows;
		private PixelBuffer? _cachedSource;
		private ImageScaleMode _cachedScaleMode;
		private Color _cachedBackground;

		/// <summary>The pixel buffer to render as an image.</summary>
		public PixelBuffer? Source
		{
			get => _source;
			set
			{
				_source = value;
				OnPropertyChanged();
				InvalidateRenderCache();
				Container?.Invalidate(true);
			}
		}

		/// <summary>How the image scales to fit available space.</summary>
		public ImageScaleMode ScaleMode
		{
			get => _scaleMode;
			set
			{
				if (_scaleMode == value) return;
				_scaleMode = value;
				OnPropertyChanged();
				InvalidateRenderCache();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc />
		public override int? ContentWidth
		{
			get
			{
				if (_source == null) return 0;
				return _source.Width + Margin.Left + Margin.Right;
			}
		}

		/// <inheritdoc />
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (_source == null)
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			int cellHeight = (_source.Height + 1) / ImagingDefaults.PixelsPerCell;
			return new System.Drawing.Size(
				_source.Width + Margin.Left + Margin.Right,
				cellHeight + Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc />
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			if (_source == null)
			{
				return new LayoutSize(
					Math.Clamp(Margin.Left + Margin.Right, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(Margin.Top + Margin.Bottom, constraints.MinHeight, constraints.MaxHeight));
			}

			int naturalCellWidth = _source.Width;
			int naturalCellHeight = (_source.Height + 1) / ImagingDefaults.PixelsPerCell;

			int constraintWidth = constraints.MaxWidth - Margin.Left - Margin.Right;
			int constraintHeight = constraints.MaxHeight - Margin.Top - Margin.Bottom;

			int cellCols, cellRows;

			switch (_scaleMode)
			{
				case ImageScaleMode.None:
				{
					// Report natural size — if inside a ScrollablePanel, this enables scrollbars.
					// Layout's Math.Clamp handles bounding to the window if not scrollable.
					cellCols = naturalCellWidth;
					cellRows = naturalCellHeight;
					break;
				}
				case ImageScaleMode.Fit:
				{
					// Scale down uniformly to fit. Need bounded avail to compute scale.
					int availW = GetBoundedAvail(constraintWidth, naturalCellWidth,
						HorizontalAlignment == Layout.HorizontalAlignment.Stretch);
					int availH = GetBoundedAvail(constraintHeight, naturalCellHeight,
						VerticalAlignment == Layout.VerticalAlignment.Fill);

					double scale = Math.Min((double)availW / naturalCellWidth, (double)availH / naturalCellHeight);
					cellCols = Math.Max(1, (int)(naturalCellWidth * scale));
					cellRows = Math.Max(1, (int)(naturalCellHeight * scale));
					break;
				}
				case ImageScaleMode.Fill:
				case ImageScaleMode.Stretch:
				{
					// Claim all available bounded space.
					cellCols = GetBoundedAvail(constraintWidth, naturalCellWidth, true);
					cellRows = GetBoundedAvail(constraintHeight, naturalCellHeight, true);
					break;
				}
				default:
				{
					cellCols = GetBoundedAvail(constraintWidth, naturalCellWidth, true);
					cellRows = GetBoundedAvail(constraintHeight, naturalCellHeight, true);
					break;
				}
			}

			int width = cellCols + Margin.Left + Margin.Right;
			int height = cellRows + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc />
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			Color bgColor = Container?.BackgroundColor ?? defaultBackground;
			Color fgColor = Container?.ForegroundColor ?? defaultForeground;
			var effectiveBg = Color.Transparent;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			if (_source == null)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);
				return;
			}

			int availWidth = bounds.Width - Margin.Left - Margin.Right;
			int availHeight = bounds.Height - Margin.Top - Margin.Bottom;

			if (availWidth <= 0 || availHeight <= 0)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);
				return;
			}

			int srcCols = _source.Width;
			int srcRows = (_source.Height + 1) / ImagingDefaults.PixelsPerCell;

			if (srcCols <= 0 || srcRows <= 0)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);
				return;
			}

			// Compute render dimensions and crop offsets per scale mode.
			int renderCols, renderRows, cropOffsetX = 0, cropOffsetY = 0;

			switch (_scaleMode)
			{
				case ImageScaleMode.None:
				{
					// Natural size — no scaling. Render full image, clip in paint loop.
					renderCols = srcCols;
					renderRows = srcRows;
					break;
				}
				case ImageScaleMode.Fit:
				{
					double scale = Math.Min((double)availWidth / srcCols, (double)availHeight / srcRows);
					renderCols = Math.Max(1, (int)(srcCols * scale));
					renderRows = Math.Max(1, (int)(srcRows * scale));
					break;
				}
				case ImageScaleMode.Fill:
				{
					double scale = Math.Max((double)availWidth / srcCols, (double)availHeight / srcRows);
					renderCols = Math.Max(1, (int)Math.Ceiling(srcCols * scale));
					renderRows = Math.Max(1, (int)Math.Ceiling(srcRows * scale));
					// Center-crop the excess
					cropOffsetX = Math.Max(0, (renderCols - availWidth) / 2);
					cropOffsetY = Math.Max(0, (renderRows - availHeight) / 2);
					break;
				}
				default: // Stretch
				{
					renderCols = Math.Max(1, availWidth);
					renderRows = Math.Max(1, availHeight);
					break;
				}
			}

			// Render cells — None uses natural rendering, others use scaled rendering
			Cell[,] cells;
			if (_scaleMode == ImageScaleMode.None)
				cells = GetOrRenderNaturalCells(_source, bgColor);
			else
				cells = GetOrRenderCells(_source, renderCols, renderRows, bgColor);

			int actualW = cells.GetLength(0);
			int actualH = cells.GetLength(1);

			// Clamp everything to actual array bounds
			cropOffsetX = Math.Clamp(cropOffsetX, 0, Math.Max(0, actualW - 1));
			cropOffsetY = Math.Clamp(cropOffsetY, 0, Math.Max(0, actualH - 1));
			int displayWidth = Math.Min(actualW - cropOffsetX, availWidth);
			int displayHeight = Math.Min(actualH - cropOffsetY, availHeight);

			for (int cy = 0; cy < displayHeight && startY + cy < bounds.Bottom; cy++)
			{
				int y = startY + cy;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, effectiveBg);

				for (int cx = 0; cx < displayWidth && startX + cx < bounds.Right; cx++)
				{
					int x = startX + cx;
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetCell(x, y, cells[cropOffsetX + cx, cropOffsetY + cy]);
				}

				int contentEndX = startX + displayWidth;
				int rightPadWidth = bounds.Right - contentEndX;
				if (rightPadWidth > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(contentEndX, y, rightPadWidth, 1), fgColor, effectiveBg);
			}

			int contentEndY = startY + displayHeight;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, fgColor, effectiveBg);
		}

		/// <summary>
		/// Returns a bounded available dimension for layout. When the constraint is unbounded
		/// (e.g. ScrollablePanel passes int.MaxValue), falls back to natural size.
		/// </summary>
		private static int GetBoundedAvail(int constraint, int naturalSize, bool wantsExpand)
		{
			if (constraint <= ImagingDefaults.MaxImageDimension)
				return wantsExpand ? constraint : Math.Min(naturalSize, constraint);

			// Unbounded — use natural size (expanding into infinity makes no sense)
			return naturalSize;
		}

		private void InvalidateRenderCache()
		{
			_cachedCells = null;
		}

		/// <summary>Render at natural size using HalfBlockRenderer.Render (no scaling).</summary>
		private Cell[,] GetOrRenderNaturalCells(PixelBuffer source, Color background)
		{
			int naturalCols = source.Width;
			int naturalRows = (source.Height + 1) / ImagingDefaults.PixelsPerCell;

			if (_cachedCells != null &&
				ReferenceEquals(_cachedSource, source) &&
				_cachedCols == naturalCols &&
				_cachedRows == naturalRows &&
				_cachedScaleMode == _scaleMode &&
				_cachedBackground.Equals(background))
			{
				return _cachedCells;
			}

			_cachedCells = HalfBlockRenderer.Render(source, background);
			_cachedSource = source;
			_cachedCols = naturalCols;
			_cachedRows = naturalRows;
			_cachedScaleMode = _scaleMode;
			_cachedBackground = background;

			return _cachedCells;
		}

		/// <summary>Render at scaled dimensions using HalfBlockRenderer.RenderScaled.</summary>
		private Cell[,] GetOrRenderCells(PixelBuffer source, int cols, int rows, Color background)
		{
			if (_cachedCells != null &&
				ReferenceEquals(_cachedSource, source) &&
				_cachedCols == cols &&
				_cachedRows == rows &&
				_cachedScaleMode == _scaleMode &&
				_cachedBackground.Equals(background))
			{
				return _cachedCells;
			}

			_cachedCells = HalfBlockRenderer.RenderScaled(source, cols, rows, background);
			_cachedSource = source;
			_cachedCols = cols;
			_cachedRows = rows;
			_cachedScaleMode = _scaleMode;
			_cachedBackground = background;

			return _cachedCells;
		}
	}
}
