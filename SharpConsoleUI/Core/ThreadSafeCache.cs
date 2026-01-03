using System;
using System.Collections.Generic;
using System.Threading;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
    /// <summary>
    /// Thread-safe wrapper for cached content with atomic operations.
    /// Provides read/write locking and integrates with the <see cref="InvalidationManager"/> for coordinated cache invalidation.
    /// </summary>
    /// <typeparam name="T">The type of content to cache. Must be a reference type.</typeparam>
    public class ThreadSafeCache<T> where T : class
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private T _content;
        private volatile bool _isValid = false;
        private readonly IWindowControl _owner;
        private readonly InvalidationManager _invalidationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadSafeCache{T}"/> class.
        /// </summary>
        /// <param name="owner">The control that owns this cache. Used for invalidation tracking.</param>
        public ThreadSafeCache(IWindowControl owner)
        {
            _owner = owner;
            _invalidationManager = InvalidationManager.Instance;
            _invalidationManager.RegisterControl(owner);
        }

        /// <summary>
        /// Gets the cached content, or null if invalid
        /// </summary>
        public T Content
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _isValid ? _content : null;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets whether the cache is valid
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
        /// Sets the cached content and marks the cache as valid.
        /// </summary>
        /// <param name="content">The content to cache.</param>
        public void SetContent(T content)
        {
            _lock.EnterWriteLock();
            try
            {
                _content = content;
                _isValid = true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Invalidates the cache, clearing the stored content and notifying the <see cref="InvalidationManager"/>.
        /// </summary>
        /// <param name="reason">The reason for invalidation. Defaults to <see cref="InvalidationReason.ContentChanged"/>.</param>
        public void Invalidate(InvalidationReason reason = InvalidationReason.ContentChanged)
        {
            _lock.EnterWriteLock();
            try
            {
                _isValid = false;
                _content = null;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Notify the invalidation manager
            _invalidationManager.RequestInvalidation(_owner, reason);
        }

        /// <summary>
        /// Gets the cached content or renders it if invalid, with thread safety.
        /// Uses the <see cref="InvalidationManager"/> for cache protection and double-check pattern.
        /// </summary>
        /// <param name="renderFunc">A function that produces the content if the cache is invalid.</param>
        /// <returns>The cached content, or newly rendered content if the cache was invalid.</returns>
        public T GetOrRender(Func<T> renderFunc)
        {
            // Quick check if already valid
            if (IsValid)
            {
                return Content;
            }

            // Use invalidation manager for thread-safe rendering
            return _invalidationManager.WithCacheProtection(_owner, () =>
            {
                // Double-check pattern: cache might have been populated by another thread
                if (IsValid)
                {
                    return Content;
                }

                // Render new content
                var newContent = renderFunc();
                SetContent(newContent);
                return newContent;
            }, default(T));
        }

        /// <summary>
        /// Tries to get the cached content without triggering a render operation.
        /// </summary>
        /// <param name="content">When this method returns, contains the cached content if valid; otherwise, null.</param>
        /// <returns><c>true</c> if the cache was valid and content was retrieved; otherwise, <c>false</c>.</returns>
        public bool TryGetContent(out T content)
        {
            _lock.EnterReadLock();
            try
            {
                content = _isValid ? _content : null;
                return _isValid;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears the cache, unregisters from the <see cref="InvalidationManager"/>, and disposes the internal lock.
        /// </summary>
        public void Dispose()
        {
            _lock.EnterWriteLock();
            try
            {
                _content = null;
                _isValid = false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            _invalidationManager.UnregisterControl(_owner);
            _lock?.Dispose();
        }
    }

    /// <summary>
    /// Extension methods for easy cache integration
    /// </summary>
    public static class CacheExtensions
    {
        /// <summary>
        /// Creates a new <see cref="ThreadSafeCache{T}"/> for the specified control.
        /// </summary>
        /// <typeparam name="T">The type of content to cache. Must be a reference type.</typeparam>
        /// <param name="control">The control that will own the cache.</param>
        /// <returns>A new thread-safe cache instance registered with the <see cref="InvalidationManager"/>.</returns>
        public static ThreadSafeCache<T> CreateThreadSafeCache<T>(this IWindowControl control) where T : class
        {
            return new ThreadSafeCache<T>(control);
        }

        /// <summary>
        /// Safely invalidates a control by requesting invalidation through the <see cref="InvalidationManager"/>.
        /// </summary>
        /// <param name="control">The control to invalidate.</param>
        /// <param name="reason">The reason for invalidation, used for determining propagation and batching.</param>
        public static void SafeInvalidate(this IWindowControl control, InvalidationReason reason)
        {
            InvalidationManager.Instance.RequestInvalidation(control, reason);
        }
    }
}