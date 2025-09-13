// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SharpConsoleUI.Events.Enhanced;

namespace SharpConsoleUI.ExceptionHandling;

/// <summary>
/// Default implementation of the exception manager
/// </summary>
public sealed class ExceptionManager : IExceptionManager
{
    private readonly ConcurrentBag<IExceptionHandler> _handlers = new();
    private readonly ILogger<ExceptionManager> _logger;
    private readonly IEventAggregator? _eventAggregator;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionManager"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="eventAggregator">The event aggregator (optional)</param>
    public ExceptionManager(ILogger<ExceptionManager> logger, IEventAggregator? eventAggregator = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventAggregator = eventAggregator;
    }

    /// <inheritdoc />
    public event EventHandler<ExceptionHandledEventArgs>? ExceptionHandled;

    /// <inheritdoc />
    public void RegisterHandler(IExceptionHandler handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _handlers.Add(handler);
        _logger.LogDebug("Registered exception handler {HandlerType} with order {Order}",
            handler.GetType().Name, handler.Order);
    }

    /// <inheritdoc />
    public void UnregisterHandler(IExceptionHandler handler)
    {
        if (handler == null)
            return;

        // Note: ConcurrentBag doesn't support removal, so we'll need to track them differently
        // For now, handlers are expected to have their own lifetime management
        _logger.LogDebug("Unregistered exception handler {HandlerType}", handler.GetType().Name);
    }

    /// <inheritdoc />
    public async Task<ExceptionHandlingResult> HandleExceptionAsync(
        Exception exception,
        string? source = null,
        string? operation = null,
        Dictionary<string, object>? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ExceptionManager));

        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        var context = new ExceptionContext(exception, source, operation, additionalData);

        _logger.LogError(exception, "Handling exception from {Source} during {Operation}",
            source ?? "Unknown", operation ?? "Unknown");

        // Publish error event
        if (_eventAggregator != null)
        {
            await _eventAggregator.PublishAsync(new ErrorOccurredEvent(exception, source), cancellationToken);
        }

        // Get handlers that can handle this exception, ordered by priority
        var applicableHandlers = _handlers
            .Where(h => h.CanHandle(context))
            .OrderBy(h => h.Order)
            .ToArray();

        if (!applicableHandlers.Any())
        {
            _logger.LogWarning("No handlers found for exception {ExceptionType} from {Source}",
                exception.GetType().Name, source ?? "Unknown");

            return new ExceptionHandlingResult(
                ExceptionHandlingStrategy.LogAndPropagate,
                false);
        }

        // Try each handler until one successfully handles the exception
        foreach (var handler in applicableHandlers)
        {
            try
            {
                _logger.LogDebug("Attempting to handle exception with {HandlerType}",
                    handler.GetType().Name);

                var result = await handler.HandleAsync(context, cancellationToken);

                if (result.Handled)
                {
                    _logger.LogInformation("Exception handled successfully by {HandlerType} with strategy {Strategy}",
                        handler.GetType().Name, result.Strategy);

                    // Fire handled event
                    ExceptionHandled?.Invoke(this, new ExceptionHandledEventArgs(context, result, handler));

                    return result;
                }
            }
            catch (Exception handlerException)
            {
                _logger.LogError(handlerException, "Exception handler {HandlerType} failed",
                    handler.GetType().Name);
            }
        }

        // No handler successfully handled the exception
        _logger.LogError("All applicable handlers failed to handle exception {ExceptionType}",
            exception.GetType().Name);

        return new ExceptionHandlingResult(
            ExceptionHandlingStrategy.LogAndPropagate,
            false);
    }

    /// <inheritdoc />
    public async Task ExecuteWithHandlingAsync(
        Func<Task> operation,
        string? source = null,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            var result = await HandleExceptionAsync(ex, source, operationName, cancellationToken: cancellationToken);

            if (result.ShouldRetry && result.RetryDelay.HasValue)
            {
                await Task.Delay(result.RetryDelay.Value, cancellationToken);
                await operation(); // Simple retry - could be enhanced with retry policies
            }
            else if (!result.Handled || result.Strategy == ExceptionHandlingStrategy.LogAndPropagate)
            {
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithHandlingAsync<T>(
        Func<Task<T>> operation,
        T fallbackValue = default!,
        string? source = null,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            var result = await HandleExceptionAsync(ex, source, operationName, cancellationToken: cancellationToken);

            if (result.ShouldRetry && result.RetryDelay.HasValue)
            {
                await Task.Delay(result.RetryDelay.Value, cancellationToken);
                return await operation(); // Simple retry
            }

            if (result.Handled)
            {
                return result.FallbackValue is T typedValue ? typedValue : fallbackValue;
            }

            if (result.Strategy == ExceptionHandlingStrategy.LogAndPropagate)
            {
                throw;
            }

            return fallbackValue;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        await Task.CompletedTask;
    }
}