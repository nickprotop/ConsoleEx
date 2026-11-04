// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Imaging;

/// <summary>
/// What the image area shows when the image cannot be drawn.
/// </summary>
/// <remarks>
/// Both failures here used to leave the area blank with nothing saying why, which is
/// indistinguishable from a bug in the terminal, the protocol or the layout.
/// </remarks>
[Collection("TimingSensitive")]
public class KittyImageFailureTests
{
	private sealed class RecordingProtocol : IGraphicsProtocol
	{
		public bool SupportsKittyGraphics => true;
		public List<(uint id, int cols, int rows)> Transmitted { get; } = new();

		public void TransmitImage(uint imageId, byte[] pngData, int columns, int rows)
			=> Transmitted.Add((imageId, columns, rows));

		public void DeleteImage(uint imageId) { }
	}

	private static PixelBuffer Picture(int w = 40, int h = 40)
	{
		var pixels = new PixelBuffer(w, h);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				pixels.SetPixel(x, y, new ImagePixel(10, 200, 30));
		return pixels;
	}

	/// <summary>Reads a buffer row back as text, so an assertion can look for a message.</summary>
	private static string RowText(CharacterBuffer buffer, int y, int width)
	{
		var sb = new StringBuilder();
		for (int x = 0; x < width; x++)
			sb.Append(buffer.GetCell(x, y).Character.ToString());
		return sb.ToString();
	}

	private static string AllText(CharacterBuffer buffer, int width, int height)
		=> string.Join("\n", Enumerable.Range(0, height).Select(y => RowText(buffer, y, width)));

	/// <summary>
	/// Paints until the async PNG encode has settled, so the result is the steady state rather
	/// than the loading frame.
	/// </summary>
	private static void PaintUntilSettled(
		KittyImageRenderer renderer, CharacterBuffer buffer, LayoutRect rect,
		PixelBuffer source, int renderCols, int renderRows)
	{
		for (int attempt = 0; attempt < 40; attempt++)
		{
			renderer.Paint(buffer, rect, rect, source, ImageScaleMode.Fit,
				0, 0, renderCols, renderRows, Color.Black);

			string text = AllText(buffer, rect.Width, rect.Height);
			if (!text.Contains("Loading", StringComparison.Ordinal))
				return;

			Thread.Sleep(50);
		}
	}

	#region A placement the protocol will refuse

