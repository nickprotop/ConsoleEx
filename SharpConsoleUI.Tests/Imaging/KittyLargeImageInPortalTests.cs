// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

/// <summary>
/// A large ImageControl painted inside a portal, in Kitty placeholder mode.
/// </summary>
/// <remarks>
/// Reported from cxagent: an image drew at 35x10 cells and not at 216x61. Every placeholder cell
/// must carry a row diacritic AND a column diacritic; a cell with only one, or none, is not a
/// reference the terminal can resolve, and the image silently does not appear.
/// </remarks>
[Collection("TimingSensitive")]
public class KittyLargeImageInPortalTests
{
	/// <summary>A portal that hosts a child through Content, the shape cxagent's preview uses.</summary>
	private sealed class TestPortal : PortalContentBase
	{
		private readonly Rectangle _bounds;

		public TestPortal(Rectangle bounds, IWindowControl content)
		{
			_bounds = bounds;
			Content = content;
		}

		public override Rectangle GetPortalBounds() => _bounds;

		public override bool ProcessMouseEvent(MouseEventArgs args) => ProcessHostedMouseEvent(args);

		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{ }
	}

	/// <summary>
	/// A headless driver that claims Kitty graphics support, so <c>ImageControl.ResolveRenderer</c>
	/// chooses the Kitty placeholder renderer instead of the half-block fallback.
	/// </summary>
	/// <remarks>
	/// The stock <see cref="HeadlessConsoleDriver"/> does not implement <c>IGraphicsProtocol</c> at
	/// all, so no headless test could reach the Kitty path before this. That is why the reported
	/// bug had no covering test.
	/// </remarks>
	private sealed class KittyHeadlessDriver : HeadlessConsoleDriver, IGraphicsProtocol
	{
		public KittyHeadlessDriver(int width, int height) : base(width, height) { }

		public List<(uint id, int columns, int rows)> Transmitted { get; } = new();

		public bool SupportsKittyGraphics => true;

		public void TransmitImage(uint imageId, byte[] pngData, int columns, int rows)
			=> Transmitted.Add((imageId, columns, rows));

		public void DeleteImage(uint imageId) { }

		public void TransmitRawRgb(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight, int columns, int rows) { }

		public void UpdateRawRgbFrame(uint imageId, byte[] rgbData, int pixelWidth, int pixelHeight) { }
	}

	private static PixelBuffer Picture(int w, int h)
	{
		var pixels = new PixelBuffer(w, h);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				pixels.SetPixel(x, y, new ImagePixel((byte)(x % 256), (byte)(y % 256), 90));
		return pixels;
	}

	/// <summary>
	/// Paints the portal twice: the first paint starts the async PNG encode and shows the loading
	/// placeholder, the second writes the real placeholder cells.
	/// </summary>
	private static CharacterBuffer PaintPortal(int cols, int rows)
	{
		// A real window system, so the portal's container chain resolves to a Kitty-capable driver.
		var driver = new KittyHeadlessDriver(cols, rows);
		var system = new ConsoleWindowSystem(driver);
		var window = new Window(system) { Left = 0, Top = 0, Width = cols, Height = rows };
		system.AddWindow(window);

		var image = new ImageControl
		{
			Source = Picture(cols * 2, rows * 4),
			ScaleMode = ImageScaleMode.Fit
		};

		var portal = new TestPortal(new Rectangle(0, 0, cols, rows), image);

		// The portal hangs off the window, which is what gives the image a route to the driver.
		window.AddControl(portal);

		var buffer = new CharacterBuffer(cols, rows, Color.Black);
		var rect = new LayoutRect(0, 0, cols, rows);

		portal.PaintDOM(buffer, rect, rect, Color.White, Color.Black);
		Thread.Sleep(800);
		portal.PaintDOM(buffer, rect, rect, Color.White, Color.Black);

		return buffer;
	}

	/// <summary>
	/// Every cell of a placeholder row carries both diacritics, not just the first.
	/// </summary>
	/// <remarks>
	/// The reported symptom was exactly this: each row's first cell was correct and the rest were
	/// bare U+10EEEE, which the terminal cannot resolve to an image position.
	/// </remarks>
	[Theory]
	[InlineData(35, 10)]
	[InlineData(216, 61)]
	public void EveryPlaceholderCellCarriesBothDiacritics(int cols, int rows)
	{
		var buffer = PaintPortal(cols, rows);

		int placeholders = 0, missingCombiners = 0, shortCombiners = 0;

		for (int y = 0; y < rows; y++)
		{
			for (int x = 0; x < cols; x++)
			{
				var cell = buffer.GetCell(x, y);
				if (cell.Character != ImagingDefaults.KittyPlaceholder)
					continue;

				placeholders++;
				if (string.IsNullOrEmpty(cell.Combiners))
					missingCombiners++;
				else if (new System.Globalization.StringInfo(cell.Combiners).LengthInTextElements < 2
					&& cell.Combiners!.Length < 2)
					shortCombiners++;
			}
		}

		Assert.True(placeholders > 0, "no placeholder cells were painted at all");
		Assert.Equal(0, missingCombiners);
		Assert.Equal(0, shortCombiners);
	}

	/// <summary>
	/// The column diacritics across a row are all different: a row whose cells all claim column 0
	/// is the same failure as a bare cell, and would pass a per-cell presence check.
	/// </summary>
	[Theory]
	[InlineData(35, 10)]
	[InlineData(216, 61)]
	public void ColumnDiacriticsAdvanceAcrossARow(int cols, int rows)
	{
		var buffer = PaintPortal(cols, rows);

		int checkedRows = 0;

		for (int y = 0; y < rows; y++)
		{
			var seen = new List<string>();
			for (int x = 0; x < cols; x++)
			{
				var cell = buffer.GetCell(x, y);
				if (cell.Character != ImagingDefaults.KittyPlaceholder) continue;
				if (string.IsNullOrEmpty(cell.Combiners)) continue;
				seen.Add(cell.Combiners!);
			}

			if (seen.Count < 2) continue;
			checkedRows++;

			Assert.Equal(seen.Count, seen.Distinct().Count());
		}

		Assert.True(checkedRows > 0, "no row had two or more placeholder cells to compare");
	}
}
