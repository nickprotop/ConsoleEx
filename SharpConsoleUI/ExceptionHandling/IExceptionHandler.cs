// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.ExceptionHandling;

/// <summary>
/// Exception handling strategy enumeration
/// </summary>
public enum ExceptionHandlingStrategy
{
    /// <summary>
    /// Log the exception and continue
    /// </summary>
    LogAndContinue,

    /// <summary>
    /// Log the exception and retry the operation
    /// </summary>
    LogAndRetry,

    /// <summary>
    /// Log the exception and propagate it
    /// </summary>
    LogAndPropagate,

    /// <summary>
    /// Handle the exception gracefully with fallback behavior
    /// </summary>
    Graceful,

    /// <summary>
    /// Terminate the application
    /// </summary>
    Terminate
}

/// <summary>
/// Exception context information
/// </summary>
/// <param name="Exception">The exception that occurred</param>
/// <param name="Source">The source component</param>
/// <param name="Operation">The operation being performed</param>
/// <param name="AdditionalData">Additional context data</param>
public sealed record ExceptionContext(
    Exception Exception,
    string? Source = null,
    string? Operation = null,
    Dictionary<string, object>? AdditionalData = null
);

/// <summary>
/// Exception handling result
/// </summary>
/// <param name="Strategy">The strategy that was applied</param>
/// <param name="Handled">Whether the exception was handled</param>
/// <param name="ShouldRetry">Whether the operation should be retried</param>
/// <param name="RetryDelay">Delay before retry</param>
/// <param name="FallbackValue">Fallback value if applicable</param>
public sealed record ExceptionHandlingResult(
    ExceptionHandlingStrategy Strategy,
    bool Handled,
    bool ShouldRetry = false,
    TimeSpan? RetryDelay = null,
    object? FallbackValue = null
);

/// <summary>
/// Interface for exception handlers
/// </summary>
public interface IExceptionHandler
{
    /// <summary>
    /// Gets the order priority for this handler (lower values execute first)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Determines if this handler can handle the specified exception
    /// </summary>
    /// <param name="context">The exception context</param>
    /// <returns>True if this handler can handle the exception</returns>
    bool CanHandle(ExceptionContext context);

    /// <summary>
    /// Handles the exception
    /// </summary>
    /// <param name="context">The exception context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The handling result</returns>
    Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for the centralized exception manager
/// </summary>
public interface IExceptionManager : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Registers an exception handler
    /// </summary>
    /// <param name="handler">The exception handler</param>
    void RegisterHandler(IExceptionHandler handler);

    /// <summary>
    /// Unregisters an exception handler
    /// </summary>
    /// <param name="handler">The exception handler</param>
    void UnregisterHandler(IExceptionHandler handler);

    /// <summary>
    /// Handles an exception using the registered handlers
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="source">The source component</param>
    /// <param name="operation">The operation being performed</param>
    /// <param name="additionalData">Additional context data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The handling result</returns>
    Task<ExceptionHandlingResult> HandleExceptionAsync(
        Exception exception,
        string? source = null,
        string? operation = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with exception handling
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="source">The source component</param>
    /// <param name="operationName">The operation name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the operation</returns>
    Task ExecuteWithHandlingAsync(
        Func<Task> operation,
        string? source = null,
        string? operationName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with exception handling and returns a result
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="fallbackValue">Fallback value if operation fails</param>
    /// <param name="source">The source component</param>
    /// <param name="operationName">The operation name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The operation result or fallback value</returns>
    Task<T> ExecuteWithHandlingAsync<T>(
        Func<Task<T>> operation,
        T fallbackValue = default!,
        string? source = null,
        string? operationName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when an exception is handled
    /// </summary>
    event EventHandler<ExceptionHandledEventArgs>? ExceptionHandled;
}

/// <summary>
/// Event arguments for exception handled events
/// </summary>
public sealed class ExceptionHandledEventArgs : EventArgs
{
    /// <summary>
    /// Gets the exception context
    /// </summary>
    public ExceptionContext Context { get; }

    /// <summary>
    /// Gets the handling result
    /// </summary>
    public ExceptionHandlingResult Result { get; }

    /// <summary>
    /// Gets the handler that processed the exception
    /// </summary>
    public IExceptionHandler Handler { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandledEventArgs"/> class
    /// </summary>
    /// <param name="context">The exception context</param>
    /// <param name="result">The handling result</param>
    /// <param name="handler">The handler that processed the exception</param>
    public ExceptionHandledEventArgs(ExceptionContext context, ExceptionHandlingResult result, IExceptionHandler handler)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Result = result ?? throw new ArgumentNullException(nameof(result));
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }
}