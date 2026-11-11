// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Xunit;
using MelLevel = Microsoft.Extensions.Logging.LogLevel;
using OurLevel = SharpConsoleUI.Logging.LogLevel;

namespace SharpConsoleUI.Tests.Logging;

/// <summary>
/// Tests for the two Microsoft.Extensions.Logging bridges: entries coming IN through
/// <see cref="SharpConsoleUILoggerProvider"/> and going OUT through
/// <see cref="LogServiceForwarder"/>.
/// </summary>
public class MelBridgeTests
{
	// A recorded sink line. Named rather than a bare tuple so the assertions read.
	private readonly record struct Written(MelLevel Level, string Category, string Message, Exception? Exception);

	#region Test doubles

	/// <summary>A sink that records what the pipeline was asked to write.</summary>
	private sealed class RecordingProvider : ILoggerProvider
	{
		private readonly List<Written> _sink;
		private readonly object _lock;

		public RecordingProvider(List<Written> sink, object sinkLock)
		{
			_sink = sink;
			_lock = sinkLock;
		}

		public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, _sink, _lock);

		public void Dispose() { }

		private sealed class Recorder : ILogger
		{
			private readonly string _category;
			private readonly List<Written> _sink;
			private readonly object _lock;

			public Recorder(string category, List<Written> sink, object sinkLock)
			{
				_category = category;
				_sink = sink;
				_lock = sinkLock;
			}

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NullScope();

			public bool IsEnabled(MelLevel logLevel) => true;

			public void Log<TState>(
				MelLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter)
			{
				lock (_lock)
					_sink.Add(new Written(logLevel, _category, formatter(state, exception), exception));
			}

