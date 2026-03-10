namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Thread-safe collection of binding subscriptions owned by a control.
/// Disposed when the control is disposed.
/// </summary>
public sealed class BindingCollection : IDisposable
{
	private readonly object _lock = new();
	private List<IDisposable>? _bindings = new();

	/// <summary>
	/// Adds a binding subscription to the collection.
	/// </summary>
	public void Add(IDisposable binding)
	{
		lock (_lock)
		{
			if (_bindings == null)
				throw new ObjectDisposedException(nameof(BindingCollection));
			_bindings.Add(binding);
		}
	}

	/// <summary>
	/// Disposes all bindings and prevents new ones from being added.
	/// </summary>
	public void Dispose()
	{
		List<IDisposable>? snapshot;
		lock (_lock)
		{
			snapshot = _bindings;
			_bindings = null;
		}

		if (snapshot != null)
		{
			foreach (var binding in snapshot)
				binding.Dispose();
		}
	}
}
