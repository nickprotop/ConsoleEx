// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Diagnostics.Analysis;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Diagnostics;

/// <summary>
/// Main coordinator for capturing rendering artifacts and metrics across all three layers.
/// Provides snapshot capture, metrics collection, and quality analysis for testing and debugging.
/// </summary>
public class RenderingDiagnostics
{
	/// <summary>
	/// Guards every dictionary below, and <see cref="_frameNumber"/>.
	/// </summary>
	/// <remarks>
	/// The render thread writes these each frame while other threads read them: the
	/// diagnostics socket serves <c>snapshot</c> and <c>image</c> from
	/// <see cref="LastConsoleSnapshot"/> on a socket thread, and tests read metrics from
	/// their own. A plain <see cref="Dictionary{TKey, TValue}"/> enumerated while another
	/// thread adds to it throws, which is exactly how this surfaced - a full test run
	/// failing inside <see cref="CleanupOldFramesLocked"/> roughly one run in two.
	/// <para>
	/// One lock rather than six concurrent dictionaries, because the operations that matter
	/// span several of them: a cleanup pass must not be half-applied when a reader looks,
	/// and <see cref="LatestBufferSnapshotLocked"/> scanning for the highest key has no
	/// meaningful answer while keys are being removed. Contention is not a concern - a frame
	/// holds it for the length of a dictionary insert.
	/// </para>
	/// </remarks>
	private readonly object _gate = new();

	private int _frameNumber = 0;
	private readonly int _retainFrames;
	private readonly DiagnosticsLayers _enabledLayers;
	private readonly bool _enableQualityAnalysis;

	// Snapshot histories (ring buffers)
	private readonly Dictionary<int, CharacterBufferSnapshot> _bufferSnapshots = new();
	private readonly Dictionary<int, AnsiLinesSnapshot> _ansiSnapshots = new();
	private readonly Dictionary<int, ConsoleBufferSnapshot> _consoleSnapshots = new();
	private readonly Dictionary<int, RenderOutputSnapshot> _outputSnapshots = new();

	// Metrics and quality reports
	private readonly Dictionary<int, RenderingMetrics> _metricsHistory = new();
	private readonly Dictionary<int, QualityReport> _qualityReports = new();

	private readonly QualityAnalyzer _qualityAnalyzer = new();

	/// <summary>
	/// Gets whether diagnostics are enabled.
	/// </summary>
	public bool IsEnabled { get; set; }

	/// <summary>
	/// Gets the enabled diagnostics layers.
	/// </summary>
	public DiagnosticsLayers EnabledLayers => _enabledLayers;

	/// <summary>
	/// Gets the current frame number.
	/// </summary>
	public int CurrentFrameNumber { get { lock (_gate) return _frameNumber; } }

	/// <summary>
	/// Gets the most recent CharacterBuffer snapshot. Falls back to the latest earlier frame that
	/// captured a buffer when the current frame did not render one (e.g. a no-op
	/// <c>UpdateDisplay()</c> that advanced the frame counter without repainting), so callers always
	/// see the last painted screen rather than a transient null.
	/// </summary>
	public CharacterBufferSnapshot? LastBufferSnapshot
	{
		get
		{
			lock (_gate)
			{
				return _bufferSnapshots.GetValueOrDefault(_frameNumber) ?? LatestBufferSnapshotLocked();
			}
		}
	}

	/// <summary>
	/// Gets the most recent ANSI lines snapshot.
	/// </summary>
	public AnsiLinesSnapshot? LastAnsiSnapshot
	{
		get { lock (_gate) return _ansiSnapshots.GetValueOrDefault(_frameNumber); }
	}

	/// <summary>
	/// Gets the most recent ConsoleBuffer snapshot.
	/// </summary>
	public ConsoleBufferSnapshot? LastConsoleSnapshot
	{
		get { lock (_gate) return _consoleSnapshots.GetValueOrDefault(_frameNumber); }
	}

	/// <summary>
	/// Gets the most recent output snapshot.
	/// </summary>
	public RenderOutputSnapshot? LastOutputSnapshot
	{
		get { lock (_gate) return _outputSnapshots.GetValueOrDefault(_frameNumber); }
	}

	/// <summary>
	/// Gets the most recent metrics.
	/// </summary>
	public RenderingMetrics? LastMetrics
	{
		get { lock (_gate) return _metricsHistory.GetValueOrDefault(_frameNumber); }
	}

	/// <summary>
	/// Gets the most recent quality report.
	/// </summary>
	public QualityReport? LastQualityReport
	{
		get { lock (_gate) return _qualityReports.GetValueOrDefault(_frameNumber); }
	}

	/// <summary>
	/// Creates a new RenderingDiagnostics instance.
	/// </summary>
	public RenderingDiagnostics(ConsoleWindowSystemOptions options)
	{
		IsEnabled = options.EnableDiagnostics;
		_retainFrames = options.DiagnosticsRetainFrames;
		_enabledLayers = options.DiagnosticsLayers;
		_enableQualityAnalysis = options.EnableQualityAnalysis;
	}

