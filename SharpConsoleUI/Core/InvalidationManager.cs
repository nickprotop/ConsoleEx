using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
    /// <summary>
    /// Reasons why a control or container needs invalidation
    /// </summary>
    [Flags]
    public enum InvalidationReason
    {
        None = 0,
        PropertyChanged = 1,
        StateChanged = 2,
        FocusChanged = 4,
        SizeChanged = 8,
        ContentChanged = 16,
        ThemeChanged = 32,
        ChildInvalidated = 64,
        All = PropertyChanged | StateChanged | FocusChanged | SizeChanged | ContentChanged | ThemeChanged | ChildInvalidated
    }

    /// <summary>
    /// Represents an invalidation request
    /// </summary>
    public class InvalidationRequest
    {
        public IWIndowControl Control { get; }
        public InvalidationReason Reason { get; }
        public bool PropagateToParent { get; }
        public DateTime RequestTime { get; }

        public InvalidationRequest(IWIndowControl control, InvalidationReason reason, bool propagateToParent = true)
        {
            Control = control;
            Reason = reason;
            PropagateToParent = propagateToParent;
            RequestTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Thread-safe cache state manager for controls
    /// </summary>
    public class CacheState
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private volatile bool _isValid = false;
        private volatile bool _isRendering = false;
        private DateTime _lastInvalidated = DateTime.MinValue;
        private InvalidationReason _invalidationReason = InvalidationReason.None;

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

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }

    /// <summary>
    /// Thread-safe invalidation manager that coordinates all invalidation requests
    /// </summary>
    public class InvalidationManager
    {
        private static readonly Lazy<InvalidationManager> _instance = new(() => new InvalidationManager());
        public static InvalidationManager Instance => _instance.Value;

        private readonly ConcurrentQueue<InvalidationRequest> _invalidationQueue = new();
        private readonly ConcurrentDictionary<IWIndowControl, CacheState> _cacheStates = new();
        private readonly Timer _batchTimer;
        private readonly object _processingLock = new();
        private volatile bool _isProcessing = false;

        private const int BatchDelayMs = 5; // Small delay to batch rapid invalidations

        private InvalidationManager()
        {
            _batchTimer = new Timer(ProcessBatch, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Registers a control with the invalidation manager
        /// </summary>
        public void RegisterControl(IWIndowControl control)
        {
            _cacheStates.TryAdd(control, new CacheState());
        }

        /// <summary>
        /// Unregisters a control from the invalidation manager
        /// </summary>
        public void UnregisterControl(IWIndowControl control)
        {
            if (_cacheStates.TryRemove(control, out var state))
            {
                state.Dispose();
            }
        }

        /// <summary>
        /// Requests invalidation for a control
        /// </summary>
        public void RequestInvalidation(IWIndowControl control, InvalidationReason reason, bool propagateToParent = true)
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
        /// Gets the cache state for a control
        /// </summary>
        public CacheState GetCacheState(IWIndowControl control)
        {
            return _cacheStates.GetOrAdd(control, _ => new CacheState());
        }

        /// <summary>
        /// Safely executes a function with cache protection
        /// </summary>
        public T WithCacheProtection<T>(IWIndowControl control, Func<T> renderFunction, T fallbackValue = default)
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
        /// Checks if a control needs rendering
        /// </summary>
        public bool NeedsRendering(IWIndowControl control)
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
                var controlRequests = new Dictionary<IWIndowControl, InvalidationReason>();
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

                    // Track containers that need notification
                    if (request.PropagateToParent && request.Control is IWIndowControl control)
                    {
                        // Find parent containers
                        var container = GetParentContainer(control);
                        if (container != null)
                        {
                            containerRequests.Add(container);
                        }
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
                        // Log error but continue processing
                        Console.WriteLine($"Error invalidating container: {ex.Message}");
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

        private IContainer GetParentContainer(IWIndowControl control)
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