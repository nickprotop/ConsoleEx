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
/// Bridges <c>Microsoft.Extensions.Logging</c> into this library's <see cref="ILogService"/>,
/// so an application's existing <see cref="ILogger"/> calls appear in
/// <see cref="Controls.LogViewerControl"/> alongside the library's own entries.
/// </summary>
/// <remarks>
/// <para>
/// No dependency-injection container is required. Construct it and keep it:
/// </para>
/// <code>
/// var bridge = new SharpConsoleUILoggerProvider(windowSystem.LogService);
/// ILogger logger = bridge.CreateLogger("MyApp.Startup");
/// logger.LogInformation("ready");
/// </code>
/// <para>
/// An application that does use the hosting pipeline can pass the same object to it,
/// because this is an ordinary <see cref="ILoggerProvider"/>:
/// </para>
/// <code>
/// builder.Logging.AddProvider(new SharpConsoleUILoggerProvider(windowSystem.LogService));
/// </code>
/// <para>
/// To go the other way, and send the library's own diagnostics out to the application's
/// pipeline, use <see cref="LogServiceForwarder"/>. Wiring both at once is safe: the two
/// suppress each other's entries rather than looping.
/// </para>
/// <para>
/// This provider does NOT change <see cref="ILogService.MinimumLevel"/>, which defaults to
/// <see cref="LogLevel.Warning"/>. Entries below it are dropped by the log service, so an
/// application that wants Information or Debug in the viewer sets the level itself:
/// <c>windowSystem.LogService.MinimumLevel = LogLevel.Debug;</c>. Filtering configured in the
/// logging pipeline still applies first; the log service's level is a second, independent floor.
/// </para>
/// <para>
/// Scopes and event IDs have no field in <see cref="LogEntry"/> and are folded into the message
/// text instead, so no information is silently discarded. The logger category is stored verbatim,
/// so a full type name from <c>ILogger&lt;T&gt;</c> is preserved and searchable even though the
/// viewer's category column may clip it for display.
/// </para>
/// </remarks>
public sealed class SharpConsoleUILoggerProvider : ILoggerProvider
{
	private readonly ILogService _logService;

