// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers.Input;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Provides double-buffered console rendering with ANSI escape sequence support.
	/// </summary>
	/// <remarks>
	/// This class maintains a front buffer (what is currently displayed) and a back buffer
	/// (what will be rendered next). Only changed cells are written to the console,
	/// optimizing rendering performance by minimizing console output operations.
	/// </remarks>
	public class ConsoleBuffer
	{
		private const string CursorForward = "\u001b[1C";
		private const string ResetSequence = "\u001b[0m";

		// Cached regex for better performance
		private static readonly Regex _ansiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private readonly Cell[,] _backBuffer;
		private readonly Cell[,] _frontBuffer;
		private readonly int _height;

		// Reusable StringBuilder for screen output
		private readonly StringBuilder _screenBuilder = new(8192);

		// Reusable StringBuilder for FormatCellAnsi to avoid allocations
		private readonly StringBuilder _formatBuilder = new(64);

		private readonly int _width;
		private readonly object _consoleLock; // Shared lock for thread-safe Console I/O
		private readonly Configuration.ConsoleWindowSystemOptions _options;

		// Pooled list to avoid per-line allocations during rendering
		private readonly List<(int startX, int endX)> _dirtyRegionsPool = new List<(int, int)>();

		// Diagnostics support (optional, for testing and debugging)
		private Diagnostics.RenderingDiagnostics? _diagnostics;

		/// <summary>
		/// Gets or sets the diagnostics system for capturing rendering metrics.
		/// </summary>
		public Diagnostics.RenderingDiagnostics? Diagnostics
		{
			get => _diagnostics;
			set => _diagnostics = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleBuffer"/> class with the specified dimensions.
		/// </summary>
		/// <param name="width">The width of the buffer in characters.</param>
		/// <param name="height">The height of the buffer in lines.</param>
		/// <param name="consoleLock">Optional shared lock for thread-safe Console I/O operations.</param>
		/// <param name="options">Optional configuration options for buffer behavior.</param>
		public ConsoleBuffer(int width, int height, Configuration.ConsoleWindowSystemOptions? options = null, object? consoleLock = null)
		{
			_width = width;
			_height = height;
			_consoleLock = consoleLock ?? new object();
			_options = options ?? Configuration.ConsoleWindowSystemOptions.Default;
			_backBuffer = new Cell[width, height];
			_frontBuffer = new Cell[width, height];

			InitializeBuffers();
		}

		/// <summary>
		/// Gets or sets a value indicating whether rendering is locked.
		/// </summary>
		/// <value>
		/// <c>true</c> if rendering should be skipped during <see cref="Render"/>; otherwise, <c>false</c>.
		/// </value>
		/// <remarks>
		/// This property is used to prevent rendering during buffer resizing operations.
		/// </remarks>
		public bool Lock { get; set; } = false;

		// Cache for FormatCellAnsi to avoid repeated string allocations
		private string _lastCellAnsi = string.Empty;
		private Color _lastCellFg;
		private Color _lastCellBg;
		private TextDecoration _lastCellDecorations;

		/// <summary>
		/// Formats an ANSI escape sequence for the given foreground color, background color,
		/// and text decorations. Caches the last result for consecutive cells with identical
		/// attributes (common case).
		/// </summary>
		private string FormatCellAnsi(Color fg, Color bg, TextDecoration decorations = TextDecoration.None)
		{
			if (fg.Equals(_lastCellFg) && bg.Equals(_lastCellBg) && decorations == _lastCellDecorations)
				return _lastCellAnsi;

			var sb = _formatBuilder;
			sb.Clear();
			// Always reset first so decorations from previous cells don't leak
			sb.Append("\x1b[0");

			// Foreground: A=0 means use terminal default (39), otherwise explicit RGB
			if (fg.A == 0)
			{
				sb.Append(";39");
			}
			else
			{
				sb.Append(";38;2;");
				sb.Append(fg.R); sb.Append(';');
				sb.Append(fg.G); sb.Append(';');
				sb.Append(fg.B);
			}

			// Background: A=0 means use terminal default (49), otherwise explicit RGB.
			// In PreserveTerminalTransparency mode, any non-opaque bg also emits 49.
			if (bg.A == 0 ||
			    (_options.TerminalTransparencyMode == Configuration.TerminalTransparencyMode.PreserveTerminalTransparency && bg.A < 255))
			{
				sb.Append(";49");
			}
			else
			{
				sb.Append(";48;2;");
				sb.Append(bg.R); sb.Append(';');
				sb.Append(bg.G); sb.Append(';');
				sb.Append(bg.B);
			}

			if (decorations != TextDecoration.None)
			{
				if ((decorations & TextDecoration.Bold) != 0) sb.Append(";1");
				if ((decorations & TextDecoration.Dim) != 0) sb.Append(";2");
				if ((decorations & TextDecoration.Italic) != 0) sb.Append(";3");
				if ((decorations & TextDecoration.Underline) != 0) sb.Append(";4");
				if ((decorations & TextDecoration.Blink) != 0) sb.Append(";5");
				if ((decorations & TextDecoration.Invert) != 0) sb.Append(";7");
				if ((decorations & TextDecoration.Strikethrough) != 0) sb.Append(";9");
			}

			sb.Append('m');

			try { _lastCellAnsi = sb.ToString(); }
			catch (ArgumentOutOfRangeException) { _lastCellAnsi = "\x1b[0m"; }
			_lastCellFg = fg;
			_lastCellBg = bg;
			_lastCellDecorations = decorations;
			return _lastCellAnsi;
		}

		/// <summary>
		/// Sets a single cell in the back buffer with the specified character and colors.
		/// </summary>
		/// <param name="x">The horizontal position (column).</param>
		/// <param name="y">The vertical position (row).</param>
		/// <param name="character">The character to write.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void SetNarrowCell(int x, int y, char character, Color fg, Color bg)
			=> SetNarrowCell(x, y, new Rune(character), fg, bg);

		/// <summary>
		/// Sets a narrow (width-1) cell in the back buffer with the specified Rune and colors.
		/// Clears IsWideContinuation and Combiners.
		/// </summary>
		public void SetNarrowCell(int x, int y, Rune character, Color fg, Color bg)
		{
			if (!IsValidPosition(x, y))
				return;

			// Fix wide char pair split: if overwriting a continuation cell, clear its orphaned base.
			// If overwriting a base cell whose continuation is at x+1, clear the orphaned continuation.
			if (_backBuffer[x, y].IsWideContinuation && x > 0)
			{
				ref var orphanedBase = ref _backBuffer[x - 1, y];
				orphanedBase.Character = new Rune(' ');
				orphanedBase.IsWideContinuation = false;
				orphanedBase.Combiners = null;
			}
			if (x + 1 < _width && _backBuffer[x + 1, y].IsWideContinuation)
			{
				ref var orphanedCont = ref _backBuffer[x + 1, y];
				orphanedCont.Character = new Rune(' ');
				orphanedCont.IsWideContinuation = false;
				orphanedCont.Combiners = null;
			}

			string ansi = FormatCellAnsi(fg, bg);
			ref var cell = ref _backBuffer[x, y];
			if (cell.Character != character || cell.AnsiEscape != ansi || cell.IsWideContinuation || cell.Combiners != null)
			{
				cell.Character = character;
				cell.AnsiEscape = ansi;
				cell.IsWideContinuation = false;
				cell.Combiners = null;
			}
		}

		/// <summary>
		/// Fills a horizontal run of cells in the back buffer with the specified character and colors.
		/// </summary>
		/// <param name="x">The starting horizontal position (column).</param>
		/// <param name="y">The vertical position (row).</param>
		/// <param name="width">The number of cells to fill.</param>
		/// <param name="character">The character to fill with.</param>
		/// <param name="fg">The foreground color.</param>
		/// <param name="bg">The background color.</param>
		public void FillCells(int x, int y, int width, char character, Color fg, Color bg)
			=> FillCells(x, y, width, new Rune(character), fg, bg);

		/// <summary>
		/// Fills a horizontal run of cells with the specified Rune and colors.
		/// </summary>
		public void FillCells(int x, int y, int width, Rune character, Color fg, Color bg)
		{
			if (!IsValidPosition(x, y) || width <= 0)
				return;

			int maxWidth = Math.Min(width, _width - x);
			if (_options.ClampToWindowWidth)
				maxWidth = Math.Min(maxWidth, GetCurrentWindowWidth() - x);

			// Fix wide char pair split at left boundary: if the first cell we're overwriting
			// is a continuation cell, its base cell (to the left) becomes orphaned — clear it.
			if (x > 0 && _backBuffer[x, y].IsWideContinuation)
			{
				ref var orphanedBase = ref _backBuffer[x - 1, y];
				orphanedBase.Character = new Rune(' ');
				orphanedBase.IsWideContinuation = false;
				orphanedBase.Combiners = null;
			}

			// Fix wide char pair split at right boundary: if the cell just past our fill range
			// is a continuation cell, its base cell (which we're about to overwrite) is gone — clear it.
			int rightEdge = x + maxWidth;
			if (rightEdge < _width && _backBuffer[rightEdge, y].IsWideContinuation)
			{
				ref var orphanedCont = ref _backBuffer[rightEdge, y];
				orphanedCont.Character = new Rune(' ');
				orphanedCont.IsWideContinuation = false;
				orphanedCont.Combiners = null;
			}

			string ansi = FormatCellAnsi(fg, bg);
			for (int i = 0; i < maxWidth; i++)
			{
				ref var cell = ref _backBuffer[x + i, y];
				if (cell.Character != character || cell.AnsiEscape != ansi || cell.IsWideContinuation || cell.Combiners != null)
				{
					cell.Character = character;
					cell.AnsiEscape = ansi;
					cell.IsWideContinuation = false;
					cell.Combiners = null;
				}
			}
		}

		/// <summary>
		/// Copies a horizontal strip of cells from a <see cref="CharacterBuffer"/> directly into the back buffer,
		/// bypassing ANSI string serialization and parsing entirely.
		/// </summary>
		/// <param name="destX">Destination X position in this buffer.</param>
		/// <param name="destY">Destination Y position in this buffer.</param>
		/// <param name="source">The source CharacterBuffer to read cells from.</param>
		/// <param name="srcX">Source X offset within the CharacterBuffer.</param>
		/// <param name="srcY">Source Y (row) within the CharacterBuffer.</param>
		/// <param name="width">Number of cells to copy.</param>
		/// <param name="fallbackBg">Background color to use for padding when source is out of bounds.</param>
		public void SetCellsFromBuffer(int destX, int destY, CharacterBuffer source, int srcX, int srcY, int width, Color fallbackBg)
		{
			if (!IsValidPosition(destX, destY) || width <= 0)
				return;

			int maxWidth = Math.Min(width, _width - destX);
			if (_options.ClampToWindowWidth)
				maxWidth = Math.Min(maxWidth, GetCurrentWindowWidth() - destX);

			// Fix wide char pair split at left boundary: if the first cell we're overwriting
			// is a continuation cell, its base cell (to the left) becomes orphaned — clear it.
			if (destX > 0 && _backBuffer[destX, destY].IsWideContinuation)
			{
				ref var orphanedBase = ref _backBuffer[destX - 1, destY];
				orphanedBase.Character = new Rune(' ');
				orphanedBase.IsWideContinuation = false;
				orphanedBase.Combiners = null;
			}

			// Fix wide char pair split at right boundary: if the cell just past our write range
			// is a continuation cell, its base cell (which we're about to overwrite) is gone — clear it.
			int rightEdge = destX + maxWidth;
			if (rightEdge < _width && _backBuffer[rightEdge, destY].IsWideContinuation)
			{
				ref var orphanedCont = ref _backBuffer[rightEdge, destY];
				orphanedCont.Character = new Rune(' ');
				orphanedCont.IsWideContinuation = false;
				orphanedCont.Combiners = null;
			}

			int sourceWidth = source.Width;
			int sourceHeight = source.Height;

			for (int i = 0; i < maxWidth; i++)
			{
				int sx = srcX + i;
				ref var destCell = ref _backBuffer[destX + i, destY];

				if (sx >= 0 && sx < sourceWidth && srcY >= 0 && srcY < sourceHeight)
				{
					var srcCell = source.GetCell(sx, srcY);

					// Fix clipped wide char at left edge of copy region: if the first source
					// cell is a continuation, its base is outside our copy range (under another
					// window). Write a space instead of an orphaned continuation that would be
					// skipped during rendering, leaving stale terminal content visible.
					// Also fix clipped wide char at right edge: if this is a wide base
					// but its continuation would be outside our copy range, write space.
					bool isOrphanedContinuation = srcCell.IsWideContinuation && i == 0;
					bool isClippedWideBase = !srcCell.IsWideContinuation &&
						sx + 1 < sourceWidth && source.GetCell(sx + 1, srcY).IsWideContinuation &&
						i == maxWidth - 1;
					if (isOrphanedContinuation || isClippedWideBase)
					{
						string ansi = FormatCellAnsi(srcCell.Foreground, srcCell.Background, srcCell.Decorations);
						var spaceRune = new Rune(' ');
						if (destCell.Character != spaceRune || destCell.AnsiEscape != ansi || destCell.IsWideContinuation || destCell.Combiners != null)
						{
							destCell.Character = spaceRune;
							destCell.AnsiEscape = ansi;
							destCell.IsWideContinuation = false;
							destCell.Combiners = null;
						}
					}
					else
					{
						string ansi = FormatCellAnsi(srcCell.Foreground, srcCell.Background, srcCell.Decorations);

						if (destCell.Character != srcCell.Character || destCell.AnsiEscape != ansi || destCell.IsWideContinuation != srcCell.IsWideContinuation || destCell.Combiners != srcCell.Combiners)
						{
							destCell.Character = srcCell.Character;
							destCell.AnsiEscape = ansi;
							destCell.IsWideContinuation = srcCell.IsWideContinuation;
							destCell.Combiners = srcCell.Combiners;
						}
					}
				}
				else
				{
					// Out of bounds: write padding space with fallback background
					var spaceRune = new Rune(' ');
					string ansi = FormatCellAnsi(Color.White, fallbackBg);
					if (destCell.Character != spaceRune || destCell.AnsiEscape != ansi || destCell.IsWideContinuation || destCell.Combiners != null)
					{
						destCell.Character = spaceRune;
						destCell.AnsiEscape = ansi;
						destCell.IsWideContinuation = false;
						destCell.Combiners = null;
					}
				}
			}
		}

		/// <summary>
		/// Clears the back buffer by resetting all cells to their default state.
		/// </summary>
		/// <remarks>
		/// All cells are reset to a space character with no ANSI formatting and marked as dirty.
		/// </remarks>
		public void Clear()
		{
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					_backBuffer[x, y].Reset();
				}
			}
		}

		/// <summary>
		/// Gets the count of dirty characters in the back buffer.
		/// </summary>
		/// <returns>The number of characters marked as dirty.</returns>
		public int GetDirtyCharacterCount()
		{
			int count = 0;
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					if (!_frontBuffer[x, y].Equals(_backBuffer[x, y]))
						count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Checks if a specific cell is dirty (front buffer differs from back buffer).
		/// Pure double-buffering: compares buffer content, no state tracking.
		/// </summary>
		/// <param name="x">The x coordinate of the cell</param>
		/// <param name="y">The y coordinate of the cell</param>
		/// <returns>True if the cell content differs between front and back buffers</returns>
		public bool IsCellDirty(int x, int y)
		{
			if (!IsValidPosition(x, y))
				return false;

			return !_frontBuffer[x, y].Equals(_backBuffer[x, y]);
		}

		/// <summary>
		/// Renders the back buffer to the console, updating only the changed portions.
		/// </summary>
		/// <remarks>
		/// This method compares the back buffer with the front buffer and only writes
		/// cells that have changed, using cursor positioning to skip unchanged regions.
		/// After rendering, the front buffer is synchronized with the back buffer.
		/// The cursor is hidden during rendering to prevent flickering.
		/// </remarks>
		public void Render()
		{
			if (Lock)
				return;

			// Lock Console I/O to prevent concurrent input operations from being corrupted
			lock (_consoleLock)
			{
				// Diagnostics: Begin metrics capture
				var metrics = _diagnostics?.IsEnabled == true ? new Diagnostics.RenderingMetrics() : null;

				// Diagnostics: Capture dirty count BEFORE rendering (before buffers sync)
				int dirtyCountBeforeRender = 0;
				if (metrics != null)
				{
					dirtyCountBeforeRender = GetDirtyCharacterCount();
				}

				// Diagnostics: Capture console buffer state before rendering
				if (_diagnostics?.IsEnabled == true && _diagnostics.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.ConsoleBuffer))
				{
					CaptureConsoleBufferSnapshot();
				}

				// Pre-process wide character dirty pair coherence.
				// The terminal treats wide chars as atomic 2-cell units, so when either half
				// of a pair changes, both must be re-emitted to avoid display corruption.
				for (int y = 0; y < _height; y++)
				{
					for (int x = 1; x < _width; x++)
					{
						ref readonly var backCell = ref _backBuffer[x, y];

						// Case 1: Back buffer continuation changed but base cell is clean → force base dirty.
						if (backCell.IsWideContinuation &&
							!_frontBuffer[x, y].Equals(backCell) &&
							_frontBuffer[x - 1, y].Equals(_backBuffer[x - 1, y]))
						{
							_frontBuffer[x - 1, y].Reset();
						}

						// Case 2: Front buffer has a continuation at x but back buffer does NOT.
						// The terminal still displays the right half of the old wide char.
						// Force the base cell (x-1) dirty so the wide char is properly replaced.
						if (_frontBuffer[x, y].IsWideContinuation &&
							!backCell.IsWideContinuation &&
							_frontBuffer[x - 1, y].Equals(_backBuffer[x - 1, y]))
						{
							_frontBuffer[x - 1, y].Reset();
						}
					}
				}

				// Hide cursor during render — use raw write on Unix to bypass .NET entirely
				WriteOutput("\x1b[?25l");

				// Build entire screen in one string for atomic output
				// This eliminates flickering by doing a single write instead of multiple cursor moves
				_screenBuilder.Clear();

				int cellsRendered = 0;

				// Choose rendering strategy based on configured dirty tracking mode
				if (_options.DirtyTrackingMode == Configuration.DirtyTrackingMode.Cell)
				{
					// CELL-LEVEL: Always render only changed regions within lines (minimal output)
					for (int y = 0; y < _height; y++)
					{
						GetDirtyRegionsInLine(y);
						if (_dirtyRegionsPool.Count == 0)
							continue;

						foreach (var (startX, endX) in _dirtyRegionsPool)
						{
							// Position cursor at start of dirty region
							_screenBuilder.Append($"\x1b[{y + 1};{startX + 1}H");

							// Append only the dirty region
							AppendRegionToBuilder(y, startX, endX, _screenBuilder);

							cellsRendered += (endX - startX + 1);
						}
					}
				}
				else if (_options.DirtyTrackingMode == Configuration.DirtyTrackingMode.Line)
				{
					// LINE-LEVEL: Always render entire line when any cell changes
					for (int y = 0; y < _height; y++)
					{
						if (!IsLineDirty(y))
							continue;

						// Add ANSI absolute positioning: ESC[row;colH (1-based)
						_screenBuilder.Append($"\x1b[{y + 1};1H");

						// Append this line's content to the screen builder
						AppendLineToBuilder(y, _screenBuilder);

						cellsRendered += _width;
					}
				}
				else  // DirtyTrackingMode.Smart
				{
					// SMART MODE: Analyze each line and choose optimal strategy per line
					for (int y = 0; y < _height; y++)
					{
						var (isDirty, useLineMode) = AnalyzeLine(y);
						if (!isDirty)
							continue;

						if (useLineMode)
						{
							// Use LINE strategy for this line (high coverage or fragmented)
							_screenBuilder.Append($"\x1b[{y + 1};1H");
							AppendLineToBuilder(y, _screenBuilder);
							cellsRendered += _width;
						}
						else
						{
							// Use CELL strategy for this line (low coverage, not fragmented)
							foreach (var (startX, endX) in _dirtyRegionsPool)
							{
								_screenBuilder.Append($"\x1b[{y + 1};{startX + 1}H");
								AppendRegionToBuilder(y, startX, endX, _screenBuilder);
								cellsRendered += (endX - startX + 1);
							}
						}
					}
				}

				// Single atomic write of entire screen via raw libc write() on Unix
				// Completely bypasses .NET's Console/StreamWriter/SyncTextWriter
				if (_screenBuilder.Length > 0)
				{
					WriteOutput(_screenBuilder);
				}

				// Diagnostics: Capture output metrics
				if (metrics != null)
				{
					// Only materialize the string for diagnostics (not on the normal hot path)
					var output = _screenBuilder.ToString();
					metrics.BytesWritten = output.Length;
					metrics.AnsiEscapeSequences = CountAnsiSequences(output);
					metrics.CursorMovements = CountCursorMoves(output);
					metrics.CellsActuallyRendered = cellsRendered;
					metrics.DirtyCellsMarked = dirtyCountBeforeRender;
					metrics.CharactersChanged = dirtyCountBeforeRender;

					// Capture output snapshot
					if (_diagnostics?.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.ConsoleOutput) == true)
					{
						_diagnostics.CaptureConsoleOutput(output);
					}

					// Record metrics
					_diagnostics?.RecordMetrics(metrics);
				}
			}
		}

		private void InitializeBuffers()
		{
			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					_frontBuffer[x, y] = new Cell();
					_backBuffer[x, y] = new Cell();
				}
			}
		}

		private bool IsLineDirty(int y)
		{
			for (int x = 0; x < _width; x++)
			{
				ref readonly var frontCell = ref _frontBuffer[x, y];
				ref readonly var backCell = ref _backBuffer[x, y];

				// Pure double-buffering: compare buffer content
				if (!frontCell.Equals(backCell))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Gets dirty regions (contiguous changed cells) within a line.
		/// Populates the pooled _dirtyRegionsPool list with (startX, endX) tuples.
		/// Used for cell-level dirty tracking mode.
		/// </summary>
		private void GetDirtyRegionsInLine(int y)
		{
			_dirtyRegionsPool.Clear();
			int regionStart = -1;

			for (int x = 0; x < _width; x++)
			{
				ref readonly var frontCell = ref _frontBuffer[x, y];
				ref readonly var backCell = ref _backBuffer[x, y];

				bool isDirty = !frontCell.Equals(backCell);

				if (isDirty)
				{
					// Start new region or continue existing
					if (regionStart < 0) regionStart = x;
				}
				else if (regionStart >= 0)
				{
					// End of dirty region
					_dirtyRegionsPool.Add((regionStart, x - 1));
					regionStart = -1;
				}
			}

			// Close final region if line ends dirty
			if (regionStart >= 0)
			{
				_dirtyRegionsPool.Add((regionStart, _width - 1));
			}
		}

		/// <summary>
		/// Smart mode: Analyzes a line in a single pass to determine:
		/// 1. Is the line dirty?
		/// 2. If dirty, should we use LINE or CELL rendering strategy?
		/// Returns (isDirty, useLineMode). Dirty regions are populated in _dirtyRegionsPool.
		/// Optimized to avoid double-scanning the line.
		/// </summary>
		private (bool isDirty, bool useLineMode) AnalyzeLine(int y)
		{
			_dirtyRegionsPool.Clear();
			int regionStart = -1;
			int dirtyCells = 0;
			int dirtyRuns = 0;

			for (int x = 0; x < _width; x++)
			{
				ref readonly var frontCell = ref _frontBuffer[x, y];
				ref readonly var backCell = ref _backBuffer[x, y];

				bool isDirty = !frontCell.Equals(backCell);

				if (isDirty)
				{
					dirtyCells++;
					if (regionStart < 0)
					{
						// Start new dirty region
						regionStart = x;
						dirtyRuns++;
					}
				}
				else if (regionStart >= 0)
				{
					// End of dirty region
					_dirtyRegionsPool.Add((regionStart, x - 1));
					regionStart = -1;
				}
			}

			// Close final region if line ends dirty
			if (regionStart >= 0)
			{
				_dirtyRegionsPool.Add((regionStart, _width - 1));
			}

			// No dirty cells? Return early
			if (dirtyCells == 0)
				return (false, false);

			// Decision heuristics for Smart mode:
			float coverage = (float)dirtyCells / _width;

			// 1. High coverage (>threshold%) → LINE mode (too much to render cell-by-cell)
			if (coverage > _options.SmartModeCoverageThreshold)
				return (true, true);

			// 2. Highly fragmented (>threshold separate runs) → LINE mode (too many cursor moves)
			if (dirtyRuns > _options.SmartModeFragmentationThreshold)
				return (true, true);

			// 3. Full line dirty → LINE mode (same output, fewer cursor moves)
			if (dirtyCells == _width)
				return (true, true);

			// 4. Low coverage + low fragmentation → CELL mode (minimal output)
			return (true, false);
		}

		/// <summary>
		/// Writes output using raw libc write() when raw mode is active, or Console.Out.Write as fallback.
		/// Raw write completely bypasses .NET's Console/StreamWriter/SyncTextWriter infrastructure,
		/// eliminating any possibility of .NET runtime code touching termios during output.
		/// </summary>
		private static void WriteOutput(string text)
		{
			if (TerminalRawMode.IsRawModeActive)
				TerminalRawMode.WriteStdout(text);
			else
				Console.Out.Write(text);
		}

		/// <summary>
		/// Writes a StringBuilder directly to stdout, avoiding the intermediate ToString() allocation.
		/// Falls back to ToString() for the Console.Out path (non-raw mode).
		/// </summary>
		private static void WriteOutput(StringBuilder sb)
		{
			if (TerminalRawMode.IsRawModeActive)
				TerminalRawMode.WriteStdout(sb);
			else
				Console.Out.Write(sb.ToString());
		}

		/// <summary>
		/// Gets current window width using ioctl when raw mode is active, avoiding ConsolePal.
		/// </summary>
		private int GetCurrentWindowWidth()
		{
			var (w, _) = TerminalRawMode.GetWindowSize();
			return w;
		}

		private bool IsValidPosition(int x, int y)
			=> x >= 0 && x < _width && y >= 0 && y < _height;

		/// <summary>
		/// Appends a specific region of a line to the builder (cell-level tracking).
		/// Only outputs cells from startX to endX (inclusive).
		/// </summary>
		private void AppendRegionToBuilder(int y, int startX, int endX, StringBuilder builder)
		{
			string lastOutputAnsi = string.Empty;

			for (int x = startX; x <= endX && x < _width; x++)
			{
				ref var backCell = ref _backBuffer[x, y];
				ref var frontCell = ref _frontBuffer[x, y];

				// Sync buffers regardless of whether we emit output
				frontCell.CopyFrom(backCell);

				// Skip continuation cells — terminal auto-advances for wide chars
				if (backCell.IsWideContinuation)
				{
					// Safety: emit any combiners attached to continuation cell
					if (backCell.Combiners != null)
						builder.Append(backCell.Combiners);
					continue;
				}

				// Wide char terminal safety: when emitting a wide character, the terminal
				// auto-advances past the continuation cell at x+1. But if the terminal was
				// previously showing a different character at x+1 (e.g., a border │), some
				// terminals don't reliably clear it — the old content persists as a ghost.
				// Fix: emit a space at x+1 first to explicitly clear the old content, then
				// reposition cursor back to x before emitting the wide char.
				bool isWideChar = x + 1 < _width && _backBuffer[x + 1, y].IsWideContinuation;
				if (isWideChar)
				{
					// Check if the terminal (old front buffer, before sync) had something
					// other than this wide char's continuation at x+1
					ref var nextFront = ref _frontBuffer[x + 1, y];
					bool terminalHadDifferentContent = !nextFront.Equals(_backBuffer[x + 1, y]);

					if (terminalHadDifferentContent)
					{
						// Emit space at x+1 to clear old terminal content
						builder.Append(backCell.AnsiEscape);
						lastOutputAnsi = backCell.AnsiEscape;
						builder.Append(' ');
						// Reposition cursor back to x
						builder.Append($"\x1b[{y + 1};{x + 1}H");
					}

					// Sync the continuation cell's front buffer now
					nextFront.CopyFrom(_backBuffer[x + 1, y]);
				}

				// Output ANSI only if it changed
				if (backCell.AnsiEscape != lastOutputAnsi)
				{
					builder.Append(backCell.AnsiEscape);
					lastOutputAnsi = backCell.AnsiEscape;
				}

				// Output character
				builder.AppendRune(backCell.Character);
				if (backCell.Combiners != null)
					builder.Append(backCell.Combiners);

				// Skip past continuation cell — we already synced it above
				if (isWideChar)
				{
					// Emit any combiners on the continuation
					ref readonly var contCell = ref _backBuffer[x + 1, y];
					if (contCell.Combiners != null)
						builder.Append(contCell.Combiners);
					x++; // Skip continuation in loop
				}
			}

			// Reset ANSI at end of region
			if (!string.IsNullOrEmpty(lastOutputAnsi))
			{
				builder.Append("\x1b[0m");
			}
		}

		private void AppendLineToBuilder(int y, StringBuilder builder)
		{
			// DO NOT clear - we are appending to the screen builder
			int consecutiveUnchanged = 0;
			string lastOutputAnsi = string.Empty;
			bool lastOutputWasWide = false;

			int maxWidth = _options.ClampToWindowWidth ? Math.Min(_width, GetCurrentWindowWidth()) : _width;
			for (int x = 0; x < maxWidth; x++)
			{
				ref var frontCell = ref _frontBuffer[x, y];
				ref var backCell = ref _backBuffer[x, y];

				// Pure double-buffering: compare buffer content
				bool shouldWrite = !frontCell.Equals(backCell);

				if (shouldWrite)
				{
					// Sync buffers first
					frontCell.CopyFrom(backCell);

					// Skip continuation cells — terminal auto-advances for wide chars
					if (backCell.IsWideContinuation)
					{
						// Safety: emit any combiners attached to continuation cell
						if (backCell.Combiners != null)
							builder.Append(backCell.Combiners);
						lastOutputWasWide = false;
						continue;
					}

					// If we have pending cursor forward movements, append them first
					if (consecutiveUnchanged > 0)
					{
						if (consecutiveUnchanged == 1)
						{
							builder.Append(CursorForward);
						}
						else
						{
							builder.Append($"\u001b[{consecutiveUnchanged}C");
						}
						consecutiveUnchanged = 0;
					}

					// Wide char terminal safety: clear old content at continuation position
					// before emitting the wide char (see AppendRegionToBuilder for full explanation)
					bool isWideChar = x + 1 < maxWidth && _backBuffer[x + 1, y].IsWideContinuation;
					if (isWideChar)
					{
						ref var nextFront = ref _frontBuffer[x + 1, y];
						if (!nextFront.Equals(_backBuffer[x + 1, y]))
						{
							// Emit ANSI + space at x+1 to clear old terminal content
							if (backCell.AnsiEscape != lastOutputAnsi)
							{
								builder.Append(backCell.AnsiEscape);
								lastOutputAnsi = backCell.AnsiEscape;
							}
							builder.Append(' ');
							// Reposition cursor back to x
							builder.Append($"\x1b[{y + 1};{x + 1}H");
						}
						// Sync continuation front buffer
						nextFront.CopyFrom(_backBuffer[x + 1, y]);
					}

					// Only output ANSI if it's different from the last one we output
					if (backCell.AnsiEscape != lastOutputAnsi)
					{
						if (!string.IsNullOrEmpty(backCell.AnsiEscape))
						{
							// Skip malformed ANSI sequences but always continue to output character
							if (backCell.AnsiEscape.StartsWith("\x1b[") && char.IsLetter(backCell.AnsiEscape[^1]))
							{
								builder.Append(backCell.AnsiEscape);
								lastOutputAnsi = backCell.AnsiEscape;
							}
						}
						else
						{
							// Empty ANSI is valid, update tracking
							lastOutputAnsi = backCell.AnsiEscape;
						}
					}

					// Always output character
					builder.AppendRune(backCell.Character);
					if (backCell.Combiners != null)
						builder.Append(backCell.Combiners);

					// Track if this was a wide char — terminal advances cursor by 2
					lastOutputWasWide = isWideChar;
				}
				else
				{
					// If previous output was a wide char, terminal already advanced past
					// this continuation cell — don't count it as a gap
					if (lastOutputWasWide && backCell.IsWideContinuation)
					{
						lastOutputWasWide = false;
						continue;
					}
					lastOutputWasWide = false;
					consecutiveUnchanged++;
				}
			}

			// Add ANSI reset after line to prevent formatting bleed at edge
			if (maxWidth > 0)
			{
				builder.Append(ResetSequence);
			}
		}

		#region Diagnostics Helper Methods

		/// <summary>
		/// Captures a snapshot of the console buffer state for diagnostics.
		/// </summary>
		private void CaptureConsoleBufferSnapshot()
		{
			if (_diagnostics == null) return;

			// Deep copy buffers to ConsoleCell arrays
			var frontCopy = new Diagnostics.Snapshots.ConsoleCell[_width, _height];
			var backCopy = new Diagnostics.Snapshots.ConsoleCell[_width, _height];

			for (int y = 0; y < _height; y++)
			{
				for (int x = 0; x < _width; x++)
				{
					frontCopy[x, y] = new Diagnostics.Snapshots.ConsoleCell(
						_frontBuffer[x, y].Character,
						_frontBuffer[x, y].AnsiEscape ?? string.Empty);

					backCopy[x, y] = new Diagnostics.Snapshots.ConsoleCell(
						_backBuffer[x, y].Character,
						_backBuffer[x, y].AnsiEscape ?? string.Empty);
				}
			}

			_diagnostics.CaptureConsoleBufferState(frontCopy, backCopy, _width, _height);
		}

		/// <summary>
		/// Counts ANSI escape sequences in output string.
		/// </summary>
		private int CountAnsiSequences(string output)
		{
			return _ansiRegex.Matches(output).Count;
		}

		/// <summary>
		/// Counts cursor positioning commands in output string.
		/// </summary>
		private int CountCursorMoves(string output)
		{
			return Regex.Matches(output, @"\x1b\[\d+;\d+H").Count;
		}

		#endregion

		private struct Cell
		{
			public string AnsiEscape;
			public Rune Character;
			public bool IsWideContinuation;
			public string? Combiners;

			public Cell()
			{
				AnsiEscape = string.Empty;
				Character = new Rune(' ');
				IsWideContinuation = false;
				Combiners = null;
			}

			public void CopyFrom(in Cell other)
			{
				Character = other.Character;
				AnsiEscape = other.AnsiEscape;
				IsWideContinuation = other.IsWideContinuation;
				Combiners = other.Combiners;
			}

			public bool Equals(in Cell other)
				=> Character == other.Character && AnsiEscape == other.AnsiEscape && IsWideContinuation == other.IsWideContinuation && Combiners == other.Combiners;

			public void Reset()
			{
				Character = new Rune(' ');
				AnsiEscape = string.Empty;
				IsWideContinuation = false;
				Combiners = null;
			}
		}
	}
}
