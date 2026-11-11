// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace SharpConsoleUI.Logging;

/// <summary>
/// Forwards this library's internal log entries out to a
/// <c>Microsoft.Extensions.Logging</c> pipeline, so window, focus, render and input
/// diagnostics land in the application's own sink (Seq, Serilog, a file, anything it has
/// configured) beside its own entries.
/// </summary>
/// <remarks>
/// <para>
/// This is the opposite direction to <see cref="SharpConsoleUILoggerProvider"/>. No
/// dependency-injection container is required: construct it, keep it, and dispose it when done.
/// </para>
/// <code>
/// var forwarder = new LogServiceForwarder(windowSystem.LogService, loggerFactory);
/// // ... application runs ...
/// forwarder.Dispose();
/// </code>
/// <para>
/// It does NOT change <see cref="ILogService.MinimumLevel"/>. Entries below that level never
/// reach this forwarder, because the log service filters before raising its event, so an
/// application that wants the library's Trace or Debug detail in its sink sets the level itself
/// and can change it again at any time while running:
/// <c>windowSystem.LogService.MinimumLevel = LogLevel.Trace;</c>
/// </para>
/// <para>
/// Forwarding is synchronous, on whichever thread logged the entry, matching how
/// <see cref="ILogService.LogAdded"/> already behaves. A logging pipeline normally batches
/// or queues its own writes; if a sink blocks instead, it blocks the thread that logged, which
/// for the library's own entries can be the render loop.
/// </para>
/// <para>
/// Wiring both directions at once is safe. An entry this forwarder emits is suppressed if it
/// comes back through <see cref="SharpConsoleUILoggerProvider"/>, so the two cannot feed each
/// other in a loop.
/// </para>
/// </remarks>
public sealed class LogServiceForwarder : IDisposable
{
	private readonly ILogService _logService;
	private readonly ILoggerFactory _loggerFactory;
	private readonly bool _prefixCategories;

	// One logger per category, created on demand. ILoggerFactory.CreateLogger is not
	// guaranteed cheap, and the library logs from a small fixed set of categories.
	private readonly Dictionary<string, ILogger> _loggers = new(StringComparer.Ordinal);
	private readonly object _loggersLock = new();

	private bool _disposed;

	/// <summary>
	/// Starts forwarding entries from <paramref name="logService"/> into
	/// <paramref name="loggerFactory"/>.
	/// </summary>
	/// <param name="logService">The source, normally <c>windowSystem.LogService</c>.</param>
	/// <param name="loggerFactory">The destination pipeline.</param>
	/// <param name="prefixCategories">
	/// When true (the default), the library's bare category names are emitted as
	/// <c>SharpConsoleUI.Window</c>, <c>SharpConsoleUI.Focus</c> and so on, so they are
	/// attributable and filterable as a group in a shared sink. When false, the category is
	/// forwarded verbatim.
	/// </param>
	/// <exception cref="ArgumentNullException">Either argument is null.</exception>
	public LogServiceForwarder(ILogService logService, ILoggerFactory loggerFactory, bool prefixCategories = true)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_prefixCategories = prefixCategories;

		_logService.LogAdded += OnLogAdded;
	}

	/// <summary>The category prefix applied when <c>prefixCategories</c> is on.</summary>
	public const string CategoryPrefix = "SharpConsoleUI";

	/// <summary>
	/// Stops forwarding. The log service and the logger factory are owned by the caller and are
	/// not disposed here.
	/// </summary>
	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		_logService.LogAdded -= OnLogAdded;
	}

	private void OnLogAdded(object? sender, LogEntry entry)
	{
		if (_disposed) return;

		// An entry that ARRIVED through SharpConsoleUILoggerProvider came from the application's
		// own pipeline. Sending it back would duplicate it in the sink that produced it, and
		// loop if that pipeline includes the provider.
		if (LogBridgeReentry.IsInbound) return;

		var level = MapLevel(entry.Level);
		if (level == MelLogLevel.None) return;

		var category = entry.Category;
		var name = string.IsNullOrEmpty(category)
			? CategoryPrefix
			: _prefixCategories ? $"{CategoryPrefix}.{category}" : category;

		ILogger logger;
		try
		{
			logger = GetLogger(name);
		}
		catch
		{
			// A factory that throws or has been disposed must not take down logging.
			return;
		}

		// Marked as outbound for the duration of the emit, so the provider drops anything the
		// pipeline logs synchronously in response (a sink that logs about its own write).
		using (LogBridgeReentry.EnterOutbound())
		{
			try
			{
				if (entry.Exception is not null)
					logger.Log(level, entry.Exception, "{Message}", entry.Message);
				else
					logger.Log(level, "{Message}", entry.Message);
			}
			catch
			{
				// ILogService swallows subscriber exceptions anyway; being explicit keeps a
				// broken sink from silently looking like a logging bug in this library.
			}
		}
	}

	private ILogger GetLogger(string name)
	{
		lock (_loggersLock)
		{
			if (_loggers.TryGetValue(name, out var existing)) return existing;
			var created = _loggerFactory.CreateLogger(name);
			_loggers[name] = created;
			return created;
		}
	}

	/// <summary>Maps the library's level onto the pipeline's, which mirrors it one to one.</summary>
	internal static MelLogLevel MapLevel(LogLevel level) => level switch
	{
		LogLevel.Trace => MelLogLevel.Trace,
		LogLevel.Debug => MelLogLevel.Debug,
		LogLevel.Information => MelLogLevel.Information,
		LogLevel.Warning => MelLogLevel.Warning,
		LogLevel.Error => MelLogLevel.Error,
		LogLevel.Critical => MelLogLevel.Critical,
		LogLevel.None => MelLogLevel.None,
		_ => MelLogLevel.Critical
	};
}
