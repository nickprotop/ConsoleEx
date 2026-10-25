// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Surfaces;

/// <summary>
/// Obtains an <see cref="ICellSurface"/> from any of the library's cell sources.
/// </summary>
/// <remarks>
/// Each adapter is a view over storage that already exists, so obtaining a surface copies
/// nothing. The cost is paid per cell read, and only for the cells actually read.
/// </remarks>
public static class CellSurface
{
	/// <summary>
	/// A surface over a window's painted buffer.
	/// </summary>
	/// <param name="window">The window.</param>
	/// <returns>A live view of the window's buffer.</returns>
	/// <exception cref="InvalidOperationException">The window has not painted yet.</exception>
	/// <remarks>
	/// Needs no diagnostics: a window's buffer exists because the renderer needs it. This is
	/// the cheap, always-available case.
	/// </remarks>
	public static ICellSurface CaptureSurface(this Window window)
	{
		ArgumentNullException.ThrowIfNull(window);

		var buffer = window.Buffer
			?? throw new InvalidOperationException(
				$"window '{window.Title}' has not painted yet, so it has no buffer to read. "
				+ "Render at least one frame before capturing it.");

		return new CharacterBufferSurface(buffer);
	}

	/// <summary>A surface over an immutable buffer snapshot.</summary>
	/// <param name="snapshot">The snapshot.</param>
	/// <returns>A view of the snapshot.</returns>
	/// <remarks>
	/// Capture now, export later: the snapshot is a deep copy, so it survives repaints and
	/// can be exported after an await without racing the renderer.
	/// </remarks>
	public static ICellSurface AsSurface(this BufferSnapshot snapshot) =>
		new BufferSnapshotSurface(snapshot);

	/// <summary>A surface over a diagnostics character-buffer snapshot.</summary>
	/// <param name="snapshot">The snapshot.</param>
	/// <returns>A view of the snapshot.</returns>
	public static ICellSurface AsSurface(this CharacterBufferSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		return new CharacterBufferSnapshotSurface(snapshot);
	}

	/// <summary>
	/// A surface over the renderer's console-buffer snapshot - the composed desktop.
	/// </summary>
	/// <param name="snapshot">The snapshot.</param>
	/// <param name="theme">Supplies colours for cells the source recorded as "terminal default".</param>
	/// <param name="back">True for the back buffer (what is about to be shown), false for the front.</param>
	/// <returns>A view of the snapshot, with ANSI parsed to typed colour.</returns>
	/// <remarks>
	/// This is the only source whose colour is stored as an escape rather than a value, and
	/// the only one that can report <see cref="SurfaceCell.BackgroundWasDefaulted"/>.
	/// </remarks>
	public static ICellSurface AsSurface(this ConsoleBufferSnapshot snapshot, ITheme theme, bool back = true)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(theme);
		return new ConsoleBufferSnapshotSurface(snapshot, theme, back);
	}

	/// <summary>
	/// A surface over the whole desktop, as the user sees it: every window, composed.
	/// </summary>
	/// <param name="system">The window system.</param>
	/// <returns>A view of the most recent composed frame.</returns>
	/// <exception cref="InvalidOperationException">
	/// The application did not opt into diagnostics recording, so no composed frame exists.
	/// </exception>
	/// <remarks>
	/// Unlike <see cref="CaptureSurface(Window)"/>, this REQUIRES
	/// <c>ConsoleWindowSystemOptions.EnableDiagnostics</c>. Per-window buffers exist because
	/// the renderer needs them; the composed result is only retained when diagnostics records
	/// it. Capturing a window needs nothing.
	/// </remarks>
	public static ICellSurface ComposedSurface(this ConsoleWindowSystem system)
	{
		ArgumentNullException.ThrowIfNull(system);

		var shot = system.RenderingDiagnostics?.LastConsoleSnapshot
			?? throw new InvalidOperationException(
				"no composed frame: this application runs with EnableDiagnostics off, or "
				+ "without the ConsoleBuffer layer, so the renderer retains nothing. Capture a "
				+ "window instead - window.CaptureSurface() - which needs no diagnostics. To enable "
				+ "composed capture, start the app with "
				+ "ConsoleWindowSystemOptions.EnableDiagnostics: true.");

		return shot.AsSurface(system.Theme);
	}

	#region Implementations

	private sealed class CharacterBufferSurface(CharacterBuffer buffer) : ICellSurface
	{
		public int Width => buffer.Width;

		public int Height => buffer.Height;

		public SurfaceCell this[int x, int y] => FromCell(buffer.GetCell(x, y));
	}

	private sealed class BufferSnapshotSurface(BufferSnapshot snapshot) : ICellSurface
	{
		public int Width => snapshot.Width;

		public int Height => snapshot.Height;

		public SurfaceCell this[int x, int y] => FromCell(snapshot.GetCell(x, y));
	}

	private sealed class CharacterBufferSnapshotSurface(CharacterBufferSnapshot snapshot) : ICellSurface
	{
		public int Width => snapshot.Width;

		public int Height => snapshot.Height;

		public SurfaceCell this[int x, int y] => FromCell(snapshot.GetCell(x, y));
	}

	private sealed class ConsoleBufferSnapshotSurface : ICellSurface
	{
		private readonly ConsoleBufferSnapshot _snapshot;
		private readonly bool _back;
		private readonly Color _fallbackBg;
		private readonly Color _fallbackFg;

		internal ConsoleBufferSnapshotSurface(ConsoleBufferSnapshot snapshot, ITheme theme, bool back)
		{
			_snapshot = snapshot;
			_back = back;
			_fallbackBg = theme.DesktopBackgroundColor ?? theme.WindowBackgroundColor;
			_fallbackFg = theme.WindowForegroundColor;
		}

		public int Width => _snapshot.Width;

		public int Height => _snapshot.Height;

		public SurfaceCell this[int x, int y]
		{
			get
			{
				var raw = _back ? _snapshot.GetBack(x, y) : _snapshot.GetFront(x, y);
				var (bg, defaulted) = AnsiColor.ParseBackground(raw.AnsiEscape, _fallbackBg);

				return new SurfaceCell(
					raw.Character,
					AnsiColor.ParseForeground(raw.AnsiEscape, _fallbackFg),
					bg,
					AnsiColor.ParseDecorations(raw.AnsiEscape),
					raw.IsWideContinuation,
					raw.Combiners,
					defaulted);
			}
		}
	}

	/// <summary>Projects a typed cell, whose colours are always known.</summary>
	private static SurfaceCell FromCell(Cell c) => new(
		c.Character,
		c.Foreground,
		c.Background,
		c.Decorations,
		c.IsWideContinuation,
		c.Combiners,
		BackgroundWasDefaulted: false);

	#endregion
}
