// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;

namespace SharpConsoleUI.ExceptionHandling;

/// <summary>
/// Base exception handler with common functionality
/// </summary>
public abstract class ExceptionHandlerBase : IExceptionHandler
{
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlerBase"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    protected ExceptionHandlerBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <inheritdoc />
    public abstract bool CanHandle(ExceptionContext context);

    /// <inheritdoc />
    public abstract Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for argument exceptions - these usually indicate programming errors
/// </summary>
public sealed class ArgumentExceptionHandler : ExceptionHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArgumentExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public ArgumentExceptionHandler(ILogger<ArgumentExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 100;

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return context.Exception is ArgumentException or ArgumentNullException or ArgumentOutOfRangeException;
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogError(context.Exception,
            "Argument exception in {Source} during {Operation}: {Message}",
            context.Source, context.Operation, context.Exception.Message);

        // Argument exceptions usually indicate bugs - propagate them
        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.LogAndPropagate,
            true));
    }
}

/// <summary>
/// Handler for I/O exceptions with retry logic
/// </summary>
public sealed class IOExceptionHandler : ExceptionHandlerBase
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Initializes a new instance of the <see cref="IOExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public IOExceptionHandler(ILogger<IOExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 200;

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return context.Exception is IOException or DirectoryNotFoundException or FileNotFoundException;
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        var retryCount = GetRetryCount(context);

        Logger.LogWarning(context.Exception,
            "I/O exception in {Source} during {Operation}: {Message} (Retry {Retry}/{MaxRetries})",
            context.Source, context.Operation, context.Exception.Message, retryCount, MaxRetries);

        if (retryCount < MaxRetries)
        {
            return Task.FromResult(new ExceptionHandlingResult(
                ExceptionHandlingStrategy.LogAndRetry,
                true,
                ShouldRetry: true,
                RetryDelay: RetryDelay));
        }

        Logger.LogError("Max retries exceeded for I/O operation in {Source}", context.Source);
        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.LogAndPropagate,
            true));
    }

    private static int GetRetryCount(ExceptionContext context)
    {
        if (context.AdditionalData?.TryGetValue("RetryCount", out var value) == true && value is int count)
        {
            return count;
        }
        return 0;
    }
}

/// <summary>
/// Handler for object disposed exceptions
/// </summary>
public sealed class ObjectDisposedExceptionHandler : ExceptionHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectDisposedExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public ObjectDisposedExceptionHandler(ILogger<ObjectDisposedExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 150;

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return context.Exception is ObjectDisposedException;
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning(context.Exception,
            "Object disposed exception in {Source} during {Operation}: {ObjectName}",
            context.Source, context.Operation,
            (context.Exception as ObjectDisposedException)?.ObjectName ?? "Unknown");

        // Object disposed exceptions should be handled gracefully
        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.Graceful,
            true));
    }
}

/// <summary>
/// Handler for cancellation exceptions
/// </summary>
public sealed class OperationCanceledExceptionHandler : ExceptionHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationCanceledExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public OperationCanceledExceptionHandler(ILogger<OperationCanceledExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 50;

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return context.Exception is OperationCanceledException;
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("Operation canceled in {Source} during {Operation}",
            context.Source, context.Operation);

        // Cancellation is expected behavior
        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.Graceful,
            true));
    }
}

/// <summary>
/// Handler for invalid operation exceptions
/// </summary>
public sealed class InvalidOperationExceptionHandler : ExceptionHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidOperationExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public InvalidOperationExceptionHandler(ILogger<InvalidOperationExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => 300;

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return context.Exception is InvalidOperationException;
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogError(context.Exception,
            "Invalid operation in {Source} during {Operation}: {Message}",
            context.Source, context.Operation, context.Exception.Message);

        // Try to handle gracefully but log as error
        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.LogAndContinue,
            true));
    }
}

/// <summary>
/// Fallback handler for any unhandled exceptions
/// </summary>
public sealed class FallbackExceptionHandler : ExceptionHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FallbackExceptionHandler"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public FallbackExceptionHandler(ILogger<FallbackExceptionHandler> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    public override int Order => int.MaxValue; // Always last

    /// <inheritdoc />
    public override bool CanHandle(ExceptionContext context)
    {
        return true; // Handles everything
    }

    /// <inheritdoc />
    public override Task<ExceptionHandlingResult> HandleAsync(ExceptionContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogCritical(context.Exception,
            "Unhandled exception in {Source} during {Operation}: {ExceptionType} - {Message}",
            context.Source, context.Operation,
            context.Exception.GetType().Name, context.Exception.Message);

        // For critical unhandled exceptions, we might want to terminate or propagate
        if (IsCriticalException(context.Exception))
        {
            Logger.LogCritical("Critical exception detected, terminating application");
            return Task.FromResult(new ExceptionHandlingResult(
                ExceptionHandlingStrategy.Terminate,
                true));
        }

        return Task.FromResult(new ExceptionHandlingResult(
            ExceptionHandlingStrategy.LogAndPropagate,
            true));
    }

    private static bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException or
               StackOverflowException or
               AccessViolationException or
               AppDomainUnloadedException;
    }
}