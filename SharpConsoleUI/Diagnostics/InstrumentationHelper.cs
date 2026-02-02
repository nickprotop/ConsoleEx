// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics
{
	/// <summary>
	/// Helper methods for performance instrumentation and debugging.
	/// </summary>
	public static class InstrumentationHelper
	{
		/// <summary>
		/// Logs a buffer operation (clear, allocation, etc.).
		/// </summary>
		public static void LogBufferOperation(string operation, string details)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.BUFFER, operation, details);
		}

		/// <summary>
		/// Logs ANSI sequence generation details.
		/// </summary>
		public static void LogAnsiGeneration(string context, int lineCount, int byteCount)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.ANSI, context,
				$"Generated {byteCount} bytes of ANSI for {lineCount} lines");
		}

		/// <summary>
		/// Logs a frame timing metric.
		/// </summary>
		public static void LogFrameTiming(string operation, double durationMs)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.FRAME, operation,
				$"Duration: {durationMs:F2}ms");
		}

		/// <summary>
		/// Logs DOM tree operation details.
		/// </summary>
		public static void LogDOMOperation(string operation, string windowId, int nodeCount)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.DOM, operation,
				$"Window={windowId}, Nodes={nodeCount}");
		}

		/// <summary>
		/// Logs paint operation details.
		/// </summary>
		public static void LogPaintOperation(string controlType, string operation, object state)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.PAINT, $"{controlType}.{operation}",
				state?.ToString() ?? "null");
		}

		/// <summary>
		/// Logs invalidation request details.
		/// </summary>
		public static void LogInvalidation(string source, string windowId, bool batched)
		{
			FlickerDebugLogger.Instance.Log(DebugLevel.INVALIDATE, source,
				$"Window={windowId}, Batched={batched}");
		}
	}
}