	/// <summary>
	/// Begins a new frame, incrementing the frame counter.
	/// </summary>
	public void BeginFrame()
	{
		if (!IsEnabled) return;

		lock (_gate)
		{
			_frameNumber++;
			CleanupOldFramesLocked();
		}
	}

	/// <summary>
	/// Captures a CharacterBuffer snapshot (TOP LAYER).
	/// </summary>
	public void CaptureCharacterBuffer(CharacterBuffer buffer)
	{
		if (!IsEnabled || !_enabledLayers.HasFlag(DiagnosticsLayers.CharacterBuffer))
			return;

		// Use built-in snapshot functionality for deep copy
		var coreSnapshot = buffer.CreateSnapshot();
		var cells = coreSnapshot.Cells;

		// Capture dirty cells
		var dirtyCellsList = buffer.GetDirtyCells().ToList();

		// Calculate dirty region from dirty cells
		LayoutRect dirtyRegion = LayoutRect.Empty;
		if (dirtyCellsList.Count > 0)
		{
			int minX = dirtyCellsList.Min(c => c.X);
			int minY = dirtyCellsList.Min(c => c.Y);
			int maxX = dirtyCellsList.Max(c => c.X) + 1;
			int maxY = dirtyCellsList.Max(c => c.Y) + 1;
			dirtyRegion = new LayoutRect(minX, minY, maxX - minX, maxY - minY);
		}

		// The frame number is read and the entry written under one lock, so a snapshot's
		// FrameNumber always matches the key it is filed under.
		lock (_gate)
		{
			_bufferSnapshots[_frameNumber] = new CharacterBufferSnapshot
			{
				Width = buffer.Width,
				Height = buffer.Height,
				Cells = cells,
				DirtyCells = dirtyCellsList,
				DirtyRegion = dirtyRegion,
				Timestamp = DateTime.UtcNow,
				FrameNumber = _frameNumber
			};
		}
	}

	/// <summary>
	/// Captures ANSI lines snapshot (MIDDLE LAYER).
	/// </summary>
	public void CaptureAnsiLines(List<string> lines)
	{
		if (!IsEnabled || !_enabledLayers.HasFlag(DiagnosticsLayers.AnsiLines))
			return;

		// Count ANSI escapes and characters
		int totalAnsi = 0;
		int totalChars = 0;

		foreach (var line in lines)
		{
			totalAnsi += System.Text.RegularExpressions.Regex.Matches(line, @"\x1b\[[0-9;]*[a-zA-Z]").Count;
			totalChars += line.Length;
		}

		lock (_gate)
		{
			_ansiSnapshots[_frameNumber] = new AnsiLinesSnapshot
			{
				Lines = new List<string>(lines),
				TotalAnsiEscapes = totalAnsi,
				TotalCharacters = totalChars,
				Timestamp = DateTime.UtcNow,
				FrameNumber = _frameNumber
			};
		}
	}

