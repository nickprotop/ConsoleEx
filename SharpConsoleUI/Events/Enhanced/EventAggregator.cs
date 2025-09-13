// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SharpConsoleUI.Events.Enhanced;

/// <summary>
/// Default implementation of the event aggregator
/// </summary>
public sealed class EventAggregator : IEventAggregator
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<IEventSubscription>> _subscriptions = new();
    private readonly ILogger<EventAggregator> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventAggregator"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public EventAggregator(ILogger<EventAggregator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int SubscriptionCount => _subscriptions.Values.Sum(bag => bag.Count);

    /// <inheritdoc />
    public async Task PublishAsync<T>(T eventObj, CancellationToken cancellationToken = default) where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventAggregator));

        if (eventObj == null)
            return;

        var eventType = typeof(T);
        _logger.LogDebug("Publishing event {EventType}", eventType.Name);

        if (!_subscriptions.TryGetValue(eventType, out var subscriptions) || !subscriptions.Any())
        {
            _logger.LogDebug("No subscribers found for event {EventType}", eventType.Name);
            return;
        }

        // Get active subscriptions and sort by priority (highest first)
        var activeSubscriptions = subscriptions
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.Priority)
            .ToArray();

        if (!activeSubscriptions.Any())
        {
            _logger.LogDebug("No active subscribers found for event {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Executing {Count} handlers for event {EventType}", activeSubscriptions.Length, eventType.Name);

        // Execute all handlers in parallel, respecting cancellation
        var tasks = activeSubscriptions.Select(async subscription =>
        {
            try
            {
                await subscription.ExecuteAsync(eventObj, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Handler execution cancelled for event {EventType}, subscription {SubscriptionId}",
                    eventType.Name, subscription.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing handler for event {EventType}, subscription {SubscriptionId}",
                    eventType.Name, subscription.Id);
            }
        });

        await Task.WhenAll(tasks);
        _logger.LogDebug("Completed publishing event {EventType}", eventType.Name);
    }

    /// <inheritdoc />
    public void Publish<T>(T eventObj) where T : class
    {
        // Run async method synchronously - not ideal but needed for backwards compatibility
        PublishAsync(eventObj).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IEventSubscription Subscribe<T>(Func<T, CancellationToken, Task> handler, int priority = 0) where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EventAggregator));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var subscription = new EventSubscription<T>(handler, priority);
        var eventType = typeof(T);

        _subscriptions.AddOrUpdate(eventType,
            _ => new ConcurrentBag<IEventSubscription> { subscription },
            (_, existing) =>
            {
                existing.Add(subscription);
                return existing;
            });

        _logger.LogDebug("Added subscription {SubscriptionId} for event {EventType} with priority {Priority}",
            subscription.Id, eventType.Name, priority);

        return subscription;
    }

    /// <inheritdoc />
    public IEventSubscription Subscribe<T>(Action<T> handler, int priority = 0) where T : class
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        return Subscribe<T>((evt, _) =>
        {
            handler(evt);
            return Task.CompletedTask;
        }, priority);
    }

    /// <inheritdoc />
    public IEventSubscription Subscribe<T>(Func<T, CancellationToken, Task> handler, Func<T, bool> predicate, int priority = 0) where T : class
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        return Subscribe<T>(async (evt, cancellationToken) =>
        {
            if (predicate(evt))
            {
                await handler(evt, cancellationToken);
            }
        }, priority);
    }

    /// <inheritdoc />
    public void Unsubscribe(IEventSubscription subscription)
    {
        if (subscription == null)
            return;

        subscription.Dispose();

        _logger.LogDebug("Unsubscribed subscription {SubscriptionId} for event {EventType}",
            subscription.Id, subscription.EventType.Name);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _logger.LogInformation("Clearing all event subscriptions");

        foreach (var subscriptionBag in _subscriptions.Values)
        {
            foreach (var subscription in subscriptionBag)
            {
                subscription.Dispose();
            }
        }

        _subscriptions.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        Clear();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        Clear();
        _disposed = true;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Event subscription implementation
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    private sealed class EventSubscription<T> : IEventSubscription where T : class
    {
        private readonly Func<T, CancellationToken, Task> _handler;
        private bool _disposed;

        public EventSubscription(Func<T, CancellationToken, Task> handler, int priority)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Id = Guid.NewGuid();
            EventType = typeof(T);
            Priority = priority;
        }

        public Guid Id { get; }
        public Type EventType { get; }
        public int Priority { get; }
        public bool IsActive => !_disposed;

        public async Task ExecuteAsync(object eventObj, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            if (eventObj is T typedEvent)
            {
                await _handler(typedEvent, cancellationToken);
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}