	/// <summary>
	/// Over the cell limit the protocol returns no chunks at all, so nothing is transmitted. The
	/// area must say so rather than painting placeholders for an image the terminal never got.
	/// </summary>
	[Fact]
	public void APlacementOverTheCellLimit_SaysSo_AndTransmitsNothing()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);
		var source = Picture();

		const int width = 60, height = 7;
		var buffer = new CharacterBuffer(width, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		int tooMany = ImagingDefaults.KittyMaxPlacementCells + 1;
		PaintUntilSettled(renderer, buffer, rect, source, tooMany, 10);

		string text = AllText(buffer, width, height);

		Assert.Contains("too large", text, StringComparison.OrdinalIgnoreCase);
		Assert.Empty(protocol.Transmitted);
	}

	/// <summary>
	/// No placeholder cells are written for a refused placement: a placeholder refers to an image
	/// the terminal holds, and it holds none.
	/// </summary>
	[Fact]
	public void ARefusedPlacement_WritesNoPlaceholderCells()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);
		var source = Picture();

		const int width = 60, height = 7;
		var buffer = new CharacterBuffer(width, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		PaintUntilSettled(renderer, buffer, rect, source,
			ImagingDefaults.KittyMaxPlacementCells + 1, 10);

		int placeholders = 0;
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				if (buffer.GetCell(x, y).Character == ImagingDefaults.KittyPlaceholder)
					placeholders++;

		Assert.Equal(0, placeholders);
	}

	/// <summary>
	/// A legal placement still transmits and still draws. The guard must not have moved the
	/// working case.
	/// </summary>
	[Fact]
	public void APlacementWithinTheLimit_StillTransmits()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);
		var source = Picture();

		const int width = 40, height = 10;
		var buffer = new CharacterBuffer(width, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		PaintUntilSettled(renderer, buffer, rect, source, width, height);

		Assert.Single(protocol.Transmitted);

		int placeholders = 0;
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
				if (buffer.GetCell(x, y).Character == ImagingDefaults.KittyPlaceholder)
					placeholders++;

		Assert.True(placeholders > 0, "a legal placement should write placeholder cells");
	}

	/// <summary>
	/// A refused placement is not remembered as transmitted, so shrinking to a legal size
	/// recovers instead of staying failed.
	/// </summary>
	[Fact]
	public void AfterARefusal_ALegalSizeRecovers()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);
		var source = Picture();

		const int width = 60, height = 7;
		var buffer = new CharacterBuffer(width, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		PaintUntilSettled(renderer, buffer, rect, source,
			ImagingDefaults.KittyMaxPlacementCells + 1, 10);
		Assert.Empty(protocol.Transmitted);

		// Now a size the protocol accepts.
		PaintUntilSettled(renderer, buffer, rect, source, 20, 5);

		Assert.Single(protocol.Transmitted);
		string text = AllText(buffer, width, height);
		Assert.DoesNotContain("too large", text, StringComparison.OrdinalIgnoreCase);
	}

	#endregion

	#region The encode latch

	/// <summary>
	/// The encode runs on a background task. Every exit from it must clear the in-progress latch:
	/// left set, the control never retries and never paints, and the exception is swallowed by an
	/// unobserved task, so the image area stays blank forever with no reason given.
	/// </summary>
	/// <remarks>
	/// Driven through a source the encoder genuinely cannot read, so the real catch runs rather
	/// than a seam standing in for it. The buffer is told it is far wider than its backing array,
	/// which is what an encoder walking it trips over. No ordinary image does this; it is the
	/// cheapest honest way to make the encode throw.
	/// </remarks>
	[Fact]
	public void AnEncodeThatThrows_ClearsTheLatch_AndReportsTheFailure()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);

		var source = new PixelBuffer(4, 4);
		typeof(PixelBuffer).GetProperty(nameof(PixelBuffer.Width))!.SetValue(source, 1 << 20);

		const int width = 60, height = 7;
		var buffer = new CharacterBuffer(width, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		for (int attempt = 0; attempt < 40; attempt++)
		{
			renderer.Paint(buffer, rect, rect, source, ImageScaleMode.Fit,
				0, 0, width, height, Color.Black);

			if (!AllText(buffer, width, height).Contains("Loading", StringComparison.Ordinal))
				break;

			Thread.Sleep(50);
		}

		string text = AllText(buffer, width, height);

		// Stuck on "Loading" forever is exactly what a latched _isEncoding looks like.
		Assert.DoesNotContain("Loading", text, StringComparison.Ordinal);
		Assert.Contains("failed to encode", text, StringComparison.OrdinalIgnoreCase);
		Assert.Empty(protocol.Transmitted);
	}

	#endregion

	#region The message itself

	/// <summary>
	/// A message wider than the area is cut to fit rather than spilling past it into whatever is
	/// drawn beside the image.
	/// </summary>
	[Fact]
	public void ALongMessage_StaysInsideTheArea()
	{
		var protocol = new RecordingProtocol();
		var renderer = new KittyImageRenderer(protocol);
		var source = Picture();

		// Narrower than the failure message, which names two numbers and a limit.
		const int width = 12, height = 5;
		var buffer = new CharacterBuffer(width + 6, height, Color.Black);
		var rect = new LayoutRect(0, 0, width, height);

		PaintUntilSettled(renderer, buffer, rect, source,
			ImagingDefaults.KittyMaxPlacementCells + 1, 10);

		// The columns beyond the image area must be untouched background.
		for (int y = 0; y < height; y++)
			for (int x = width; x < width + 6; x++)
				Assert.Equal(new System.Text.Rune(' '), buffer.GetCell(x, y).Character);
	}

	#endregion
}
