// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using System.Runtime.InteropServices;
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

		// StringBuilder for render operations to minimize string allocations
		private readonly StringBuilder _renderBuilder = new(1024);

		private readonly int _width;
		private readonly object? _consoleLock; // Shared lock for thread-safe Console I/O
		private readonly Configuration.ConsoleWindowSystemOptions _options;
		// FIX27: Track last full redraw time for periodic leak clearing
		private DateTime _lastFullRedraw = DateTime.Now;

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

		/// <summary>
		/// Adds content to the back buffer at the specified position.
		/// </summary>
		/// <param name="x">The horizontal position (column) to start writing at.</param>
		/// <param name="y">The vertical position (row) to write to.</param>
		/// <param name="content">The content to write, which may include ANSI escape sequences for formatting.</param>
		/// <remarks>
		/// ANSI escape sequences in the content are parsed and associated with the appropriate cells,
		/// ensuring proper formatting when rendered. Content that extends beyond the buffer width is truncated.
		/// </remarks>
		public void AddContent(int x, int y, string content)
		{
			// Early exit conditions
			if (!IsValidPosition(x, y) || string.IsNullOrEmpty(content))
				return;

			// Remove trailing reset sequence if present
			if (content.EndsWith(ResetSequence))
				content = content[..^ResetSequence.Length];

			var activeAnsiSequence = new StringBuilder(64); // Pre-size for typical ANSI sequences
			int contentPos = 0;
			int bufferX = x;
			int contentLength = AnsiConsoleHelper.StripAnsiStringLength(content);

			// Clear the area where new content will be written
		// FIX1: Pre-clearing can be disabled for better double-buffering
		if (!_options.Fix1_DisablePreclear)
		{
			ClearArea(x, y, contentLength);
		}

			// Single-pass state machine parser for ANSI sequences
		// FIX6: Also limit width in AddContent to prevent writing beyond console
		int maxBufferX = _options.Fix6_WidthLimit ? Math.Min(_width, Console.WindowWidth) : _width;
			while (contentPos < content.Length && bufferX < maxBufferX)
			{
				// State machine: detect ESC character
				if (content[contentPos] == '\x1B' &&
					contentPos + 1 < content.Length &&
					content[contentPos + 1] == '[')
				{
					// Parse ANSI sequence inline
					int seqStart = contentPos;
					contentPos += 2;  // Skip ESC[

					// Read parameters and command (consume all non-letter characters)
					while (contentPos < content.Length &&
						   !char.IsLetter(content[contentPos]))
					{
						contentPos++;
					}

					// Include the terminating command letter
					if (contentPos < content.Length)
					{
						contentPos++;  // Include terminator
						activeAnsiSequence.Append(content, seqStart, contentPos - seqStart);
					}
				}
				else
				{
					// Regular character - write to buffer
					ref var cell = ref _backBuffer[bufferX, y]; // Use ref for better performance
				
				// FIX2: Conditional dirty flagging for true double-buffering
				if (_options.Fix2_ConditionalDirty)
				{
					char newChar = content[contentPos];
					string newAnsi = activeAnsiSequence.ToString();
					
					if (cell.Character != newChar || cell.AnsiEscape != newAnsi)
					{
						cell.Character = newChar;
						cell.AnsiEscape = newAnsi;
					}
				}
				else
				{
					// Original: always mark dirty
				char newChar = content[contentPos];
				string newAnsi = activeAnsiSequence.ToString();


				cell.Character = newChar;
				cell.AnsiEscape = newAnsi;
				}
					contentPos++;
					bufferX++;
				}
			}

		// FIX3: End-of-line formatting removed - was causing ANSI sequence accumulation
		// (previously prepended ResetSequence which accumulated unbounded)


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
			var renderStartTime = metrics != null ? DateTime.UtcNow : default;

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

			Console.CursorVisible = false;

			// FIX24: Drain input buffer to prevent mouse sequences from being echoed during rendering
			// Race condition: If mouse events arrive while we're outputting cursor positioning,
			// they get echoed at our current cursor position, mixing with our content
			if (_options.Fix24_DrainInputBeforeRender)
			{
				int drained = 0;
				while (Console.KeyAvailable)
				{
					try
					{
						Console.ReadKey(true);  // Consume without displaying
						drained++;
					}
					catch
					{
						break;  // Stop if read fails
					}
				}
			}
			// FIX25: Temporarily disable mouse tracking during rendering
			// Problem: .NET Console.ReadKey() toggles echo on/off, creating windows where mouse sequences get echoed
			// Solution: Disable mouse tracking entirely during render to prevent new events from arriving
			if (_options.Fix25_DisableMouseDuringRender)
			{
				// Disable all mouse tracking modes (reverse order of enable)
				Console.Out.Write("\x1b[?1003l");  // Disable any event mouse
				Console.Out.Write("\x1b[?1002l");  // Disable button event tracking
				Console.Out.Write("\x1b[?1000l");  // Disable basic mouse reporting
				Console.Out.Flush();
			}


		// FIX27: Periodic redraw of clean cells to clear terminal echo leaks (Linux/macOS only)
		// Check if 1 second has elapsed since last full redraw (time-based, not frame-based)
		if (_options.Fix27_PeriodicFullRedraw && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var elapsed = (DateTime.Now - _lastFullRedraw).TotalSeconds;
			if (elapsed >= _options.Fix27_RedrawIntervalSeconds)
			{
				int cleanCellsMarkedDirty = 0;
				
				// Mark only CLEAN (non-dirty) cells as dirty
				// Dirty cells will be redrawn anyway, so no need to touch them
				for (int y = 0; y < _height; y++)
				{
					for (int x = 0; x < _width; x++)
					{
						ref var frontCell = ref _frontBuffer[x, y];
						ref var backCell = ref _backBuffer[x, y];
						
						// If cell is clean (front == back), modify front to force redraw
						if (frontCell.Equals(backCell))
						{
							// Make front != back by changing front character
							// Back buffer has correct content and will be rendered
							frontCell.Character = (char)0;  // Dummy value to trigger !Equals()
							cleanCellsMarkedDirty++;
						}
				}
				}
				
				_lastFullRedraw = DateTime.Now;
			}
		}

			// Build entire screen in one string for atomic output
			// This eliminates flickering by doing a single write instead of multiple cursor moves
			var screenBuilder = new StringBuilder();

		// FIX14: Track rendering statistics
		int linesRendered = 0;
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

				linesRendered++;

				foreach (var (startX, endX) in dirtyRegions)
				{
					// Position cursor at start of dirty region
					screenBuilder.Append($"\x1b[{y + 1};{startX + 1}H");

					// Append only the dirty region
					AppendRegionToBuilder(y, startX, endX, screenBuilder);

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

				linesRendered++;

				// Add ANSI absolute positioning: ESC[row;colH (1-based)
				screenBuilder.Append($"\x1b[{y + 1};1H");

				// Append this line's content to the screen builder
				AppendLineToBuilder(y, screenBuilder);

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

				linesRendered++;

				if (useLineMode)
				{
					// Use LINE strategy for this line (high coverage or fragmented)
					screenBuilder.Append($"\x1b[{y + 1};1H");
					AppendLineToBuilder(y, screenBuilder);
					cellsRendered += _width;
				}
				else
				{
					// Use CELL strategy for this line (low coverage, not fragmented)
					foreach (var (startX, endX) in dirtyRegions)
					{
						screenBuilder.Append($"\x1b[{y + 1};{startX + 1}H");
						AppendRegionToBuilder(y, startX, endX, screenBuilder);
						cellsRendered += (endX - startX + 1);
					}
				}
			}
		}


			// Single atomic write of entire screen - no cursor jumps, no flicker!
			var output = screenBuilder.ToString();
			if (output.Length > 0)
			{
				Console.Write(output);
			}

			// Diagnostics: Capture output metrics
			if (metrics != null)
			{
				metrics.BytesWritten = output.Length;
				metrics.AnsiEscapeSequences = CountAnsiSequences(output);
				metrics.CursorMovements = CountCursorMoves(output);
				metrics.CellsActuallyRendered = cellsRendered; // Actual cells rendered (mode-aware)
				metrics.DirtyCellsMarked = dirtyCountBeforeRender; // Captured before rendering
				metrics.CharactersChanged = dirtyCountBeforeRender; // Number of characters that changed

				// Capture output snapshot
				if (_diagnostics?.EnabledLayers.HasFlag(Configuration.DiagnosticsLayers.ConsoleOutput) == true)
				{
					_diagnostics.CaptureConsoleOutput(output);
				}

				// Record metrics
				_diagnostics?.RecordMetrics(metrics);
			}

			// FIX25: Re-enable mouse tracking after rendering completes
			if (_options.Fix25_DisableMouseDuringRender)
			{
				// Re-enable mouse tracking in same order as NetConsoleDriver initialization
				Console.Out.Write("\x1b[?1000h");  // Enable basic mouse reporting
				Console.Out.Write("\x1b[?1002h");  // Enable button event tracking
				Console.Out.Write("\x1b[?1003h");  // Enable any event mouse
				Console.Out.Flush();
			}
		}
	}


		private void ClearArea(int x, int y, int length)
		{
			length = Math.Min(length, _width - x);
			for (int i = 0; i < length; i++)
			{
				ref var cell = ref _backBuffer[x + i, y];

				// FIX7: Only mark dirty if actually changing the cell
				if (_options.Fix7_ClearAreaConditional)
				{
					if (cell.Character != ' ' || cell.AnsiEscape != string.Empty)
					{
						cell.Character = ' ';
						cell.AnsiEscape = string.Empty;
					}
				}
				else
				{
					// Original: always mark dirty
					cell.Character = ' ';
					cell.AnsiEscape = string.Empty;
				}
			}
		}

		private void InitializeBuffers()
		{
			// Use a single cell template to initialize both buffers
			var template = new Cell();

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
			string lastOutputAnsi = string.Empty;  // FIX13: Track last ANSI output to avoid redundancy

			// FIX14: Track line length before appending (for accurate measurement)
			int lineStartLength = builder.Length;
			int ansiChanges = 0;  // Count how many times ANSI actually changes
			int cellsWritten = 0;  // Count cells written

			// FIX6: Limit to console window width to prevent writing beyond visible area
			int maxWidth = _options.Fix6_WidthLimit ? Math.Min(_width, Console.WindowWidth) : _width;
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

					// FIX13: Only output ANSI if it's different from the last one we output
					if (_options.Fix13_OptimizeAnsiOutput)
					{
						if (backCell.AnsiEscape != lastOutputAnsi)
						{
							// FIX15: Fix infinite re-render bug by NOT using continue
							if (!string.IsNullOrEmpty(backCell.AnsiEscape))
							{
								if (_options.Fix15_FixBufferSyncBug)
								{
									// NEW: Skip only malformed ANSI, always sync buffers
									if (backCell.AnsiEscape.StartsWith("\x1b[") && char.IsLetter(backCell.AnsiEscape[^1]))
									{
										builder.Append(backCell.AnsiEscape);
										lastOutputAnsi = backCell.AnsiEscape;
										ansiChanges++;  // FIX14: Count ANSI changes
									}
									else
									{
										// Skip malformed ANSI sequence but continue to output character
										// DO NOT use continue - we must sync buffers and output character
									}
								}
								else
								{
									// OLD BUGGY CODE: Using continue causes infinite re-render
									if (!backCell.AnsiEscape.StartsWith("\x1b[") || !char.IsLetter(backCell.AnsiEscape[^1]))
									{
										continue;  // BUG: Skips buffer sync, causes infinite re-render
									}
									builder.Append(backCell.AnsiEscape);
									lastOutputAnsi = backCell.AnsiEscape;
									ansiChanges++;
								}
							}
							else
							{
								// Empty ANSI is valid, update tracking
								lastOutputAnsi = backCell.AnsiEscape;
							}
						}
					}
					else
					{
						// Original: always output ANSI (for comparison testing)
						// FIX15: Fix infinite re-render bug by NOT using continue
						if (!string.IsNullOrEmpty(backCell.AnsiEscape))
						{
							if (_options.Fix15_FixBufferSyncBug)
							{
								// NEW: Skip only malformed ANSI, always sync buffers
								if (backCell.AnsiEscape.StartsWith("\x1b[") && char.IsLetter(backCell.AnsiEscape[^1]))
								{
									builder.Append(backCell.AnsiEscape);
									ansiChanges++;  // FIX14: Count ANSI changes
								}
								else
								{
									// Skip malformed ANSI sequence but continue to output character
									// DO NOT use continue - we must sync buffers and output character
								}
							}
							else
							{
								// OLD BUGGY CODE: Using continue causes infinite re-render
								if (!backCell.AnsiEscape.StartsWith("\x1b[") || !char.IsLetter(backCell.AnsiEscape[^1]))
								{
									continue;  // BUG: Skips buffer sync, causes infinite re-render
								}
								builder.Append(backCell.AnsiEscape);
								ansiChanges++;
							}
						}
					}
					// FIX15: CRITICAL - Always output character and sync buffers, even if ANSI was malformed
					builder.Append(backCell.Character);
					cellsWritten++;  // FIX14: Count cells written
					frontCell.CopyFrom(backCell);
				}
				else
				{
					consecutiveUnchanged++;
				}
			}

		// FIX12: Add ANSI reset after line to prevent formatting bleed at edge
		// When cursor reaches position 204 after writing to column 203, active ANSI
		// formatting can cause artifacts. Reset ensures clean state.
		if (_options.Fix12_ResetAfterLine && maxWidth > 0)
		{
			builder.Append(ResetSequence);
		}

	}

		// Use struct for better memory layout and performance
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