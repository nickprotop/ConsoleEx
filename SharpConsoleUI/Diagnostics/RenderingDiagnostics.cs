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
	public int CurrentFrameNumber => _frameNumber;

	/// <summary>
	/// Gets the most recent CharacterBuffer snapshot.
	/// </summary>
	public CharacterBufferSnapshot? LastBufferSnapshot => GetSnapshot<CharacterBufferSnapshot>(_frameNumber);

	/// <summary>
	/// Gets the most recent ANSI lines snapshot.
	/// </summary>
	public AnsiLinesSnapshot? LastAnsiSnapshot => GetSnapshot<AnsiLinesSnapshot>(_frameNumber);

	/// <summary>
	/// Gets the most recent ConsoleBuffer snapshot.
	/// </summary>
	public ConsoleBufferSnapshot? LastConsoleSnapshot => GetSnapshot<ConsoleBufferSnapshot>(_frameNumber);

	/// <summary>
	/// Gets the most recent output snapshot.
	/// </summary>
	public RenderOutputSnapshot? LastOutputSnapshot => GetSnapshot<RenderOutputSnapshot>(_frameNumber);

	/// <summary>
	/// Gets the most recent metrics.
	/// </summary>
	public RenderingMetrics? LastMetrics => GetMetrics(_frameNumber);

	/// <summary>
	/// Gets the most recent quality report.
	/// </summary>
	public QualityReport? LastQualityReport => GetQualityReport(_frameNumber);

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

		_frameNumber++;
		CleanupOldFrames();
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

		var snapshot = new CharacterBufferSnapshot
		{
			Width = buffer.Width,
			Height = buffer.Height,
			Cells = cells,
			DirtyCells = dirtyCellsList,
			DirtyRegion = dirtyRegion,
			Timestamp = DateTime.UtcNow,
			FrameNumber = _frameNumber
		};

		_bufferSnapshots[_frameNumber] = snapshot;
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

		var snapshot = new AnsiLinesSnapshot
		{
			Lines = new List<string>(lines),
			TotalAnsiEscapes = totalAnsi,
			TotalCharacters = totalChars,
			Timestamp = DateTime.UtcNow,
			FrameNumber = _frameNumber
		};

		_ansiSnapshots[_frameNumber] = snapshot;
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

		var snapshot = new ConsoleBufferSnapshot
		{
			FrontBuffer = frontCopy,
			BackBuffer = backCopy,
			DirtyCells = dirtyCells,
			Width = width,
			Height = height,
			Timestamp = DateTime.UtcNow,
			FrameNumber = _frameNumber
		};

		_consoleSnapshots[_frameNumber] = snapshot;
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

		var snapshot = new RenderOutputSnapshot
		{
			FullOutput = output,
			BytesWritten = output.Length,
			CursorMoves = cursorMoves,
			Timestamp = DateTime.UtcNow,
			FrameNumber = _frameNumber
		};

		_outputSnapshots[_frameNumber] = snapshot;
	}

	/// <summary>
	/// Records rendering metrics for a frame.
	/// </summary>
	public void RecordMetrics(RenderingMetrics metrics)
	{
		if (!IsEnabled) return;

		metrics.FrameNumber = _frameNumber;
		metrics.Timestamp = DateTime.UtcNow;

		_metricsHistory[_frameNumber] = metrics;

		// Generate quality report if enabled
		if (_enableQualityAnalysis)
		{
			var ansi = _ansiSnapshots.GetValueOrDefault(_frameNumber);
			var console = _consoleSnapshots.GetValueOrDefault(_frameNumber);

			var report = _qualityAnalyzer.Analyze(ansi, console, metrics);
			_qualityReports[_frameNumber] = report;
		}
	}

	/// <summary>
	/// Gets a snapshot of a specific type for a given frame.
	/// </summary>
	public T? GetSnapshot<T>(int frameNumber) where T : class
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

	/// <summary>
	/// Gets metrics for a specific frame.
	/// </summary>
	public RenderingMetrics? GetMetrics(int frameNumber)
	{
		return _metricsHistory.GetValueOrDefault(frameNumber);
	}

	/// <summary>
	/// Gets quality report for a specific frame.
	/// </summary>
	public QualityReport? GetQualityReport(int frameNumber)
	{
		return _qualityReports.GetValueOrDefault(frameNumber);
	}

	/// <summary>
	/// Gets all recorded metrics.
	/// </summary>
	public IReadOnlyList<RenderingMetrics> GetAllMetrics()
	{
		return _metricsHistory.Values.OrderBy(m => m.FrameNumber).ToList();
	}

	/// <summary>
	/// Clears all captured diagnostics data.
	/// </summary>
	public void Clear()
	{
		_bufferSnapshots.Clear();
		_ansiSnapshots.Clear();
		_consoleSnapshots.Clear();
		_outputSnapshots.Clear();
		_metricsHistory.Clear();
		_qualityReports.Clear();
		_frameNumber = 0;
	}

	/// <summary>
	/// Removes old frames beyond the retention limit.
	/// </summary>
	private void CleanupOldFrames()
	{
		if (_retainFrames <= 0) return;

		int oldestFrameToKeep = _frameNumber - _retainFrames;

		// Remove old snapshots
		foreach (var frame in _bufferSnapshots.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_bufferSnapshots.Remove(frame);

		foreach (var frame in _ansiSnapshots.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_ansiSnapshots.Remove(frame);

		foreach (var frame in _consoleSnapshots.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_consoleSnapshots.Remove(frame);

		foreach (var frame in _outputSnapshots.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_outputSnapshots.Remove(frame);

		foreach (var frame in _metricsHistory.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_metricsHistory.Remove(frame);

		foreach (var frame in _qualityReports.Keys.Where(f => f < oldestFrameToKeep).ToList())
			_qualityReports.Remove(frame);
	}
}
