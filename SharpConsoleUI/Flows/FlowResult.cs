// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Represents the outcome of a completed flow run, carrying either a typed value,
	/// a cancellation signal, or a fault with the originating exception.
	/// </summary>
	/// <typeparam name="T">The type of value produced by a successfully completed flow.</typeparam>
	public readonly struct FlowResult<T>
	{
		private FlowResult(bool completed, T? value, bool cancelled, bool faulted, Exception? error)
		{
			Completed = completed;
			Value = value;
			Cancelled = cancelled;
			Faulted = faulted;
			Error = error;
		}

		/// <summary>
		/// <c>true</c> when the flow completed successfully and <see cref="Value"/> is valid.
		/// </summary>
		public bool Completed { get; }

		/// <summary>
		/// The value produced by the flow. Only meaningful when <see cref="Completed"/> is <c>true</c>.
		/// </summary>
		public T? Value { get; }

		/// <summary>
		/// <c>true</c> when the flow was cancelled by the user or the host.
		/// </summary>
		public bool Cancelled { get; }

		/// <summary>
		/// <c>true</c> when the flow terminated due to an unhandled exception.
		/// </summary>
		public bool Faulted { get; }

		/// <summary>
		/// The exception that caused the fault. Only meaningful when <see cref="Faulted"/> is <c>true</c>.
		/// </summary>
		public Exception? Error { get; }

		/// <summary>
		/// Creates a successful completion result carrying <paramref name="value"/>.
		/// </summary>
		/// <param name="value">The value produced by the flow.</param>
		/// <returns>A <see cref="FlowResult{T}"/> with <see cref="Completed"/> set to <c>true</c>.</returns>
		public static FlowResult<T> Complete(T value) => new(true, value, false, false, null);

		/// <summary>
		/// Creates a cancellation result indicating the flow was abandoned.
		/// </summary>
		/// <returns>A <see cref="FlowResult{T}"/> with <see cref="Cancelled"/> set to <c>true</c>.</returns>
		public static FlowResult<T> Cancel() => new(false, default, true, false, null);

		/// <summary>
		/// Creates a fault result wrapping the given exception.
		/// </summary>
		/// <param name="error">The exception that caused the flow to fault.</param>
		/// <returns>A <see cref="FlowResult{T}"/> with <see cref="Faulted"/> set to <c>true</c>.</returns>
		public static FlowResult<T> Fault(Exception error) => new(false, default, false, true, error);
	}
}
