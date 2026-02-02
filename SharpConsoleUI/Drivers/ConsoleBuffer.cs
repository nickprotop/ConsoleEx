// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
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

		// ===== DOUBLE-BUFFERING FIX TOGGLES =====
		// Set each to true/false to enable/disable specific optimizations
		private const bool FIX1_DISABLE_PRECLEAR = true;         // Disable pre-clearing to reduce dirty cells
		private const bool FIX2_CONDITIONAL_DIRTY = true;        // Only mark cells dirty if content changed
		private const bool FIX3_NO_ANSI_ACCUMULATION = true;   // TEMP DISABLED Prevent ANSI escape sequence accumulation (ENABLED)
		private const bool FIX4_ISLINEDIRTY_EQUALS = true;   // TEMP DISABLED Use Equals comparison in IsLineDirty
		private const bool FIX5_APPENDLINE_EQUALS = true;   // TEMP DISABLED Use Equals comparison in AppendLineToBuilder
		private const bool FIX6_WIDTH_LIMIT = false;  // TEMP DISABLED Limit rendering to Console.WindowWidth
		private const bool FIX7_CLEARAREA_CONDITIONAL = true;    // Only clear cells if they're not already empty
		private const bool FIX12_RESET_AFTER_LINE = true;        // Add ANSI reset after line to prevent edge artifacts
		private const bool FIX13_OPTIMIZE_ANSI_OUTPUT = true;    // Only output ANSI when it changes (prevents massive bloat)
		private const bool FIX15_FIX_BUFFER_SYNC_BUG = true;     // CRITICAL: Fix infinite re-render bug (always sync buffers, skip only malformed ANSI)

		// ===== DIAGNOSTIC LOGGING TOGGLES =====
		// Enable these to debug right-edge artifacts
		private const bool FIX8_LOG_EDGE_WRITES = false;         // Log writes near right edge (column width-2 and beyond)
		private const bool FIX9_LOG_LINE_OUTPUT = false;         // Log final line output for first 5 lines
		private const bool FIX10_SNAPSHOT_EDGE_CELLS = false;    // Snapshot rightmost cells after AddContent
		private const bool FIX14_LOG_FRAME_STATS = true;         // Log frame statistics (dirty cells, output size, double-buffer efficiency)
		private const bool FIX21_LOG_MOUSE_ANSI = true;          // CRITICAL: Detect and log mouse ANSI sequences in output
		private const bool FIX24_DRAIN_INPUT_BEFORE_RENDER = false; // Drain input buffer before rendering to prevent race condition

		private const bool FIX25_DISABLE_MOUSE_DURING_RENDER = false; // Temporarily disable mouse tracking during rendering to prevent echo

		// Diagnostic log file path
		private const string DIAGNOSTIC_LOG_FILE = "/tmp/consolebuffer_diagnostics.log";
		private static readonly object _logLock = new object();
		private static bool _logFileInitialized = false;

		// Helper method for diagnostic logging
		public static void LogDiagnostic(string message)
		{
			try
			{
				lock (_logLock)
				{
					// Clear log file on first write
					if (!_logFileInitialized)
					{
						File.WriteAllText(DIAGNOSTIC_LOG_FILE, $"=== ConsoleBuffer Diagnostic Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===\n");
						_logFileInitialized = true;
					}

					// Append with timestamp
					string timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
					File.AppendAllText(DIAGNOSTIC_LOG_FILE, timestampedMessage);
				}
			}
			catch
			{
				// Silently ignore logging errors to not break the application
			}
		}

		// Cached regex for better performance
		private static readonly Regex _ansiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private readonly Cell[,] _backBuffer;
		private readonly Cell[,] _frontBuffer;
		private readonly int _height;

		// StringBuilder for render operations to minimize string allocations
		private readonly StringBuilder _renderBuilder = new(1024);

		private readonly int _width;
		private readonly object? _consoleLock; // Shared lock for thread-safe Console I/O

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleBuffer"/> class with the specified dimensions.
		/// </summary>
		/// <param name="width">The width of the buffer in characters.</param>
		/// <param name="height">The height of the buffer in lines.</param>
		/// <param name="consoleLock">Optional shared lock for thread-safe Console I/O operations.</param>
		public ConsoleBuffer(int width, int height, object? consoleLock = null)
		{
			_width = width;
			_height = height;
			_consoleLock = consoleLock;
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

			// FIX21: Detect mouse ANSI at source - catch it being written TO buffer
			if (FIX21_LOG_MOUSE_ANSI && (content.Contains("\x1b[<") || content.Contains("[<")))
			{
				string safeContent = content.Replace("\x1b", "\\e").Replace("\n", "\\n").Replace("\r", "\\r");
				LogDiagnostic($"[FIX21] 🔴 MOUSE ANSI WRITTEN TO BUFFER at ({x},{y}) {(y >= _height - 5 ? "🔴 BOTTOM" : "")}: {safeContent}");
				System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
				for (int i = 1; i <= 3 && i < st.FrameCount; i++)
				{
					var frame = st.GetFrame(i);
					LogDiagnostic($"[FIX21]   Frame {i}: {frame?.GetMethod()?.DeclaringType?.Name}.{frame?.GetMethod()?.Name}");
				}
			}

			// Remove trailing reset sequence if present
			if (content.EndsWith(ResetSequence))
				content = content[..^ResetSequence.Length];

			var activeAnsiSequence = new StringBuilder(64); // Pre-size for typical ANSI sequences
			int contentPos = 0;
			int bufferX = x;
			int contentLength = AnsiConsoleHelper.StripAnsiStringLength(content);

			// Clear the area where new content will be written
		// FIX1: Pre-clearing can be disabled for better double-buffering
		if (!FIX1_DISABLE_PRECLEAR)
		{
			ClearArea(x, y, contentLength);
		}

			// Single-pass state machine parser for ANSI sequences
		// FIX6: Also limit width in AddContent to prevent writing beyond console
		int maxBufferX = FIX6_WIDTH_LIMIT ? Math.Min(_width, Console.WindowWidth) : _width;
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
				if (FIX2_CONDITIONAL_DIRTY)
				{
					char newChar = content[contentPos];
					string newAnsi = activeAnsiSequence.ToString();
					
					// FIX8: Log writes near right edge
					if (FIX8_LOG_EDGE_WRITES && bufferX >= maxBufferX - 2)
					{
						LogDiagnostic($"[FIX8] EdgeWrite: x={bufferX}, y={y}, char='{newChar}', ansi='{newAnsi.Replace("\x1b", "\\e")}', maxX={maxBufferX}");
					}
					
					if (cell.Character != newChar || cell.AnsiEscape != newAnsi)
					{
						cell.Character = newChar;
						cell.AnsiEscape = newAnsi;
						cell.IsDirty = true;
					}
				}
				else
				{
					// Original: always mark dirty
				char newChar = content[contentPos];
				string newAnsi = activeAnsiSequence.ToString();

				// FIX8: Log writes near right edge
				if (FIX8_LOG_EDGE_WRITES && bufferX >= maxBufferX - 2)
				{
					LogDiagnostic($"[FIX8] EdgeWrite: x={bufferX}, y={y}, char='{newChar}', ansi='{newAnsi.Replace("\x1b", "\\e")}', maxX={maxBufferX}");
				}

				cell.Character = newChar;
				cell.AnsiEscape = newAnsi;
					cell.IsDirty = true;
				}
					contentPos++;
					bufferX++;
				}
			}

		// FIX3: End-of-line formatting removed - was causing ANSI sequence accumulation
		// (previously prepended ResetSequence which accumulated unbounded)


		// FIX10: Snapshot edge cells after AddContent completes
		if (FIX10_SNAPSHOT_EDGE_CELLS && IsValidPosition(x, y))
		{
			int edge = Math.Min(_width, Console.WindowWidth) - 1;
			if (edge >= 0 && y < _height)
			{
				ref var cell = ref _backBuffer[edge, y];
				LogDiagnostic($"[FIX10] EdgeCell[{edge},{y}]: char='{cell.Character}', ansi='{cell.AnsiEscape.Replace("\x1b", "\\e")}', dirty={cell.IsDirty}");
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
					if (_backBuffer[x, y].IsDirty)
						count++;
				}
			}
			return count;
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
			Console.CursorVisible = false;

			// FIX24: Drain input buffer to prevent mouse sequences from being echoed during rendering
			// Race condition: If mouse events arrive while we're outputting cursor positioning,
			// they get echoed at our current cursor position, mixing with our content
			if (FIX24_DRAIN_INPUT_BEFORE_RENDER)
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
				if (drained > 0)
				{
					LogDiagnostic($"[FIX24] Drained {drained} input events before rendering");
				}
			}
			// FIX25: Temporarily disable mouse tracking during rendering
			// Problem: .NET Console.ReadKey() toggles echo on/off, creating windows where mouse sequences get echoed
			// Solution: Disable mouse tracking entirely during render to prevent new events from arriving
			if (FIX25_DISABLE_MOUSE_DURING_RENDER)
			{
				// Disable all mouse tracking modes (reverse order of enable)
				Console.Out.Write("\x1b[?1003l");  // Disable any event mouse
				Console.Out.Write("\x1b[?1002l");  // Disable button event tracking
				Console.Out.Write("\x1b[?1000l");  // Disable basic mouse reporting
				Console.Out.Flush();
				LogDiagnostic("[FIX25] Mouse tracking disabled for rendering");
			}


			// Build entire screen in one string for atomic output
			// This eliminates flickering by doing a single write instead of multiple cursor moves
			var screenBuilder = new StringBuilder();

			// FIX14: Track rendering statistics
			int linesRendered = 0;

			for (int y = 0; y < Math.Min(_height, Console.WindowHeight); y++)
			{
				if (!IsLineDirty(y))
					continue;

				// FIX14: Count lines rendered
				linesRendered++;

				// Add ANSI absolute positioning: ESC[row;colH (1-based)
				screenBuilder.Append($"\x1b[{y + 1};1H");

				// Append this line's content to the screen builder
				AppendLineToBuilder(y, screenBuilder);
			}

			// FIX14: Enhanced frame statistics to understand double-buffer efficiency
			if (FIX14_LOG_FRAME_STATS && screenBuilder.Length > 0)
			{
				int totalCells = _width * _height;
				int dirtyCells = GetDirtyCharacterCount();
				int outputBytes = screenBuilder.Length;
				double dirtyPercent = (dirtyCells * 100.0) / totalCells;
				double avgBytesPerLine = linesRendered > 0 ? (double)outputBytes / linesRendered : 0;
				double avgDirtyCellsPerLine = linesRendered > 0 ? (double)dirtyCells / linesRendered : 0;

				// Calculate position sequence overhead: ~10 bytes per line (e.g., "\x1b[25;1H")
				int positionOverhead = linesRendered * 10;
				int contentBytes = outputBytes - positionOverhead;
				double avgContentBytesPerLine = linesRendered > 0 ? (double)contentBytes / linesRendered : 0;

				LogDiagnostic($"[FRAME] dirty={dirtyCells}/{totalCells} ({dirtyPercent:F1}%), " +
				              $"lines={linesRendered}/{_height}, " +
				              $"totalBytes={outputBytes} (pos={positionOverhead}, content={contentBytes}), " +
				              $"avg={avgContentBytesPerLine:F0}b/line, cells/line={avgDirtyCellsPerLine:F1}");
			}

			// FIX21: Detect mouse ANSI sequences in output (should NEVER appear in rendering)
			if (FIX21_LOG_MOUSE_ANSI && screenBuilder.Length > 0)
			{
				string output = screenBuilder.ToString();

				// Check for SGR mouse sequences: ESC[<button;x;yM or ESC[<button;x;ym
				if (output.Contains("\x1b[<") || output.Contains("[<"))
				{
					LogDiagnostic($"[FIX21] ⚠️ MOUSE ANSI DETECTED in output! Pattern: ESC[< or [<");

					// Find all occurrences
					int index = 0;
					while ((index = output.IndexOf("\x1b[<", index)) >= 0)
					{
						int end = Math.Min(index + 30, output.Length);
						string snippet = output.Substring(index, end - index).Replace("\x1b", "\\e");
						LogDiagnostic($"[FIX21]   Position {index}: {snippet}");
						index++;
					}

					index = 0;
					while ((index = output.IndexOf("[<", index)) >= 0)
					{
						int end = Math.Min(index + 30, output.Length);
						string snippet = output.Substring(index, end - index);
						LogDiagnostic($"[FIX21]   Malformed at {index}: {snippet}");
						index++;
					}
				}

				// Check for mouse-specific patterns: <button;x;yM (not RGB colors)
				var mouseMatches = System.Text.RegularExpressions.Regex.Matches(output, @"<\d+;\d+;\d+[Mm]");
				if (mouseMatches.Count > 0)
				{
					LogDiagnostic($"[FIX21] ⚠️ TRUE MOUSE sequences found: {mouseMatches.Count}");
					foreach (System.Text.RegularExpressions.Match match in mouseMatches)
					{
						LogDiagnostic($"[FIX21]   Mouse pattern: {match.Value}");
					}
				}

				// Check for malformed patterns at status bar line (y=68+)
				if (output.Contains("CPU") && (output.Contains("[-e") || output.Contains("[<")))
				{
					int cpuIndex = output.IndexOf("CPU");
					int contextStart = Math.Max(0, cpuIndex - 50);
					int contextEnd = Math.Min(output.Length, cpuIndex + 200);
					string statusContext = output.Substring(contextStart, contextEnd - contextStart).Replace("\x1b", "\\e");
					LogDiagnostic($"[FIX21] ⚠️ Status bar contains suspicious patterns:");
					LogDiagnostic($"[FIX21]   Context: {statusContext}");
				}
			}

			// Single atomic write of entire screen - no cursor jumps, no flicker!
			if (screenBuilder.Length > 0)
			{
				Console.Write(screenBuilder.ToString());
			}

			// FIX25: Re-enable mouse tracking after rendering completes
			if (FIX25_DISABLE_MOUSE_DURING_RENDER)
			{
				// Re-enable mouse tracking in same order as NetConsoleDriver initialization
				Console.Out.Write("\x1b[?1000h");  // Enable basic mouse reporting
				Console.Out.Write("\x1b[?1002h");  // Enable button event tracking
				Console.Out.Write("\x1b[?1003h");  // Enable any event mouse
				Console.Out.Flush();
				LogDiagnostic("[FIX25] Mouse tracking re-enabled after rendering");
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
				if (FIX7_CLEARAREA_CONDITIONAL)
				{
					if (cell.Character != ' ' || cell.AnsiEscape != string.Empty)
					{
						cell.Character = ' ';
						cell.AnsiEscape = string.Empty;
						cell.IsDirty = true;
					}
				}
				else
				{
					// Original: always mark dirty
					cell.Character = ' ';
					cell.AnsiEscape = string.Empty;
					cell.IsDirty = true;
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

			// FIX4: Use Equals comparison only (true double-buffering) or include IsDirty flag
			bool isDirty = FIX4_ISLINEDIRTY_EQUALS
				? !frontCell.Equals(backCell)  // Only check content changes
				: (backCell.IsDirty || !frontCell.Equals(backCell));  // Original: check IsDirty flag too

			if (isDirty)
					return true;
			}
			return false;
		}

		private bool IsValidPosition(int x, int y)
			=> x >= 0 && x < _width && y >= 0 && y < _height;

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
			int maxWidth = FIX6_WIDTH_LIMIT ? Math.Min(_width, Console.WindowWidth) : _width;
			// DEBUG: Log the actual width values
			if (y == 0) LogDiagnostic($"FIX6: _width={_width}, Console.WindowWidth={Console.WindowWidth}, maxWidth={maxWidth}");
			for (int x = 0; x < maxWidth; x++)
			{
				ref var frontCell = ref _frontBuffer[x, y];
				ref var backCell = ref _backBuffer[x, y];

			// FIX5: Use Equals comparison only (true double-buffering) or include IsDirty flag
			bool shouldWrite = FIX5_APPENDLINE_EQUALS
				? !frontCell.Equals(backCell)  // Only check content changes
				: (backCell.IsDirty || !frontCell.Equals(backCell));  // Original: check IsDirty flag too

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
					if (FIX13_OPTIMIZE_ANSI_OUTPUT)
					{
						if (backCell.AnsiEscape != lastOutputAnsi)
						{
							// FIX15: Fix infinite re-render bug by NOT using continue
							if (!string.IsNullOrEmpty(backCell.AnsiEscape))
							{
								if (FIX15_FIX_BUFFER_SYNC_BUG)
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
										LogDiagnostic($"[FIX15] Malformed ANSI at ({x},{y}), skipping sequence but syncing buffers: '{backCell.AnsiEscape.Replace("\x1b", "\\e")}'");
										// DO NOT use continue - we must sync buffers and output character
									}
								}
								else
								{
									// OLD BUGGY CODE: Using continue causes infinite re-render
									if (!backCell.AnsiEscape.StartsWith("\x1b[") || !char.IsLetter(backCell.AnsiEscape[^1]))
									{
										LogDiagnostic($"[OLD BUGGY] Malformed ANSI at ({x},{y}): {backCell.AnsiEscape}");
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
							if (FIX15_FIX_BUFFER_SYNC_BUG)
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
									LogDiagnostic($"[FIX15] Malformed ANSI at ({x},{y}), skipping sequence but syncing buffers: '{backCell.AnsiEscape.Replace("\x1b", "\\e")}'");
									// DO NOT use continue - we must sync buffers and output character
								}
							}
							else
							{
								// OLD BUGGY CODE: Using continue causes infinite re-render
								if (!backCell.AnsiEscape.StartsWith("\x1b[") || !char.IsLetter(backCell.AnsiEscape[^1]))
								{
									LogDiagnostic($"[OLD BUGGY] Malformed ANSI at ({x},{y}): {backCell.AnsiEscape}");
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
					backCell.IsDirty = false;
				}
				else
				{
					consecutiveUnchanged++;
				}
			}

		// FIX12: Add ANSI reset after line to prevent formatting bleed at edge
		// When cursor reaches position 204 after writing to column 203, active ANSI
		// formatting can cause artifacts. Reset ensures clean state.
		if (FIX12_RESET_AFTER_LINE && maxWidth > 0)
		{
			builder.Append(ResetSequence);
		}

		// FIX9: Log final line output for first 5 lines
		if (FIX9_LOG_LINE_OUTPUT && y < 5)
		{
			string lineOutput = builder.ToString();
			// Get just the part for this line (after the last position sequence)
			int lastPos = lineOutput.LastIndexOf($"\x1b[{y + 1};1H");
			string thisLine = lastPos >= 0 ? lineOutput.Substring(lastPos) : lineOutput;
			LogDiagnostic($"[FIX9] Line[{y}]: length={thisLine.Length}, content={thisLine.Replace("\x1b", "\\e")}");
		}

		// FIX14: Enhanced per-line diagnostics to understand dirty cell distribution
		if (FIX14_LOG_FRAME_STATS && cellsWritten > 0)
		{
			int actualLineLength = builder.Length - lineStartLength;  // Length of THIS line only (excluding position sequence)
			double avgBytesPerCell = (double)actualLineLength / cellsWritten;
			double ansiOverheadPercent = ansiChanges > 0 ? (ansiChanges * 20.0 / actualLineLength) * 100 : 0;  // Assume ~20 bytes per ANSI

			// Log all lines with activity to see distribution
			LogDiagnostic($"[PER-LINE] y={y}: dirty={cellsWritten}/{maxWidth} ({(cellsWritten * 100.0 / maxWidth):F1}%), " +
			              $"bytes={actualLineLength}, ansi={ansiChanges}, " +
			              $"avgB/cell={avgBytesPerCell:F1}, ansiOverhead%={ansiOverheadPercent:F0}");
		}

	}

		// Use struct for better memory layout and performance
		private struct Cell
		{
			public string AnsiEscape;
			public char Character;
			public bool IsDirty;

			public Cell()
			{
				AnsiEscape = string.Empty;
				Character = ' ';
				IsDirty = true;
			}

			public void CopyFrom(in Cell other)
			{
				Character = other.Character;
				AnsiEscape = other.AnsiEscape;
				IsDirty = other.IsDirty;
			}

			public bool Equals(in Cell other)
				=> Character == other.Character && AnsiEscape == other.AnsiEscape;

			public void Reset()
			{
				Character = ' ';
				AnsiEscape = string.Empty;
				IsDirty = true;
			}
		}
	}
}