			private sealed class NullScope : IDisposable
			{
				public void Dispose() { }
			}
		}
	}

	private static ILoggerFactory FactoryRecordingInto(
		List<Written> sink, object sinkLock, ILoggerProvider? alsoRegister = null)
	{
		return LoggerFactory.Create(b =>
		{
			b.SetMinimumLevel(MelLevel.Trace);
			b.AddProvider(new RecordingProvider(sink, sinkLock));
			if (alsoRegister is not null) b.AddProvider(alsoRegister);
		});
	}

	#endregion

	#region Inbound: pipeline -> ILogService

	[Fact]
	public void Provider_WritesEntryIntoLogService_WithCategoryAndLevel()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("MyApp.Services.OrderProcessor");

		logger.LogInformation("hello");

		var entry = Assert.Single(svc.GetAllLogs());
		Assert.Equal(OurLevel.Information, entry.Level);
		Assert.Equal("MyApp.Services.OrderProcessor", entry.Category);
		Assert.Equal("hello", entry.Message);
	}

	[Theory]
	[InlineData(MelLevel.Trace, OurLevel.Trace)]
	[InlineData(MelLevel.Debug, OurLevel.Debug)]
	[InlineData(MelLevel.Information, OurLevel.Information)]
	[InlineData(MelLevel.Warning, OurLevel.Warning)]
	[InlineData(MelLevel.Error, OurLevel.Error)]
	[InlineData(MelLevel.Critical, OurLevel.Critical)]
	public void Provider_MapsEveryLevel(MelLevel from, OurLevel expected)
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.Log(from, "m");

		Assert.Equal(expected, Assert.Single(svc.GetAllLogs()).Level);
	}

	[Fact]
	public void Provider_DropsLevelNone()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.Log(MelLevel.None, "should not arrive");

		Assert.Empty(svc.GetAllLogs());
	}

	/// <summary>
	/// The provider must NOT touch MinimumLevel. The default is Warning, so an application that
	/// wants Information in the viewer lowers the level itself; the bridge silently lowering it
	/// would mutate state the application owns.
	/// </summary>
	[Fact]
	public void Provider_RespectsMinimumLevel_AndDoesNotChangeIt()
	{
		var svc = new LogService();   // default floor: Warning
		Assert.Equal(OurLevel.Warning, svc.MinimumLevel);

		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.LogInformation("below the floor");
		logger.LogWarning("at the floor");

		Assert.Equal(OurLevel.Warning, svc.MinimumLevel);
		Assert.Equal("at the floor", Assert.Single(svc.GetAllLogs()).Message);
	}

	/// <summary>The floor is settable while running, which is how an application opts into detail.</summary>
	[Fact]
	public void Provider_HonoursMinimumLevelChangedOnTheFly()
	{
		var svc = new LogService();
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.LogDebug("dropped");
		svc.MinimumLevel = OurLevel.Debug;
		logger.LogDebug("kept");

		Assert.Equal("kept", Assert.Single(svc.GetAllLogs()).Message);
	}

	[Fact]
	public void Provider_IsEnabled_TracksTheFloor()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Warning };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		Assert.False(logger.IsEnabled(MelLevel.Information));
		Assert.True(logger.IsEnabled(MelLevel.Warning));
		Assert.True(logger.IsEnabled(MelLevel.Critical));
		Assert.False(logger.IsEnabled(MelLevel.None));
	}

	[Fact]
	public void Provider_CarriesExceptionOnError()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");
		var boom = new InvalidOperationException("boom");

		logger.LogError(boom, "failed");

		Assert.Same(boom, Assert.Single(svc.GetAllLogs()).Exception);
	}

	/// <summary>
	/// Trace/Debug/Information/Warning have no exception overload on ILogService, so an
	/// exception logged at those levels is appended to the message rather than lost.
	/// </summary>
	[Fact]
	public void Provider_AppendsExceptionAtNonErrorLevels()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.LogWarning(new TimeoutException("slow"), "took too long");

		var entry = Assert.Single(svc.GetAllLogs());
		Assert.Contains("took too long", entry.Message);
		Assert.Contains("TimeoutException", entry.Message);
		Assert.Contains("slow", entry.Message);
	}

	[Fact]
	public void Provider_FoldsEventIdIntoMessage()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		logger.Log(MelLevel.Information, new EventId(7, "Submitted"), "body");

		Assert.Contains("7:Submitted", Assert.Single(svc.GetAllLogs()).Message);
	}

	[Fact]
	public void Provider_FoldsScopesIntoMessage_OutermostFirst()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		using (logger.BeginScope("Order=42"))
		using (logger.BeginScope("Attempt=2"))
		{
			logger.LogInformation("scoped");
		}

		var message = Assert.Single(svc.GetAllLogs()).Message;
		Assert.Contains("Order=42 => Attempt=2", message);
		Assert.Contains("scoped", message);
	}

	[Fact]
	public void Provider_DisposedScope_DoesNotDecorateLaterEntries()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var logger = new SharpConsoleUILoggerProvider(svc).CreateLogger("cat");

		using (logger.BeginScope("Inside")) { logger.LogInformation("a"); }
		logger.LogInformation("b");

		var after = svc.GetAllLogs().Last();
		Assert.Equal("b", after.Message);
		Assert.DoesNotContain("Inside", after.Message);
	}

	/// <summary>
	/// A hosting pipeline disposes its providers at shutdown. That must not dispose the window
	/// system's log service, which the application still owns and may still be using.
	/// </summary>
	[Fact]
	public void Provider_Dispose_DoesNotDisableTheBorrowedService()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var provider = new SharpConsoleUILoggerProvider(svc);

		provider.Dispose();
		svc.LogWarning("still works");

		Assert.True(svc.IsEnabled);
		Assert.Single(svc.GetAllLogs());
	}

	[Fact]
	public void Provider_NullService_Throws()
		=> Assert.Throws<ArgumentNullException>(() => new SharpConsoleUILoggerProvider(null!));

	#endregion

	#region Outbound: ILogService -> pipeline

	[Fact]
	public void Forwarder_SendsLibraryEntriesToThePipeline()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace };

		using var forwarder = new LogServiceForwarder(svc, FactoryRecordingInto(sink, sinkLock));
		svc.LogWarning("focus lost", "Focus");

		var got = Assert.Single(sink);
		Assert.Equal(MelLevel.Warning, got.Level);
		Assert.Equal("SharpConsoleUI.Focus", got.Category);
		Assert.Equal("focus lost", got.Message);
	}

	[Fact]
	public void Forwarder_WithoutPrefix_ForwardsCategoryVerbatim()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace };

		using var forwarder = new LogServiceForwarder(
			svc, FactoryRecordingInto(sink, sinkLock), prefixCategories: false);
		svc.LogWarning("m", "Focus");

		Assert.Equal("Focus", Assert.Single(sink).Category);
	}

	[Fact]
	public void Forwarder_CarriesTheException()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var boom = new InvalidOperationException("boom");

		using var forwarder = new LogServiceForwarder(svc, FactoryRecordingInto(sink, sinkLock));
		svc.LogError("failed", boom, "System");

		Assert.Same(boom, Assert.Single(sink).Exception);
	}

	/// <summary>
	/// Entries below the service's floor never reach the forwarder, because the service filters
	/// before raising its event. Lowering the floor at runtime is how detail is turned on.
	/// </summary>
	[Fact]
	public void Forwarder_FollowsMinimumLevelChangedOnTheFly()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService();   // Warning

		using var forwarder = new LogServiceForwarder(svc, FactoryRecordingInto(sink, sinkLock));

		svc.LogDebug("dropped", "Render");
		Assert.Empty(sink);

		svc.MinimumLevel = OurLevel.Trace;
		svc.LogDebug("forwarded", "Render");

		Assert.Equal("forwarded", Assert.Single(sink).Message);
	}

	[Fact]
	public void Forwarder_Dispose_StopsForwarding()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace };

		var forwarder = new LogServiceForwarder(svc, FactoryRecordingInto(sink, sinkLock));
		forwarder.Dispose();
		svc.LogWarning("after dispose", "Window");

		Assert.Empty(sink);
	}

	[Fact]
	public void Forwarder_NullArguments_Throw()
	{
		var sink = new List<Written>();
		var sinkLock = new object();

		Assert.Throws<ArgumentNullException>(
			() => new LogServiceForwarder(null!, FactoryRecordingInto(sink, sinkLock)));
		Assert.Throws<ArgumentNullException>(
			() => new LogServiceForwarder(new LogService(), null!));
	}

	/// <summary>A sink that throws must not break logging for the application.</summary>
	[Fact]
	public void Forwarder_SinkThatThrows_DoesNotBreakLogging()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var factory = LoggerFactory.Create(b =>
		{
			b.SetMinimumLevel(MelLevel.Trace);
			b.AddProvider(new ThrowingProvider());
		});

		using var forwarder = new LogServiceForwarder(svc, factory);
		svc.LogWarning("still buffered", "Window");

		Assert.Single(svc.GetAllLogs());
	}

	private sealed class ThrowingProvider : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName) => new Thrower();
		public void Dispose() { }

		private sealed class Thrower : ILogger
		{
			public IDisposable BeginScope<TState>(TState state) where TState : notnull => new N();
			public bool IsEnabled(MelLevel logLevel) => true;

			public void Log<TState>(
				MelLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter)
				=> throw new InvalidOperationException("sink is down");

			private sealed class N : IDisposable
			{
				public void Dispose() { }
			}
		}
	}

	#endregion

	#region Both directions at once: the cycle

	/// <summary>
	/// The configuration that would loop: the provider is registered in the very pipeline the
	/// forwarder sends to. Each entry must cross exactly once, in each direction.
	/// </summary>
	[Fact]
	public void BothDirections_EntryCrossesExactlyOnce_AndDoesNotLoop()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace };

		var factory = FactoryRecordingInto(sink, sinkLock, new SharpConsoleUILoggerProvider(svc));
		using var forwarder = new LogServiceForwarder(svc, factory);

		// Inbound: the application logs. One buffer entry, one sink entry.
		factory.CreateLogger("MyApp.Orders").LogInformation("APP");
		Assert.Single(svc.GetAllLogs().Where(e => e.Message == "APP"));
		lock (sinkLock) Assert.Single(sink.Where(s => s.Message == "APP"));

		// Outbound: the library logs. One buffer entry, one sink entry.
		svc.LogWarning("LIB", "Focus");
		Assert.Single(svc.GetAllLogs().Where(e => e.Message == "LIB"));
		lock (sinkLock) Assert.Single(sink.Where(s => s.Message == "LIB"));
	}

	/// <summary>
	/// Volume through a fully cyclic wiring must terminate. Without the re-entry guard this does
	/// not return at all, so the test completing is as much the assertion as the counts are.
	/// </summary>
	[Fact]
	public void BothDirections_UnderVolume_Terminates()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace, MaxBufferSize = 10_000 };

		var factory = FactoryRecordingInto(sink, sinkLock, new SharpConsoleUILoggerProvider(svc));
		using var forwarder = new LogServiceForwarder(svc, factory);
		var appLogger = factory.CreateLogger("MyApp");

		for (int i = 0; i < 200; i++)
		{
			appLogger.LogInformation($"IN-{i}");
			svc.LogWarning($"OUT-{i}", "Render");
		}

		Assert.Equal(400, svc.Count);
		lock (sinkLock) Assert.Equal(400, sink.Count);
	}

	/// <summary>
	/// The guard must not swallow a genuinely new entry logged from ANOTHER thread while a
	/// forward is in flight. An AsyncLocal guard flows into pool work and would suppress it,
	/// which is why the guard is thread-static.
	/// </summary>
	[Fact]
	public async Task Guard_DoesNotSuppressEntriesFromOtherThreads()
	{
		var svc = new LogService { MinimumLevel = OurLevel.Trace };
		var echoed = new ManualResetEventSlim(false);

		var factory = LoggerFactory.Create(b =>
		{
			b.SetMinimumLevel(MelLevel.Trace);
			b.AddProvider(new EchoOnPoolThreadProvider(svc, echoed));
		});

		using var forwarder = new LogServiceForwarder(svc, factory);
		svc.LogWarning("TRIGGER", "Focus");

		Assert.True(echoed.Wait(TimeSpan.FromSeconds(10)), "the echo never ran");
		await Task.Delay(100);   // let the echo's own write land

		Assert.Contains(svc.GetAllLogs(), e => e.Message.Contains("ECHO"));
	}

	/// <summary>A sink whose write hands work to the thread pool, which then logs again.</summary>
	private sealed class EchoOnPoolThreadProvider : ILoggerProvider
	{
		private readonly ILogService _svc;
		private readonly ManualResetEventSlim _done;

		public EchoOnPoolThreadProvider(ILogService svc, ManualResetEventSlim done)
		{
			_svc = svc;
			_done = done;
		}

		public ILogger CreateLogger(string categoryName) => new L(_svc, _done);
		public void Dispose() { }

		private sealed class L : ILogger
		{
			private readonly ILogService _svc;
			private readonly ManualResetEventSlim _done;

			public L(ILogService svc, ManualResetEventSlim done)
			{
				_svc = svc;
				_done = done;
			}

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => new N();
			public bool IsEnabled(MelLevel logLevel) => true;

			public void Log<TState>(
				MelLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter)
			{
				var message = formatter(state, exception);
				if (message.Contains("ECHO")) return;   // the echo itself must not recurse

				Task.Run(() =>
				{
					_svc.LogInfo($"ECHO of {message}", "SinkThread");
					_done.Set();
				});
			}

			private sealed class N : IDisposable
			{
				public void Dispose() { }
			}
		}
	}

	[Fact]
	public async Task Forwarder_ConcurrentLogging_LosesNothingAndDoesNotDeadlock()
	{
		var sink = new List<Written>();
		var sinkLock = new object();
		var svc = new LogService { MinimumLevel = OurLevel.Trace, MaxBufferSize = 10_000 };

		using var forwarder = new LogServiceForwarder(svc, FactoryRecordingInto(sink, sinkLock));

		var tasks = Enumerable.Range(0, 8).Select(t => Task.Run(() =>
		{
			for (int i = 0; i < 50; i++) svc.LogWarning($"T{t}-{i}", "Window");
		})).ToArray();

		var all = Task.WhenAll(tasks);
		Assert.True(await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(10))) == all,
			"concurrent logging deadlocked");
		await all;

		lock (sinkLock) Assert.Equal(400, sink.Count);
	}

	#endregion

	#region The real thing: through the viewer, on screen

	/// <summary>
	/// The end-to-end path the feature exists for: an application's ILogger call must appear as a
	/// rendered row in a LogViewerControl inside a real window, and survive a re-render. Asserts
	/// on the rendered lines, not on the buffer, because feeding the buffer was never the part at
	/// risk: the first version of this bridge filled the buffer correctly while the pane stayed
	/// empty on screen.
	/// </summary>
	[Fact]
	public void RealThing_IloggerEntry_RendersInLogViewer_AndSurvivesReRender()
	{
		var driver = new HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);

		// Boundary-stressing size, mirroring the real Log Stream window: the category column is
		// genuinely too narrow for a full type name here.
		var window = new Window(system) { Title = "Logs", Width = 100, Height = 14 };
		system.AddWindow(window);

		system.LogService.MinimumLevel = OurLevel.Information;

		var provider = new SharpConsoleUILoggerProvider(system.LogService);
		ILogger logger = provider.CreateLogger("MyApp.Services.OrderProcessor");

		var viewer = new LogViewerControl(system.LogService)
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			FilterLevel = OurLevel.Trace,
			AutoScroll = true
		};
		window.AddControl(viewer);

		var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };
		window.RenderAndGetVisibleContent(region);   // attach + first frame

		// Log AFTER attach, through the application-facing ILogger, then pump the marshalled work.
		logger.LogInformation("ORDERPLACED-12345");
		logger.LogWarning("STOCKLOW-widget");
		system.DrainUiThreadQueueForTests();

		var lines1 = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines1, l => l.Contains("STOCKLOW-widget"));

		// The TAIL of the dotted category survives the narrow column, not the namespace head:
		// "OrderProcessor" is the part a reader needs.
		Assert.Contains(lines1, l => l.Contains("OrderProcessor"));

		// Survives a re-render.
		var lines2 = window.RenderAndGetVisibleContent(region);
		Assert.Contains(lines2, l => l.Contains("STOCKLOW-widget"));
		Assert.Contains(lines2, l => l.Contains("OrderProcessor"));
	}

	#endregion
}
