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

			// Alignment determines how much space the CONTROL claims from the layout.
			// ImageScaleMode determines how the IMAGE fits within the control's space.
			bool stretchH = HorizontalAlignment == Layout.HorizontalAlignment.Stretch;
			bool fillV = VerticalAlignment == Layout.VerticalAlignment.Fill;

			// Determine whether the scale mode inherently wants to expand.
			// Fill/Stretch use available constraint space; Fit/None use natural size.
			bool expandH = stretchH || _scaleMode == ImageScaleMode.Fill || _scaleMode == ImageScaleMode.Stretch;
			bool expandV = fillV || _scaleMode == ImageScaleMode.Fill || _scaleMode == ImageScaleMode.Stretch;

			int constraintWidth = constraints.MaxWidth - Margin.Left - Margin.Right;
			int constraintHeight = constraints.MaxHeight - Margin.Top - Margin.Bottom;

			// Guard against unbounded constraints (scrollable layout passes int.MaxValue).
			int availWidth, availHeight;
			if (expandH && constraintWidth <= ImagingDefaults.MaxImageDimension)
				availWidth = constraintWidth;
			else if (expandH)
				availWidth = ImagingDefaults.MaxImageDimension;
			else
				availWidth = Math.Min(naturalCellWidth, constraintWidth);

			if (expandV && constraintHeight <= ImagingDefaults.MaxImageDimension)
				availHeight = constraintHeight;
			else if (expandV)
				availHeight = ImagingDefaults.MaxImageDimension;
			else
				availHeight = Math.Min(naturalCellHeight, constraintHeight);

			ComputeScaledDimensions(_source, availWidth, availHeight, _scaleMode,
				out int cellCols, out int cellRows);

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
			bool preserveBg = Container?.HasGradientBackground ?? false;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor, preserveBg);

			if (_source == null)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, bgColor, preserveBg);
				return;
			}

			int availWidth = bounds.Width - Margin.Left - Margin.Right;
			int availHeight = bounds.Height - Margin.Top - Margin.Bottom;

			if (availWidth <= 0 || availHeight <= 0)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, bgColor, preserveBg);
				return;
			}

			ComputeScaledDimensions(_source, availWidth, availHeight, _scaleMode,
				out int cellCols, out int cellRows);

			var cells = GetOrRenderCells(_source, cellCols, cellRows, bgColor);

			int actualCellWidth = cells.GetLength(0);
			int actualCellHeight = cells.GetLength(1);

			for (int cy = 0; cy < actualCellHeight && startY + cy < bounds.Bottom; cy++)
			{
				int y = startY + cy;
				if (y < clipRect.Y || y >= clipRect.Bottom) continue;

				// Fill left margin
				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, y, Margin.Left, 1), fgColor, bgColor, preserveBg);

				// Paint image cells
				for (int cx = 0; cx < actualCellWidth && startX + cx < bounds.Right; cx++)
				{
					int x = startX + cx;
					if (x < clipRect.X || x >= clipRect.Right) continue;
					buffer.SetCell(x, y, cells[cx, cy]);
				}

				// Fill right padding
				int contentEndX = startX + actualCellWidth;
				int rightPadWidth = bounds.Right - contentEndX;
				if (rightPadWidth > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(contentEndX, y, rightPadWidth, 1), fgColor, bgColor, preserveBg);
			}

			int contentEndY = startY + actualCellHeight;
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, fgColor, bgColor, preserveBg);
		}

		private void InvalidateRenderCache()
		{
			_cachedCells = null;
		}

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

		private static void ComputeScaledDimensions(PixelBuffer source, int availCols, int availRows,
			ImageScaleMode mode, out int cols, out int rows)
		{
			int srcCols = source.Width;
			int srcRows = (source.Height + 1) / ImagingDefaults.PixelsPerCell;

			if (srcCols <= 0 || srcRows <= 0)
			{
				cols = 0;
				rows = 0;
				return;
			}

			switch (mode)
			{
				case ImageScaleMode.Fit:
				{
					double scaleX = (double)availCols / srcCols;
					double scaleY = (double)availRows / srcRows;
					double scale = Math.Min(scaleX, scaleY);
					cols = Math.Max(1, (int)(srcCols * scale));
					rows = Math.Max(1, (int)(srcRows * scale));
					break;
				}
				case ImageScaleMode.Fill:
				{
					double scaleX = (double)availCols / srcCols;
					double scaleY = (double)availRows / srcRows;
					double scale = Math.Max(scaleX, scaleY);
					cols = Math.Min(availCols, Math.Max(1, (int)(srcCols * scale)));
					rows = Math.Min(availRows, Math.Max(1, (int)(srcRows * scale)));
					break;
				}
				case ImageScaleMode.Stretch:
					cols = Math.Max(1, availCols);
					rows = Math.Max(1, availRows);
					break;
				case ImageScaleMode.None:
					cols = Math.Min(srcCols, availCols);
					rows = Math.Min(srcRows, availRows);
					break;
				default:
					cols = Math.Max(1, availCols);
					rows = Math.Max(1, availRows);
					break;
			}
		}
	}
}