	/// <summary>
	/// Creates a provider that writes every log entry it receives into the given log service.
	/// </summary>
	/// <param name="logService">
	/// The destination, normally <c>windowSystem.LogService</c>.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="logService"/> is null.</exception>
	public SharpConsoleUILoggerProvider(ILogService logService)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));
	}

	/// <summary>
	/// Creates a logger whose entries are written to the log service under the given category.
	/// </summary>
	/// <param name="categoryName">
	/// The category, stored verbatim. The hosting pipeline passes the full type name for
	/// <c>ILogger&lt;T&gt;</c>.
	/// </param>
	/// <returns>A logger bound to this provider's log service.</returns>
	public ILogger CreateLogger(string categoryName)
		=> new LogServiceLogger(_logService, categoryName ?? string.Empty);

	/// <summary>
	/// Does nothing. The log service is owned by the caller (normally the window system),
	/// so this provider never disposes it.
	/// </summary>
	public void Dispose()
	{
		// Intentionally empty: we borrow the ILogService, we do not own it. Disposing the
		// window system's log service here would tear down logging for the whole application
		// when a hosting pipeline disposes its providers.
	}

	/// <summary>Maps a pipeline level onto the library's own, which mirrors it one to one.</summary>
	internal static LogLevel MapLevel(MelLogLevel level) => level switch
	{
		MelLogLevel.Trace => LogLevel.Trace,
		MelLogLevel.Debug => LogLevel.Debug,
		MelLogLevel.Information => LogLevel.Information,
		MelLogLevel.Warning => LogLevel.Warning,
		MelLogLevel.Error => LogLevel.Error,
		MelLogLevel.Critical => LogLevel.Critical,
		MelLogLevel.None => LogLevel.None,
		// Defensive: a value outside the enum can only come from a cast. Treat it as the
		// highest severity rather than dropping it, so nothing disappears unexplained.
		_ => LogLevel.Critical
	};

	/// <summary>
	/// The <see cref="ILogger"/> handed to the application. Holds no state beyond its category
	/// and its destination, so it is safe to create one per category and keep it.
	/// </summary>
	private sealed class LogServiceLogger : ILogger
	{
		private readonly ILogService _logService;
		private readonly string _category;

		// Scope state is per-thread: a scope opened on one thread must not decorate messages
		// logged on another. AsyncLocal carries it across awaits within that logical flow.
		private static readonly System.Threading.AsyncLocal<ScopeNode?> _scope = new();

		public LogServiceLogger(ILogService logService, string category)
		{
			_logService = logService;
			_category = category;
		}

		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			var parent = _scope.Value;
			_scope.Value = new ScopeNode(state.ToString(), parent);
			return new ScopeHandle(parent);
		}

		// Ask the log service, so a level it would discard costs no formatting. Null is possible
		// when a caller constructs the logger directly, hence the null-conditional.
		public bool IsEnabled(MelLogLevel logLevel)
			=> logLevel != MelLogLevel.None
				&& _logService is { IsEnabled: true }
				&& MapLevel(logLevel) >= _logService.MinimumLevel;

		public void Log<TState>(
			MelLogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel == MelLogLevel.None) return;
			if (formatter is null) return;

			// This entry is one LogServiceForwarder is currently carrying OUT to the pipeline.
			// Writing it back into the log service would duplicate it and, with both bridges
			// wired, loop. See LogBridgeReentry.
			if (LogBridgeReentry.IsOutbound) return;

			var message = formatter(state, exception);

			// An event id carries meaning the message often omits, and LogEntry has no field
			// for it. Prefix rather than drop.
			if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
			{
				var id = string.IsNullOrEmpty(eventId.Name)
					? eventId.Id.ToString()
					: $"{eventId.Id}:{eventId.Name}";
				message = $"[{id}] {message}";
			}

			var scopes = DescribeScopes();
			if (scopes is not null)
				message = $"{scopes} {message}";

			var level = MapLevel(logLevel);

			// Marked for the duration of the write: the log service raises LogAdded
			// synchronously, so a LogServiceForwarder on the same service sees this mark and
			// does not send the entry back to the pipeline it just came from.
			using (LogBridgeReentry.EnterInbound())
			{
				// Route through the typed members so file logging and the LogAdded event behave
				// exactly as they do for the library's own entries.
				switch (level)
				{
					case LogLevel.Error:
						_logService.LogError(message, exception, _category);
						break;
					case LogLevel.Critical:
						_logService.LogCritical(message, exception, _category);
						break;
					default:
						// Trace/Debug/Information/Warning have no exception overload. Append it
						// so a logged exception at these levels is not lost.
						if (exception is not null)
							message = $"{message} | {exception.GetType().Name}: {exception.Message}";
						_logService.Log(level, message, _category);
						break;
				}
			}
		}

		/// <summary>Renders the active scope chain outermost-first, or null when there is none.</summary>
		private static string? DescribeScopes()
		{
			var node = _scope.Value;
			if (node is null) return null;

			var parts = new List<string>();
			for (var n = node; n is not null; n = n.Parent)
			{
				if (!string.IsNullOrEmpty(n.Text))
					parts.Add(n.Text!);
			}

			if (parts.Count == 0) return null;
			parts.Reverse();
			return $"[{string.Join(" => ", parts)}]";
		}

		private sealed class ScopeNode
		{
			public ScopeNode(string? text, ScopeNode? parent)
			{
				Text = text;
				Parent = parent;
			}

			public string? Text { get; }
			public ScopeNode? Parent { get; }
		}

		/// <summary>
		/// Restores the enclosing scope on dispose. Restoring the captured parent rather than
		/// popping means an out-of-order or repeated dispose cannot corrupt the chain.
		/// </summary>
		private sealed class ScopeHandle : IDisposable
		{
			private readonly ScopeNode? _parent;
			private bool _disposed;

			public ScopeHandle(ScopeNode? parent) => _parent = parent;

			public void Dispose()
			{
				if (_disposed) return;
				_disposed = true;
				_scope.Value = _parent;
			}
		}
	}
}
