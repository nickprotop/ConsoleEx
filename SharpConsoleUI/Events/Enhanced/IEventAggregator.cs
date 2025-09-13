// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events.Enhanced;

/// <summary>
/// Event aggregator interface for decoupled event handling
/// </summary>
public interface IEventAggregator : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Publishes an event to all subscribers
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="eventObj">The event object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the publish operation</returns>
    Task PublishAsync<T>(T eventObj, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes an event synchronously
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="eventObj">The event object</param>
    void Publish<T>(T eventObj) where T : class;

    /// <summary>
    /// Subscribes to an event type
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="handler">The event handler</param>
    /// <param name="priority">The handler priority (higher values execute first)</param>
    /// <returns>A subscription that can be disposed to unsubscribe</returns>
    IEventSubscription Subscribe<T>(Func<T, CancellationToken, Task> handler, int priority = 0) where T : class;

    /// <summary>
    /// Subscribes to an event type with synchronous handler
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="handler">The event handler</param>
    /// <param name="priority">The handler priority (higher values execute first)</param>
    /// <returns>A subscription that can be disposed to unsubscribe</returns>
    IEventSubscription Subscribe<T>(Action<T> handler, int priority = 0) where T : class;

    /// <summary>
    /// Subscribes to an event type with a predicate filter
    /// </summary>
    /// <typeparam name="T">The event type</typeparam>
    /// <param name="handler">The event handler</param>
    /// <param name="predicate">Filter predicate</param>
    /// <param name="priority">The handler priority (higher values execute first)</param>
    /// <returns>A subscription that can be disposed to unsubscribe</returns>
    IEventSubscription Subscribe<T>(Func<T, CancellationToken, Task> handler, Func<T, bool> predicate, int priority = 0) where T : class;

    /// <summary>
    /// Unsubscribes from an event type
    /// </summary>
    /// <param name="subscription">The subscription to remove</param>
    void Unsubscribe(IEventSubscription subscription);

    /// <summary>
    /// Gets the number of active subscriptions
    /// </summary>
    int SubscriptionCount { get; }

    /// <summary>
    /// Clears all subscriptions
    /// </summary>
    void Clear();
}

/// <summary>
/// Event subscription interface
/// </summary>
public interface IEventSubscription : IDisposable
{
    /// <summary>
    /// Gets the subscription ID
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the event type this subscription handles
    /// </summary>
    Type EventType { get; }

    /// <summary>
    /// Gets the handler priority
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gets whether the subscription is active
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Executes the handler for this subscription
    /// </summary>
    /// <param name="eventObj">The event object</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the execution</returns>
    Task ExecuteAsync(object eventObj, CancellationToken cancellationToken = default);
}