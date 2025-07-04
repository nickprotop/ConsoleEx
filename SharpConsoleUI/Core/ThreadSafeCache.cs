using System;
using System.Collections.Generic;
using System.Threading;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Core
{
    /// <summary>
    /// Thread-safe wrapper for cached content with atomic operations
    /// </summary>
    public class ThreadSafeCache<T> where T : class
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private T _content;
        private volatile bool _isValid = false;
        private readonly IWIndowControl _owner;
        private readonly InvalidationManager _invalidationManager;

        public ThreadSafeCache(IWIndowControl owner)
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
        /// Sets the cached content and marks as valid
        /// </summary>
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
        /// Invalidates the cache
        /// </summary>
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
        /// Gets content or renders it if invalid, with thread safety
        /// </summary>
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
        /// Tries to get content without triggering render
        /// </summary>
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
        /// Clears the cache and disposes resources
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
        /// Creates a thread-safe cache for a control
        /// </summary>
        public static ThreadSafeCache<T> CreateThreadSafeCache<T>(this IWIndowControl control) where T : class
        {
            return new ThreadSafeCache<T>(control);
        }

        /// <summary>
        /// Safely invalidates a control with reason
        /// </summary>
        public static void SafeInvalidate(this IWIndowControl control, InvalidationReason reason)
        {
            InvalidationManager.Instance.RequestInvalidation(control, reason);
        }
    }
}