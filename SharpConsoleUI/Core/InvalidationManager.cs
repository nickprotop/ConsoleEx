using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
    /// <summary>
    /// Specifies the reasons why a control or container needs invalidation.
    /// This is a flags enumeration allowing multiple reasons to be combined.
    /// </summary>
    [Flags]
    public enum InvalidationReason
    {
        /// <summary>No invalidation reason specified.</summary>
        None = 0,
        /// <summary>A property value has changed.</summary>
        PropertyChanged = 1,
        /// <summary>The control's internal state has changed.</summary>
        StateChanged = 2,
        /// <summary>Focus has changed to or from the control.</summary>
        FocusChanged = 4,
        /// <summary>The control's size has changed.</summary>
        SizeChanged = 8,
        /// <summary>The control's content has changed.</summary>
        ContentChanged = 16,
        /// <summary>The theme has changed.</summary>
        ThemeChanged = 32,
        /// <summary>A child control was invalidated.</summary>
        ChildInvalidated = 64,
        /// <summary>All invalidation reasons combined.</summary>
        All = PropertyChanged | StateChanged | FocusChanged | SizeChanged | ContentChanged | ThemeChanged | ChildInvalidated
    }

    /// <summary>
    /// Represents an invalidation request for a control.
    /// </summary>
    public class InvalidationRequest
    {
        /// <summary>
        /// Gets the control that needs invalidation.
        /// </summary>
        public IWindowControl Control { get; }

        /// <summary>
        /// Gets the reason for the invalidation.
        /// </summary>
        public InvalidationReason Reason { get; }

        /// <summary>
        /// Gets whether this invalidation should propagate to the parent container.
        /// </summary>
        public bool PropagateToParent { get; }

        /// <summary>
        /// Gets the UTC time when the invalidation was requested.
        /// </summary>
        public DateTime RequestTime { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidationRequest"/> class.
        /// </summary>
        /// <param name="control">The control that needs invalidation.</param>
        /// <param name="reason">The reason for the invalidation.</param>
        /// <param name="propagateToParent">Whether to propagate the invalidation to the parent container. Defaults to <c>true</c>.</param>
        public InvalidationRequest(IWindowControl control, InvalidationReason reason, bool propagateToParent = true)
        {
            Control = control;
            Reason = reason;
            PropagateToParent = propagateToParent;
            RequestTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Thread-safe cache state manager for controls.
    /// Tracks validation state, rendering status, and invalidation reasons.
    /// </summary>
    public class CacheState
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private volatile bool _isValid = false;
        private volatile bool _isRendering = false;
        private DateTime _lastInvalidated = DateTime.MinValue;
        private InvalidationReason _invalidationReason = InvalidationReason.None;

        /// <summary>
        /// Gets whether the cache is currently valid.
        /// </summary>
        public bool IsValid 
        { 
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _isValid;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets whether a render operation is currently in progress.
        /// </summary>
        public bool IsRendering
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _isRendering;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Marks the cache as invalid with the specified reason.
        /// </summary>
        /// <param name="reason">The reason for invalidation.</param>
        public void Invalidate(InvalidationReason reason)
        {
            _lock.EnterWriteLock();
            try
            {
                _isValid = false;
                _invalidationReason |= reason;
                _lastInvalidated = DateTime.UtcNow;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Marks the cache as currently rendering to prevent concurrent render operations.
        /// </summary>
        public void MarkAsRendering()
        {
            _lock.EnterWriteLock();
            try
            {
                _isRendering = true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Marks the cache as valid and clears the rendering flag and invalidation reason.
        /// </summary>
        public void MarkAsValid()
        {
            _lock.EnterWriteLock();
            try
            {
                _isValid = true;
                _isRendering = false;
                _invalidationReason = InvalidationReason.None;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to begin a render operation. Returns <c>false</c> if another render is in progress.
        /// </summary>
        /// <returns><c>true</c> if rendering was started; <c>false</c> if a render is already in progress.</returns>
        public bool TryBeginRendering()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isRendering)
                    return false;

                _isRendering = true;
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the current invalidation reason flags.
        /// </summary>
        /// <returns>The combined invalidation reasons.</returns>
        public InvalidationReason GetInvalidationReason()
        {
            _lock.EnterReadLock();
            try
            {
                return _invalidationReason;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Disposes the internal reader-writer lock.
        /// </summary>
        public void Dispose()
        {
            _lock?.Dispose();
        }
    }

    /// <summary>
    /// Thread-safe invalidation manager that coordinates all invalidation requests.
    /// Implements singleton pattern and batches rapid invalidation requests for efficiency.
    /// </summary>
    public class InvalidationManager
    {
        private static readonly Lazy<InvalidationManager> _instance = new(() => new InvalidationManager());

        /// <summary>
        /// Gets the singleton instance of the <see cref="InvalidationManager"/>.
        /// </summary>
        public static InvalidationManager Instance => _instance.Value;

        private readonly ConcurrentQueue<InvalidationRequest> _invalidationQueue = new();
        private readonly ConcurrentDictionary<IWindowControl, CacheState> _cacheStates = new();
        private readonly ConcurrentDictionary<IWindowControl, IContainer> _controlHierarchy = new();
        private readonly ConcurrentDictionary<IContainer, HashSet<IWindowControl>> _containerChildren = new();
        private readonly Timer _batchTimer;
        private readonly object _processingLock = new();
        private volatile bool _isProcessing = false;

        private const int BatchDelayMs = 5; // Small delay to batch rapid invalidations

        /// <summary>
        /// Optional log service for error logging. Can be set by ConsoleWindowSystem or tests.
        /// </summary>
        public ILogService? LogService { get; set; }

        private InvalidationManager()
        {
            _batchTimer = new Timer(ProcessBatch, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Registers a control with the invalidation manager
        /// </summary>
        public void RegisterControl(IWindowControl control)
        {
            _cacheStates.TryAdd(control, new CacheState());
        }

        /// <summary>
        /// Unregisters a control from the invalidation manager
        /// </summary>
        public void UnregisterControl(IWindowControl control)
        {
            if (_cacheStates.TryRemove(control, out var state))
            {
                state.Dispose();
            }

            // Clean up hierarchy tracking
            if (_controlHierarchy.TryRemove(control, out var parent))
            {
                if (_containerChildren.TryGetValue(parent, out var children))
                {
                    children.Remove(control);
                    if (children.Count == 0)
                    {
                        _containerChildren.TryRemove(parent, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Registers the parent-child relationship for hierarchy tracking.
        /// Used to propagate invalidation to parent containers.
        /// </summary>
        /// <param name="child">The child control.</param>
        /// <param name="parent">The parent container.</param>
        public void RegisterControlHierarchy(IWindowControl child, IContainer parent)
        {
            if (child == null || parent == null) return;

            _controlHierarchy.AddOrUpdate(child, parent, (k, v) => parent);
            _containerChildren.AddOrUpdate(parent,
                new HashSet<IWindowControl> { child },
                (k, v) => { v.Add(child); return v; });
        }

        /// <summary>
        /// Unregisters a control from its parent container in the hierarchy tracking.
        /// </summary>
        /// <param name="child">The child control to unregister.</param>
        public void UnregisterControlHierarchy(IWindowControl child)
        {
            if (child == null) return;

            if (_controlHierarchy.TryRemove(child, out var parent))
            {
                if (_containerChildren.TryGetValue(parent, out var children))
                {
                    children.Remove(child);
                    if (children.Count == 0)
                    {
                        _containerChildren.TryRemove(parent, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Requests invalidation for a control. The request is queued and processed in batches.
        /// </summary>
        /// <param name="control">The control to invalidate.</param>
        /// <param name="reason">The reason for invalidation.</param>
        /// <param name="propagateToParent">Whether to propagate the invalidation to the parent container. Defaults to <c>true</c>.</param>
        public void RequestInvalidation(IWindowControl control, InvalidationReason reason, bool propagateToParent = true)
        {
            if (control == null) return;

            // Get or create cache state
            var state = _cacheStates.GetOrAdd(control, _ => new CacheState());

            // Mark as invalid
            state.Invalidate(reason);

            // Queue the request
            _invalidationQueue.Enqueue(new InvalidationRequest(control, reason, propagateToParent));

            // Start batch timer if not already running
            lock (_processingLock)
            {
                if (!_isProcessing)
                {
                    _batchTimer.Change(BatchDelayMs, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Checks if a control is a direct child of the specified container.
        /// </summary>
        /// <param name="control">The control to check.</param>
        /// <param name="container">The container to check against.</param>
        /// <returns><c>true</c> if the control is a direct child of the container; otherwise, <c>false</c>.</returns>
        public bool IsChildOfContainer(IWindowControl control, IContainer container)
        {
            return _controlHierarchy.TryGetValue(control, out var parent) && parent == container;
        }

        /// <summary>
        /// Gets all direct children of a container.
        /// </summary>
        /// <param name="container">The container whose children to retrieve.</param>
        /// <returns>A copy of the children collection to avoid concurrent modification issues.</returns>
        public IEnumerable<IWindowControl> GetContainerChildren(IContainer container)
        {
            if (_containerChildren.TryGetValue(container, out var children))
            {
                return children.ToList(); // Return a copy to avoid concurrent modification
            }
            return Enumerable.Empty<IWindowControl>();
        }

        /// <summary>
        /// Gets or creates the cache state for a control.
        /// </summary>
        /// <param name="control">The control whose cache state to retrieve.</param>
        /// <returns>The cache state for the control.</returns>
        public CacheState GetCacheState(IWindowControl control)
        {
            return _cacheStates.GetOrAdd(control, _ => new CacheState());
        }

        /// <summary>
        /// Safely executes a render function with cache protection.
        /// Prevents concurrent rendering and handles invalidation on failure.
        /// </summary>
        /// <typeparam name="T">The type of the render result.</typeparam>
        /// <param name="control">The control being rendered.</param>
        /// <param name="renderFunction">The function that produces the rendered content.</param>
        /// <param name="fallbackValue">The value to return if another render is in progress. Defaults to <c>default(T)</c>.</param>
        /// <returns>The rendered content, or the fallback value if rendering could not proceed.</returns>
        public T WithCacheProtection<T>(IWindowControl control, Func<T> renderFunction, T fallbackValue = default!)
        {
            var state = GetCacheState(control);
            
            if (!state.TryBeginRendering())
            {
                // Another thread is already rendering, return fallback
                return fallbackValue;
            }

            try
            {
                var result = renderFunction();
                state.MarkAsValid();
                return result;
            }
            catch
            {
                // Mark as invalid if rendering fails
                state.Invalidate(InvalidationReason.All);
                throw;
            }
            finally
            {
                // Always clear rendering flag
                state.MarkAsValid();
            }
        }

        /// <summary>
        /// Checks if a control needs rendering (is invalid and not currently being rendered).
        /// </summary>
        /// <param name="control">The control to check.</param>
        /// <returns><c>true</c> if the control needs rendering; otherwise, <c>false</c>.</returns>
        public bool NeedsRendering(IWindowControl control)
        {
            var state = GetCacheState(control);
            return !state.IsValid && !state.IsRendering;
        }

        private void ProcessBatch(object state)
        {
            lock (_processingLock)
            {
                _isProcessing = true;
            }

            try
            {
                // Group invalidation requests by control to avoid duplicate processing
                var controlRequests = new Dictionary<IWindowControl, InvalidationReason>();
                var containerRequests = new HashSet<IContainer>();

                // Drain the queue
                while (_invalidationQueue.TryDequeue(out var request))
                {
                    // Combine reasons for the same control
                    if (controlRequests.ContainsKey(request.Control))
                    {
                        controlRequests[request.Control] |= request.Reason;
                    }
                    else
                    {
                        controlRequests[request.Control] = request.Reason;
                    }

                    // Track containers that need notification using hierarchy tracking
                    if (request.PropagateToParent && _controlHierarchy.TryGetValue(request.Control, out var parentContainer))
                    {
                        containerRequests.Add(parentContainer);
                    }
                }

                // Process container invalidations (batch these to avoid cascade)
                foreach (var container in containerRequests)
                {
                    try
                    {
                        container.Invalidate(false); // Don't redraw all, just mark as dirty
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other containers
                        // Don't use Console.WriteLine as it would corrupt UI
                        LogService?.Log(LogLevel.Error, $"Failed to invalidate container: {ex.Message}", "Invalidation");
                    }
                }
            }
            finally
            {
                lock (_processingLock)
                {
                    _isProcessing = false;
                }
            }
        }

        private IContainer GetParentContainer(IWindowControl control)
        {
            // Try to find parent container through common patterns
            var controlType = control.GetType();
            
            // Check for Container property
            var containerProperty = controlType.GetProperty("Container");
            if (containerProperty != null)
            {
                return containerProperty.GetValue(control) as IContainer;
            }

            return null;
        }
    }
}