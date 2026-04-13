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
		private int _minimumWidth;
		private int _minimumHeight;

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

		/// <summary>
		/// Minimum measured width (in cells) for the control's layout box, including margins.
		/// Does not enlarge the drawn image — only pads the measured box. Default: 0.
		/// </summary>
		public int MinimumWidth
		{
			get => _minimumWidth;
			set
			{
				if (_minimumWidth == value) return;
				_minimumWidth = value;
				OnPropertyChanged();
				Container?.Invalidate(false);
			}
		}

		/// <summary>
		/// Minimum measured height (in cells) for the control's layout box, including margins.
		/// Does not enlarge the drawn image — only pads the measured box. Default: 0.
		/// </summary>
		public int MinimumHeight
		{
			get => _minimumHeight;
			set
			{
				if (_minimumHeight == value) return;
				_minimumHeight = value;
				OnPropertyChanged();
				Container?.Invalidate(false);
			}
		}

		/// <inheritdoc />
		/// <remarks>
		/// For <see cref="ImageScaleMode.None"/> returns the natural pixel-buffer width plus margins,
		/// so a host like ScrollablePanel can size its extent to the full image. For Fit / Fill / Stretch
		/// returns <c>null</c> because the displayed width is layout-determined.
		/// </remarks>
		public override int? ContentWidth
		{
			get
			{
				if (_source == null) return 0;
				if (_scaleMode == ImageScaleMode.None)
					return _source.Width + Margin.Left + Margin.Right;
				return null;
			}
		}

		/// <inheritdoc />
		/// <remarks>
		/// For <see cref="ImageScaleMode.None"/> returns the natural cell dimensions of the source image plus margins.
		/// For Fit / Fill / Stretch returns only the margin contribution, because the image footprint is
		/// determined by the layout pass and reporting a logical size would cause hosts (e.g. scroll
		/// containers) to over-allocate extent for an auto-scaling image.
		/// </remarks>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			if (_source == null)
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			if (_scaleMode != ImageScaleMode.None)
				return new System.Drawing.Size(Margin.Left + Margin.Right, Margin.Top + Margin.Bottom);

			int cellHeight = CellRowsFor(_source.Height);
			return new System.Drawing.Size(
				_source.Width + Margin.Left + Margin.Right,
				cellHeight + Margin.Top + Margin.Bottom);
		}

		/// <inheritdoc />
		public override IContainer? Container
		{
			get => base.Container;
			set
			{
				if (!ReferenceEquals(base.Container, value))
					InvalidateRenderCache();
				base.Container = value;
			}
		}

		/// <inheritdoc />
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			if (_source == null)
			{
				int emptyW = Math.Max(Margin.Left + Margin.Right, _minimumWidth);
				int emptyH = Math.Max(Margin.Top + Margin.Bottom, _minimumHeight);
				return new LayoutSize(
					Math.Clamp(emptyW, constraints.MinWidth, constraints.MaxWidth),
					Math.Clamp(emptyH, constraints.MinHeight, constraints.MaxHeight));
			}

			int naturalCellWidth = _source.Width;
			int naturalCellHeight = CellRowsFor(_source.Height);

			int constraintWidth = constraints.MaxWidth - Margin.Left - Margin.Right;
			int constraintHeight = constraints.MaxHeight - Margin.Top - Margin.Bottom;

			// Resolve bounded available space for Fit/Fill/Stretch; None uses natural.
			int availW = GetBoundedAvail(constraintWidth, naturalCellWidth,
				HorizontalAlignment == Layout.HorizontalAlignment.Stretch ||
				_scaleMode == ImageScaleMode.Fill || _scaleMode == ImageScaleMode.Stretch);
			int availH = GetBoundedAvail(constraintHeight, naturalCellHeight,
				VerticalAlignment == Layout.VerticalAlignment.Fill ||
				_scaleMode == ImageScaleMode.Fill || _scaleMode == ImageScaleMode.Stretch);

			var geom = ComputeRenderGeometry(availW, availH, naturalCellWidth, naturalCellHeight);

			int width = geom.cols + Margin.Left + Margin.Right;
			int height = geom.rows + Margin.Top + Margin.Bottom;

			width = Math.Max(width, _minimumWidth);
			height = Math.Max(height, _minimumHeight);

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

			// Explicit cache invalidation on background change. The render cache keys on bgColor
			// internally, but without this check a stale _cachedCells reference could be kept alive
			// across paints when bg changes (the next Get* call would miss and re-render anyway,
			// this just makes the intent obvious).
			if (_cachedCells != null && !_cachedBackground.Equals(bgColor))
				InvalidateRenderCache();

			// Intentional: margins render transparent so the window background (and any
			// Porter-Duff compositing layers beneath this control) show through. Using
			// bgColor here would opaquely paint the margin area and defeat compositing.
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
			int srcRows = CellRowsFor(_source.Height);

			if (srcCols <= 0 || srcRows <= 0)
			{
				ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);
				return;
			}

			var geom = ComputeRenderGeometry(availWidth, availHeight, srcCols, srcRows);
			int renderCols = geom.cols;
			int renderRows = geom.rows;
			int cropOffsetX = geom.cropX;
			int cropOffsetY = geom.cropY;

			// Render cells — None uses natural rendering, others use scaled rendering
			Cell[,] cells;
			if (_scaleMode == ImageScaleMode.None)
				cells = GetOrRenderNaturalCells(_source, bgColor);
			else
				cells = GetOrRenderCells(_source, renderCols, renderRows, bgColor);

			int actualW = cells.GetLength(0);
			int actualH = cells.GetLength(1);

			// Clamp crop so that availWidth / availHeight cells still fit starting from the offset.
			cropOffsetX = Math.Clamp(cropOffsetX, 0, Math.Max(0, actualW - availWidth));
			cropOffsetY = Math.Clamp(cropOffsetY, 0, Math.Max(0, actualH - availHeight));
			int displayWidth = Math.Min(actualW - cropOffsetX, availWidth);
			int displayHeight = Math.Min(actualH - cropOffsetY, availHeight);

			// TODO: Kitty graphics protocol — use GetRenderGeometry() to compute
			// exposed pixel regions and emit APC sequences instead of SetCell()
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
		/// Computes the render rectangle (inside <paramref name="bounds"/>, after margins) and
		/// crop offsets for the current source and scale mode, without painting. Intended for use
		/// by future graphics-protocol back-ends (e.g. Kitty/Sixel) that need to know the exposed
		/// pixel region of the image.
		/// </summary>
		/// <param name="bounds">The outer layout bounds assigned to this control.</param>
		/// <returns>
		/// A tuple of the render rectangle (inside margins) and the crop offsets into the rendered
		/// cell buffer. Returns an empty rect with zero offsets when the source is null or the
		/// available area is non-positive.
		/// </returns>
		protected internal (LayoutRect renderRect, int cropX, int cropY) GetRenderGeometry(LayoutRect bounds)
		{
			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int availWidth = bounds.Width - Margin.Left - Margin.Right;
			int availHeight = bounds.Height - Margin.Top - Margin.Bottom;

			if (_source == null || availWidth <= 0 || availHeight <= 0)
				return (new LayoutRect(startX, startY, 0, 0), 0, 0);

			int srcCols = _source.Width;
			int srcRows = CellRowsFor(_source.Height);
			if (srcCols <= 0 || srcRows <= 0)
				return (new LayoutRect(startX, startY, 0, 0), 0, 0);

			var geom = ComputeRenderGeometry(availWidth, availHeight, srcCols, srcRows);
			int displayWidth = Math.Min(geom.cols - geom.cropX, availWidth);
			int displayHeight = Math.Min(geom.rows - geom.cropY, availHeight);
			return (new LayoutRect(startX, startY, Math.Max(0, displayWidth), Math.Max(0, displayHeight)),
				geom.cropX, geom.cropY);
		}

		/// <summary>
		/// Single source of truth for scale-mode geometry. Returns rendered cell dimensions and
		/// (for Fill mode) the center-crop offsets into the rendered buffer. Called from both
		/// <see cref="MeasureDOM"/> and <see cref="PaintDOM"/>.
		/// </summary>
		private (int cols, int rows, int cropX, int cropY) ComputeRenderGeometry(
			int availWidth, int availHeight, int srcCols, int srcRows)
		{
			switch (_scaleMode)
			{
				case ImageScaleMode.None:
					return (srcCols, srcRows, 0, 0);

				case ImageScaleMode.Fit:
				{
					double scale = Math.Min((double)availWidth / srcCols, (double)availHeight / srcRows);
					int cols = Math.Max(1, (int)(srcCols * scale));
					int rows = Math.Max(1, (int)(srcRows * scale));
					return (cols, rows, 0, 0);
				}

				case ImageScaleMode.Fill:
				{
					double scale = Math.Max((double)availWidth / srcCols, (double)availHeight / srcRows);
					int cols = Math.Max(1, (int)Math.Ceiling(srcCols * scale));
					int rows = Math.Max(1, (int)Math.Ceiling(srcRows * scale));
					int cropX = Math.Max(0, (cols - availWidth) / 2);
					int cropY = Math.Max(0, (rows - availHeight) / 2);
					return (cols, rows, cropX, cropY);
				}

				case ImageScaleMode.Stretch:
				default:
				{
					int cols = Math.Max(1, availWidth);
					int rows = Math.Max(1, availHeight);
					return (cols, rows, 0, 0);
				}
			}
		}

		/// <summary>
		/// Converts a pixel height to the number of half-block rows required to display it.
		/// </summary>
		private static int CellRowsFor(int pixelHeight)
			=> (pixelHeight + ImagingDefaults.PixelsPerCell - 1) / ImagingDefaults.PixelsPerCell;

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

		/// <summary>
		/// Drops the cached rendered cells. The next <see cref="PaintDOM"/> will re-render
		/// via <see cref="HalfBlockRenderer"/>. Callers that change external state which affects
		/// rendering (for example, swapping the theme that drives the container background) should
		/// invoke this to guarantee the cache is rebuilt.
		/// </summary>
		public void InvalidateImageCache()
		{
			_cachedCells = null;
		}

		// Private alias kept so existing internal call sites need not change.
		private void InvalidateRenderCache() => InvalidateImageCache();

		/// <summary>Render at natural size using HalfBlockRenderer.Render (no scaling).</summary>
		private Cell[,] GetOrRenderNaturalCells(PixelBuffer source, Color background)
		{
			int naturalCols = source.Width;
			int naturalRows = CellRowsFor(source.Height);

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
