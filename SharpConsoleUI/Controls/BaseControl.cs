// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;

namespace SharpConsoleUI.Controls
{
    /// <summary>
    /// Base class for all controls with standardized invalidation methods
    /// </summary>
    public abstract class BaseControl : IWindowControl
    {
        protected readonly ThreadSafeCache<List<string>> _contentCache;
        private Alignment _alignment = Alignment.Left;
        private IContainer? _container;
        private Margin _margin = new Margin(0, 0, 0, 0);
        private StickyPosition _stickyPosition = StickyPosition.None;
        private bool _visible = true;
        private object? _tag;

        protected BaseControl()
        {
            _contentCache = this.CreateThreadSafeCache<List<string>>();
        }

        #region IWindowControl Implementation

        public virtual int? ActualWidth
        {
            get
            {
                var content = _contentCache.Content;
                if (content == null) return null;

                int maxLength = 0;
                foreach (var line in content)
                {
                    int length = Helpers.AnsiConsoleHelper.StripAnsiStringLength(line);
                    if (length > maxLength) maxLength = length;
                }
                return maxLength;
            }
        }

        public virtual Alignment Alignment
        {
            get => _alignment;
            set
            {
                if (_alignment != value)
                {
                    _alignment = value;
                    InvalidateLayout();
                }
            }
        }

        public virtual IContainer? Container
        {
            get => _container;
            set
            {
                if (_container != value)
                {
                    _container = value;
                    InvalidateProperty();
                }
            }
        }

        public virtual Margin Margin
        {
            get => _margin;
            set
            {
                if (!_margin.Equals(value))
                {
                    _margin = value;
                    InvalidateLayout();
                }
            }
        }

        public virtual StickyPosition StickyPosition
        {
            get => _stickyPosition;
            set
            {
                if (_stickyPosition != value)
                {
                    _stickyPosition = value;
                    InvalidateLayout();
                }
            }
        }

        public virtual object? Tag
        {
            get => _tag;
            set => _tag = value;
        }

        public virtual bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    InvalidateProperty();
                }
            }
        }

        public virtual int? Width { get; set; }

        public abstract List<string> RenderContent(int? availableWidth, int? availableHeight);

        public virtual System.Drawing.Size GetLogicalContentSize()
        {
            var content = RenderContent(10000, 10000);
            return new System.Drawing.Size(
                content.FirstOrDefault()?.Length ?? 0,
                content.Count
            );
        }

        public virtual void Invalidate()
        {
            InvalidateContent();
        }

        public virtual void Dispose()
        {
            _contentCache?.Dispose();
            _container = null;
        }

        #endregion

        #region Standardized Invalidation Methods

        /// <summary>
        /// Invalidates due to property changes (colors, text, etc.)
        /// </summary>
        protected virtual void InvalidateProperty()
        {
            _contentCache.Invalidate(InvalidationReason.PropertyChanged);
        }

        /// <summary>
        /// Invalidates due to content changes (items added/removed, text changed)
        /// </summary>
        protected virtual void InvalidateContent()
        {
            _contentCache.Invalidate(InvalidationReason.ContentChanged);
        }

        /// <summary>
        /// Invalidates due to layout changes (size, position, alignment)
        /// </summary>
        protected virtual void InvalidateLayout()
        {
            _contentCache.Invalidate(InvalidationReason.SizeChanged);
        }

        /// <summary>
        /// Invalidates due to state changes (focus, selection, enabled)
        /// </summary>
        protected virtual void InvalidateState()
        {
            _contentCache.Invalidate(InvalidationReason.StateChanged);
        }

        /// <summary>
        /// Invalidates due to theme changes
        /// </summary>
        protected virtual void InvalidateTheme()
        {
            _contentCache.Invalidate(InvalidationReason.ThemeChanged);
        }

        /// <summary>
        /// Invalidates with a specific reason
        /// </summary>
        protected virtual void Invalidate(InvalidationReason reason)
        {
            _contentCache.Invalidate(reason);
        }

        #endregion
    }
}