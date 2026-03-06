// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers.Input;
using SharpConsoleUI.Layout;
using System.Text;
using System.Text.RegularExpressions;
using Color = Spectre.Console.Color;

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

		private readonly int _width;
		private readonly object? _consoleLock; // Shared lock for thread-safe Console I/O
		private readonly Configuration.ConsoleWindowSystemOptions _options;

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
			_consoleLock = consoleLock;
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

		/// <summary>
		/// Formats an ANSI escape sequence for the given foreground and background colors.
		/// Caches the last result for consecutive cells with the same colors (common case).
		/// </summary>
		private string FormatCellAnsi(Color fg, Color bg)
		{
			if (fg.Equals(_lastCellFg) && bg.Equals(_lastCellBg))
				return _lastCellAnsi;

			_lastCellAnsi = $"\x1b[38;2;{fg.R};{fg.G};{fg.B};48;2;{bg.R};{bg.G};{bg.B}m";
			_lastCellFg = fg;
			_lastCellBg = bg;
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
		public void SetCell(int x, int y, char character, Color fg, Color bg)
		{
			if (!IsValidPosition(x, y))
				return;

			string ansi = FormatCellAnsi(fg, bg);
			ref var cell = ref _backBuffer[x, y];
			if (cell.Character != character || cell.AnsiEscape != ansi)
			{
				cell.Character = character;
				cell.AnsiEscape = ansi;
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
		{
			if (!IsValidPosition(x, y) || width <= 0)
				return;

			int maxWidth = Math.Min(width, _width - x);
			if (_options.ClampToWindowWidth)
				maxWidth = Math.Min(maxWidth, GetCurrentWindowWidth() - x);

			string ansi = FormatCellAnsi(fg, bg);
			for (int i = 0; i < maxWidth; i++)
			{
				ref var cell = ref _backBuffer[x + i, y];
				if (cell.Character != character || cell.AnsiEscape != ansi)
				{
					cell.Character = character;
					cell.AnsiEscape = ansi;
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

			int sourceWidth = source.Width;
			int sourceHeight = source.Height;

			for (int i = 0; i < maxWidth; i++)
			{
				int sx = srcX + i;
				ref var destCell = ref _backBuffer[destX + i, destY];

				if (sx >= 0 && sx < sourceWidth && srcY >= 0 && srcY < sourceHeight)
				{
					var srcCell = source.GetCell(sx, srcY);
					string ansi = FormatCellAnsi(srcCell.Foreground, srcCell.Background);

					if (destCell.Character != srcCell.Character || destCell.AnsiEscape != ansi)
					{
						destCell.Character = srcCell.Character;
						destCell.AnsiEscape = ansi;
					}
				}
				else
				{
					// Out of bounds: write padding space with fallback background
					string ansi = FormatCellAnsi(Color.White, fallbackBg);
					if (destCell.Character != ' ' || destCell.AnsiEscape != ansi)
					{
						destCell.Character = ' ';
						destCell.AnsiEscape = ansi;
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
			lock (_consoleLock ?? new object())
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
						var dirtyRegions = GetDirtyRegionsInLine(y);
						if (dirtyRegions.Count == 0)
							continue;

						foreach (var (startX, endX) in dirtyRegions)
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
						var (isDirty, useLineMode, dirtyRegions) = AnalyzeLine(y);
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
							foreach (var (startX, endX) in dirtyRegions)
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
				var output = _screenBuilder.ToString();
				if (output.Length > 0)
				{
					WriteOutput(output);
				}

				// Diagnostics: Capture output metrics
				if (metrics != null)
				{
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
		/// Returns list of (startX, endX) tuples representing dirty regions.
		/// Used for cell-level dirty tracking mode.
		/// </summary>
		private List<(int startX, int endX)> GetDirtyRegionsInLine(int y)
		{
			var regions = new List<(int, int)>();
			int? regionStart = null;

			for (int x = 0; x < _width; x++)
			{
				ref readonly var frontCell = ref _frontBuffer[x, y];
				ref readonly var backCell = ref _backBuffer[x, y];

				bool isDirty = !frontCell.Equals(backCell);

				if (isDirty)
				{
					// Start new region or continue existing
					regionStart ??= x;
				}
				else if (regionStart.HasValue)
				{
					// End of dirty region
					regions.Add((regionStart.Value, x - 1));
					regionStart = null;
				}
			}

			// Close final region if line ends dirty
			if (regionStart.HasValue)
			{
				regions.Add((regionStart.Value, _width - 1));
			}

			return regions;
		}

		/// <summary>
		/// Smart mode: Analyzes a line in a single pass to determine:
		/// 1. Is the line dirty?
		/// 2. If dirty, should we use LINE or CELL rendering strategy?
		/// Returns (isDirty, useLineMode, dirtyRegions).
		/// Optimized to avoid double-scanning the line.
		/// </summary>
		private (bool isDirty, bool useLineMode, List<(int startX, int endX)> dirtyRegions) AnalyzeLine(int y)
		{
			var regions = new List<(int startX, int endX)>();
			int? regionStart = null;
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
					if (!regionStart.HasValue)
					{
						// Start new dirty region
						regionStart = x;
						dirtyRuns++;
					}
				}
				else if (regionStart.HasValue)
				{
					// End of dirty region
					regions.Add((regionStart.Value, x - 1));
					regionStart = null;
				}
			}

			// Close final region if line ends dirty
			if (regionStart.HasValue)
			{
				regions.Add((regionStart.Value, _width - 1));
			}

			// No dirty cells? Return early
			if (dirtyCells == 0)
				return (false, false, regions);

			// Decision heuristics for Smart mode:
			float coverage = (float)dirtyCells / _width;

			// 1. High coverage (>threshold%) → LINE mode (too much to render cell-by-cell)
			if (coverage > _options.SmartModeCoverageThreshold)
				return (true, true, regions);

			// 2. Highly fragmented (>threshold separate runs) → LINE mode (too many cursor moves)
			if (dirtyRuns > _options.SmartModeFragmentationThreshold)
				return (true, true, regions);

			// 3. Full line dirty → LINE mode (same output, fewer cursor moves)
			if (dirtyCells == _width)
				return (true, true, regions);

			// 4. Low coverage + low fragmentation → CELL mode (minimal output)
			return (true, false, regions);
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

				// Output ANSI only if it changed
				if (backCell.AnsiEscape != lastOutputAnsi)
				{
					builder.Append(backCell.AnsiEscape);
					lastOutputAnsi = backCell.AnsiEscape;
				}

				// Output character
				builder.Append(backCell.Character);

				// Sync buffers
				frontCell.CopyFrom(backCell);
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

			int maxWidth = _options.ClampToWindowWidth ? Math.Min(_width, GetCurrentWindowWidth()) : _width;
			for (int x = 0; x < maxWidth; x++)
			{
				ref var frontCell = ref _frontBuffer[x, y];
				ref var backCell = ref _backBuffer[x, y];

				// Pure double-buffering: compare buffer content
				bool shouldWrite = !frontCell.Equals(backCell);

				if (shouldWrite)
				{
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

					// Always output character and sync buffers, even if ANSI was malformed
					builder.Append(backCell.Character);
					frontCell.CopyFrom(backCell);
				}
				else
				{
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
			public char Character;

			public Cell()
			{
				AnsiEscape = string.Empty;
				Character = ' ';
			}

			public void CopyFrom(in Cell other)
			{
				Character = other.Character;
				AnsiEscape = other.AnsiEscape;
			}

			public bool Equals(in Cell other)
				=> Character == other.Character && AnsiEscape == other.AnsiEscape;

			public void Reset()
			{
				Character = ' ';
				AnsiEscape = string.Empty;
			}
		}
	}
}
