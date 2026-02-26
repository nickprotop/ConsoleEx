// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Built-in gutter renderer that displays source line numbers.
	/// Highlights the current line number using the control's foreground color.
	/// </summary>
	public class LineNumberGutterRenderer : IGutterRenderer
	{
		private Color? _lineNumberColor;

		/// <summary>
		/// Gets or sets the foreground color for line numbers.
		/// When null, defaults to <see cref="Color.Grey"/>.
		/// The current-line number always uses the context's foreground color for emphasis.
		/// </summary>
		public Color LineNumberColor
		{
			get => _lineNumberColor ?? Color.Grey;
			set => _lineNumberColor = value;
		}

		/// <summary>
		/// Gets or sets the right-side padding in columns after the line number digits.
		/// Defaults to <see cref="ControlDefaults.LineNumberGutterPadding"/>.
		/// </summary>
		public int Padding { get; set; } = ControlDefaults.LineNumberGutterPadding;

		/// <inheritdoc/>
		public int GetWidth(int totalLineCount)
		{
			int digits = Math.Max(1, (int)Math.Floor(Math.Log10(Math.Max(1, totalLineCount))) + 1);
			return digits + Padding;
		}

		/// <inheritdoc/>
		public void Render(in GutterRenderContext context, int width)
		{
			Color gutterFg = context.IsCursorLine ? context.ForegroundColor : LineNumberColor;
			Color gutterBg = context.BackgroundColor;

			string gutterText;
			if (context.SourceLineIndex >= 0 && context.IsFirstWrappedSegment)
			{
				gutterText = (context.SourceLineIndex + 1).ToString()
					.PadLeft(width - Padding)
					.PadRight(width);
			}
			else
			{
				gutterText = new string(' ', width);
			}

			for (int g = 0; g < width; g++)
			{
				int cellX = context.X + g;
				context.Buffer.SetCell(cellX, context.Y, gutterText[g], gutterFg, gutterBg);
			}
		}
	}
}