	/// <summary>
	/// Captures ConsoleBuffer internal state (BOTTOM LAYER).
	/// </summary>
	public void CaptureConsoleBufferState(ConsoleCell[,] front, ConsoleCell[,] back, int width, int height)
	{
		if (!IsEnabled || !_enabledLayers.HasFlag(DiagnosticsLayers.ConsoleBuffer))
			return;

		// Deep copy buffers
		var frontCopy = new ConsoleCell[width, height];
		var backCopy = new ConsoleCell[width, height];

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				frontCopy[x, y] = front[x, y];
				backCopy[x, y] = back[x, y];
			}
		}

		// Find dirty cells
		var dirtyCells = new List<(int x, int y)>();
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (!front[x, y].Equals(back[x, y]))
				{
					dirtyCells.Add((x, y));
				}
			}
		}

		lock (_gate)
		{
			_consoleSnapshots[_frameNumber] = new ConsoleBufferSnapshot
			{
				FrontBuffer = frontCopy,
				BackBuffer = backCopy,
				DirtyCells = dirtyCells,
				Width = width,
				Height = height,
				Timestamp = DateTime.UtcNow,
				FrameNumber = _frameNumber
			};
		}
	}

	/// <summary>
	/// Captures console output string.
	/// </summary>
	public void CaptureConsoleOutput(string output)
	{
		if (!IsEnabled || !_enabledLayers.HasFlag(DiagnosticsLayers.ConsoleOutput))
			return;

		// Count cursor moves
		int cursorMoves = System.Text.RegularExpressions.Regex.Matches(output, @"\x1b\[\d+;\d+H").Count;

		lock (_gate)
		{
			_outputSnapshots[_frameNumber] = new RenderOutputSnapshot
			{
				FullOutput = output,
				BytesWritten = output.Length,
				CursorMoves = cursorMoves,
				Timestamp = DateTime.UtcNow,
				FrameNumber = _frameNumber
			};
		}
	}

	/// <summary>
	/// Records rendering metrics for a frame.
	/// </summary>
	public void RecordMetrics(RenderingMetrics metrics)
	{
		if (!IsEnabled) return;

		AnsiLinesSnapshot? ansi = null;
		ConsoleBufferSnapshot? console = null;
		int frame;

		lock (_gate)
		{
			frame = _frameNumber;
			metrics.FrameNumber = frame;
			metrics.Timestamp = DateTime.UtcNow;

			_metricsHistory[frame] = metrics;

			if (_enableQualityAnalysis)
			{
				ansi = _ansiSnapshots.GetValueOrDefault(frame);
				console = _consoleSnapshots.GetValueOrDefault(frame);
			}
		}

		// Analysis runs OUTSIDE the lock: it is the one genuinely slow thing here, and it
		// reads only the two snapshots pulled out above, which are immutable once filed.
		if (_enableQualityAnalysis)
		{
			var report = _qualityAnalyzer.Analyze(ansi, console, metrics);
			lock (_gate) _qualityReports[frame] = report;
		}
	}

	/// <summary>
	/// Gets a snapshot of a specific type for a given frame.
	/// </summary>
	public T? GetSnapshot<T>(int frameNumber) where T : class
	{
		lock (_gate)
		{
			if (typeof(T) == typeof(CharacterBufferSnapshot))
				return _bufferSnapshots.GetValueOrDefault(frameNumber) as T;

			if (typeof(T) == typeof(AnsiLinesSnapshot))
				return _ansiSnapshots.GetValueOrDefault(frameNumber) as T;

			if (typeof(T) == typeof(ConsoleBufferSnapshot))
				return _consoleSnapshots.GetValueOrDefault(frameNumber) as T;

			if (typeof(T) == typeof(RenderOutputSnapshot))
				return _outputSnapshots.GetValueOrDefault(frameNumber) as T;

			return null;
		}
	}

	/// <summary>
	/// Returns the buffer snapshot from the highest-numbered frame that captured one, or
	/// <c>null</c> if no buffer has been captured. Used as a fallback so <see cref="LastBufferSnapshot"/>
	/// survives trailing no-op frames that advanced the counter without repainting.
	/// </summary>
	/// <remarks>Callers must hold <see cref="_gate"/>: this enumerates the keys.</remarks>
	private CharacterBufferSnapshot? LatestBufferSnapshotLocked()
	{
		if (_bufferSnapshots.Count == 0)
			return null;

		int latest = int.MinValue;
		foreach (var frame in _bufferSnapshots.Keys)
			if (frame > latest)
				latest = frame;

		return _bufferSnapshots.GetValueOrDefault(latest);
	}

	/// <summary>
	/// Gets metrics for a specific frame.
	/// </summary>
	public RenderingMetrics? GetMetrics(int frameNumber)
	{
		lock (_gate) return _metricsHistory.GetValueOrDefault(frameNumber);
	}

	/// <summary>
	/// Gets quality report for a specific frame.
	/// </summary>
	public QualityReport? GetQualityReport(int frameNumber)
	{
		lock (_gate) return _qualityReports.GetValueOrDefault(frameNumber);
	}

	/// <summary>
	/// Gets all recorded metrics.
	/// </summary>
	public IReadOnlyList<RenderingMetrics> GetAllMetrics()
	{
		// Materialised under the lock. Returning a lazy sequence would enumerate the
		// dictionary after the lock was released, which is the bug this guards against.
		lock (_gate) return _metricsHistory.Values.OrderBy(m => m.FrameNumber).ToList();
	}

	/// <summary>
	/// Clears all captured diagnostics data.
	/// </summary>
	public void Clear()
	{
		lock (_gate)
		{
			_bufferSnapshots.Clear();
			_ansiSnapshots.Clear();
			_consoleSnapshots.Clear();
			_outputSnapshots.Clear();
			_metricsHistory.Clear();
			_qualityReports.Clear();
			_frameNumber = 0;
		}
	}

	/// <summary>
	/// Removes old frames beyond the retention limit.
	/// </summary>
	/// <remarks>Callers must hold <see cref="_gate"/>.</remarks>
	private void CleanupOldFramesLocked()
	{
		if (_retainFrames <= 0) return;

		int oldestFrameToKeep = _frameNumber - _retainFrames;

		DropFramesBefore(_bufferSnapshots, oldestFrameToKeep);
		DropFramesBefore(_ansiSnapshots, oldestFrameToKeep);
		DropFramesBefore(_consoleSnapshots, oldestFrameToKeep);
		DropFramesBefore(_outputSnapshots, oldestFrameToKeep);
		DropFramesBefore(_metricsHistory, oldestFrameToKeep);
		DropFramesBefore(_qualityReports, oldestFrameToKeep);
	}

	/// <summary>Drops every entry filed under a frame older than <paramref name="oldest"/>.</summary>
	/// <remarks>
	/// The keys are copied before anything is removed, because removing from a dictionary
	/// invalidates an enumerator over it. Callers must hold <see cref="_gate"/>.
	/// </remarks>
	private static void DropFramesBefore<T>(Dictionary<int, T> history, int oldest)
	{
		if (history.Count == 0) return;

		List<int>? stale = null;
		foreach (int frame in history.Keys)
		{
			if (frame >= oldest) continue;
			(stale ??= new List<int>()).Add(frame);
		}

		if (stale == null) return;
		foreach (int frame in stale)
			history.Remove(frame);
	}
}
