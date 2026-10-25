// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Surfaces;

/// <summary>
/// Reads typed colour and decorations back out of an emitted SGR escape.
/// </summary>
/// <remarks>
/// <para>
/// The composed console buffer stores the escape the renderer emitted rather than the values
/// behind it, so reading it back means parsing. This is the one place that happens: every
/// consumer of a composed surface sees typed colour and never an escape.
/// </para>
/// <para>
/// Hand-parsed rather than regex. It runs per cell over a whole screen, and the grammar the
/// renderer emits is fixed and tiny.
/// </para>
/// </remarks>
internal static class AnsiColor
{
	/// <summary>
	/// Reads the background out of an escape.
	/// </summary>
	/// <param name="ansi">The emitted escape.</param>
	/// <param name="fallback">Used when the escape says "terminal default", or says nothing.</param>
	/// <returns>The colour, and whether it was substituted rather than measured.</returns>
	/// <remarks>
	/// Only <c>48;2;r;g;b</c> carries a real colour. <c>49</c> means terminal-default, which
	/// this process genuinely does not know, so it is reported as substituted rather than
	/// passed off as a measurement.
	/// </remarks>
	internal static (Color Color, bool WasDefaulted) ParseBackground(string? ansi, Color fallback)
	{
		if (string.IsNullOrEmpty(ansi)) return (fallback, true);

		int i = ansi.IndexOf("48;2;", StringComparison.Ordinal);
		if (i < 0) return (fallback, true);

		i += 5;
		return TryByte(ansi, ref i, out byte r)
			&& TryByte(ansi, ref i, out byte g)
			&& TryByte(ansi, ref i, out byte b)
				? (new Color(r, g, b), false)
				: (fallback, true);
	}

	/// <summary>
	/// Reads the foreground out of an escape.
	/// </summary>
	/// <param name="ansi">The emitted escape.</param>
	/// <param name="fallback">Used when the escape says "terminal default", or says nothing.</param>
	/// <returns>The colour.</returns>
	/// <remarks>
	/// A defaulted foreground is not reported separately. Unlike a background - where a wrong
	/// guess repaints whole regions - it only affects glyph ink, so the theme's window
	/// foreground is a safe stand-in.
	/// </remarks>
	internal static Color ParseForeground(string? ansi, Color fallback)
	{
		if (string.IsNullOrEmpty(ansi)) return fallback;

		int i = ansi.IndexOf("38;2;", StringComparison.Ordinal);
		if (i < 0) return fallback;

		i += 5;
		return TryByte(ansi, ref i, out byte r)
			&& TryByte(ansi, ref i, out byte g)
			&& TryByte(ansi, ref i, out byte b)
				? new Color(r, g, b)
				: fallback;
	}

	/// <summary>
	/// Reads the decoration attributes out of an escape.
	/// </summary>
	/// <param name="ansi">The emitted escape.</param>
	/// <returns>The decorations the escape sets.</returns>
	/// <remarks>
	/// Scans for the single-number SGR attributes, skipping the <c>38;2;</c> and <c>48;2;</c>
	/// colour runs - whose r/g/b components would otherwise be misread as attributes, since a
	/// green value of 1 is indistinguishable from "bold" without tracking the run.
	/// </remarks>
	internal static TextDecoration ParseDecorations(string? ansi)
	{
		if (string.IsNullOrEmpty(ansi)) return TextDecoration.None;

		var result = TextDecoration.None;

		for (int i = 0; i < ansi.Length;)
		{
			// Step over anything that is not a digit (ESC, '[', 'm', ';').
			if (ansi[i] < '0' || ansi[i] > '9') { i++; continue; }

			int start = i;
			int n = 0;
			while (i < ansi.Length && ansi[i] >= '0' && ansi[i] <= '9')
			{
				n = n * 10 + (ansi[i] - '0');
				if (n > 9999) n = 9999;   // saturate; nothing we read is this large
				i++;
			}

			// A truecolour introducer owns the next three numbers. Skip them so an r/g/b
			// component is never mistaken for an attribute.
			if ((n == 38 || n == 48) && Peek(ansi, i) == 2)
			{
				SkipNumbers(ansi, ref i, 4);   // the '2', then r, g, b
				continue;
			}

			result |= n switch
			{
				1 => TextDecoration.Bold,
				2 => TextDecoration.Dim,
				3 => TextDecoration.Italic,
				4 => TextDecoration.Underline,
				5 => TextDecoration.Blink,
				7 => TextDecoration.Invert,
				9 => TextDecoration.Strikethrough,
				_ => TextDecoration.None,
			};

			if (i == start) i++;   // defensive: never fail to advance
		}

		return result;
	}

	/// <summary>Reads the next number without consuming it, or -1.</summary>
	private static int Peek(string s, int i)
	{
		while (i < s.Length && (s[i] == ';' || s[i] == '[')) i++;
		if (i >= s.Length || s[i] < '0' || s[i] > '9') return -1;

		int n = 0;
		while (i < s.Length && s[i] >= '0' && s[i] <= '9')
		{
			n = n * 10 + (s[i] - '0');
			if (n > 9999) return 9999;
			i++;
		}
		return n;
	}

	/// <summary>Consumes up to <paramref name="count"/> separated numbers.</summary>
	private static void SkipNumbers(string s, ref int i, int count)
	{
		for (int k = 0; k < count; k++)
		{
			while (i < s.Length && (s[i] == ';')) i++;
			if (i >= s.Length || s[i] < '0' || s[i] > '9') return;
			while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
		}
	}

	/// <summary>Reads one decimal byte, then steps over a single separator.</summary>
	private static bool TryByte(string s, ref int i, out byte value)
	{
		value = 0;
		int start = i;
		int n = 0;

		while (i < s.Length && s[i] >= '0' && s[i] <= '9')
		{
			n = n * 10 + (s[i] - '0');
			if (n > 255) return false;
			i++;
		}

		if (i == start) return false;
		value = (byte)n;

		if (i < s.Length && (s[i] == ';' || s[i] == 'm')) i++;
		return true;
	}
}
