// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace SharpConsoleUI.Core;

/// <summary>
/// Manager for tracking and disposing resources in a coordinated manner
/// </summary>
public sealed class DisposableManager : IDisposable, IAsyncDisposable
{
	private readonly ConcurrentBag<IDisposable> _disposables = new();
	private readonly ConcurrentBag<IAsyncDisposable> _asyncDisposables = new();
	private readonly ConcurrentBag<Func<Task>> _disposalTasks = new();
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DisposableManager"/> class
	/// </summary>
	public DisposableManager()
	{
	}

	/// <summary>
	/// Gets whether the manager has been disposed
	/// </summary>
	public bool IsDisposed => _disposed;

	/// <summary>
	/// Registers a disposable resource for management
	/// </summary>
	/// <param name="disposable">The disposable resource</param>
	/// <returns>The registered resource for fluent chaining</returns>
	/// <exception cref="ObjectDisposedException">Thrown if the manager is already disposed</exception>
	public T Register<T>(T disposable) where T : IDisposable
	{
		ThrowIfDisposed();

		if (disposable == null)
			return disposable;

		_disposables.Add(disposable);

		if (disposable is IAsyncDisposable asyncDisposable)
		{
			_asyncDisposables.Add(asyncDisposable);
		}

		return disposable;
	}

	/// <summary>
	/// Registers an async disposable resource for management
	/// </summary>
	/// <param name="asyncDisposable">The async disposable resource</param>
	/// <returns>The registered resource for fluent chaining</returns>
	/// <exception cref="ObjectDisposedException">Thrown if the manager is already disposed</exception>
	public T RegisterAsync<T>(T asyncDisposable) where T : IAsyncDisposable
	{
		ThrowIfDisposed();

		if (asyncDisposable == null)
			return asyncDisposable;

		_asyncDisposables.Add(asyncDisposable);

		if (asyncDisposable is IDisposable disposable)
		{
			_disposables.Add(disposable);
		}

		return asyncDisposable;
	}

	/// <summary>
	/// Registers a custom disposal task
	/// </summary>
	/// <param name="disposalTask">The disposal task</param>
	/// <exception cref="ObjectDisposedException">Thrown if the manager is already disposed</exception>
	public void RegisterDisposalTask(Func<Task> disposalTask)
	{
		ThrowIfDisposed();

		if (disposalTask == null)
			throw new ArgumentNullException(nameof(disposalTask));

		_disposalTasks.Add(disposalTask);
	}

	/// <summary>
	/// Registers a disposal action (synchronous)
	/// </summary>
	/// <param name="disposalAction">The disposal action</param>
	/// <exception cref="ObjectDisposedException">Thrown if the manager is already disposed</exception>
	public void RegisterDisposalAction(Action disposalAction)
	{
		ThrowIfDisposed();

		if (disposalAction == null)
			throw new ArgumentNullException(nameof(disposalAction));

		_disposalTasks.Add(() =>
		{
			disposalAction();
			return Task.CompletedTask;
		});
	}

	/// <summary>
	/// Creates a disposal scope that will be automatically registered
	/// </summary>
	/// <returns>A disposal scope</returns>
	public DisposalScope CreateScope()
	{
		ThrowIfDisposed();
		return Register(new DisposalScope());
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
			return;

		// Execute custom disposal tasks first
		foreach (var task in _disposalTasks)
		{
			try
			{
				task().GetAwaiter().GetResult();
			}
			catch
			{
				// Swallow exceptions during disposal
			}
		}

		// Dispose synchronous resources
		foreach (var disposable in _disposables)
		{
			try
			{
				disposable.Dispose();
			}
			catch
			{
				// Swallow exceptions during disposal
			}
		}

		_disposed = true;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
			return;

		// Execute custom disposal tasks first
		var disposalTasksList = _disposalTasks.ToArray();
		if (disposalTasksList.Length > 0)
		{
			var tasks = disposalTasksList.Select(async task =>
			{
				try
				{
					await task();
				}
				catch
				{
					// Swallow exceptions during disposal
				}
			});

			await Task.WhenAll(tasks);
		}

		// Dispose async resources
		foreach (var asyncDisposable in _asyncDisposables)
		{
			try
			{
				await asyncDisposable.DisposeAsync();
			}
			catch
			{
				// Swallow exceptions during disposal
			}
		}

		// Dispose remaining synchronous resources that aren't async disposable
		var remainingDisposables = _disposables.Except(_asyncDisposables.OfType<IDisposable>()).ToArray();
		foreach (var disposable in remainingDisposables)
		{
			try
			{
				disposable.Dispose();
			}
			catch
			{
				// Swallow exceptions during disposal
			}
		}

		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(DisposableManager));
	}
}

/// <summary>
/// A disposal scope that can register resources for disposal when the scope is disposed
/// </summary>
public sealed class DisposalScope : IDisposable, IAsyncDisposable
{
	private readonly DisposableManager _manager;

	internal DisposalScope()
	{
		_manager = new DisposableManager();
	}

	/// <summary>
	/// Gets whether the scope has been disposed
	/// </summary>
	public bool IsDisposed => _manager.IsDisposed;

	/// <summary>
	/// Registers a disposable resource in this scope
	/// </summary>
	/// <param name="disposable">The disposable resource</param>
	/// <returns>The registered resource for fluent chaining</returns>
	public T Register<T>(T disposable) where T : IDisposable
	{
		return _manager.Register(disposable);
	}

	/// <summary>
	/// Registers an async disposable resource in this scope
	/// </summary>
	/// <param name="asyncDisposable">The async disposable resource</param>
	/// <returns>The registered resource for fluent chaining</returns>
	public T RegisterAsync<T>(T asyncDisposable) where T : IAsyncDisposable
	{
		return _manager.RegisterAsync(asyncDisposable);
	}

	/// <summary>
	/// Registers a disposal action for this scope
	/// </summary>
	/// <param name="disposalAction">The disposal action</param>
	public void RegisterDisposalAction(Action disposalAction)
	{
		_manager.RegisterDisposalAction(disposalAction);
	}

	/// <inheritdoc />
	public void Dispose() => _manager.Dispose();

	/// <inheritdoc />
	public ValueTask DisposeAsync() => _manager.DisposeAsync();
}

/// <summary>
/// Extension methods for easier resource management
/// </summary>
public static class DisposableManagerExtensions
{
	/// <summary>
	/// Registers the object for disposal if it implements IDisposable
	/// </summary>
	/// <param name="manager">The disposable manager</param>
	/// <param name="obj">The object to register</param>
	/// <returns>The object for fluent chaining</returns>
	public static T RegisterIfDisposable<T>(this DisposableManager manager, T obj)
	{
		if (obj is IDisposable disposable)
		{
			manager.Register(disposable);
		}

		return obj;
	}

	/// <summary>
	/// Registers the object for disposal if it implements IAsyncDisposable
	/// </summary>
	/// <param name="manager">The disposable manager</param>
	/// <param name="obj">The object to register</param>
	/// <returns>The object for fluent chaining</returns>
	public static T RegisterIfAsyncDisposable<T>(this DisposableManager manager, T obj)
	{
		if (obj is IAsyncDisposable asyncDisposable)
		{
			manager.RegisterAsync(asyncDisposable);
		}

		return obj;
	}
}
