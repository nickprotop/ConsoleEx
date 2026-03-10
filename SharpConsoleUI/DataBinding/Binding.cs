using System.ComponentModel;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// One-way binding: subscribes to source <see cref="INotifyPropertyChanged"/> and pushes
/// values to a target setter when the specified property changes.
/// </summary>
internal sealed class OneWayBinding<TSource, TSrc, TTgt> : IDisposable
	where TSource : INotifyPropertyChanged
{
	private readonly TSource _source;
	private readonly string _sourcePropertyName;
	private readonly Func<TSource, TSrc> _sourceGetter;
	private readonly Action<TTgt> _targetSetter;
	private readonly Func<TSrc, TTgt> _converter;

	public OneWayBinding(
		TSource source,
		string sourcePropertyName,
		Func<TSource, TSrc> sourceGetter,
		Action<TTgt> targetSetter,
		Func<TSrc, TTgt> converter)
	{
		_source = source;
		_sourcePropertyName = sourcePropertyName;
		_sourceGetter = sourceGetter;
		_targetSetter = targetSetter;
		_converter = converter;

		// Apply initial value
		_targetSetter(_converter(_sourceGetter(_source)));

		// Subscribe to changes
		_source.PropertyChanged += OnSourcePropertyChanged;
	}

	private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == _sourcePropertyName)
			_targetSetter(_converter(_sourceGetter(_source)));
	}

	public void Dispose()
	{
		_source.PropertyChanged -= OnSourcePropertyChanged;
	}
}

/// <summary>
/// Two-way binding: subscribes to <see cref="INotifyPropertyChanged"/> on both source and target,
/// synchronizing values in both directions with a re-entrancy guard.
/// </summary>
internal sealed class TwoWayBinding<TSource, TControl, TSrc, TTgt> : IDisposable
	where TSource : INotifyPropertyChanged
	where TControl : INotifyPropertyChanged
{
	private readonly TSource _source;
	private readonly TControl _target;
	private readonly string _sourcePropertyName;
	private readonly string _targetPropertyName;
	private readonly Func<TSource, TSrc> _sourceGetter;
	private readonly Action<TSrc> _sourceSetter;
	private readonly Func<TControl, TTgt> _targetGetter;
	private readonly Action<TTgt> _targetSetter;
	private readonly Func<TSrc, TTgt> _toTarget;
	private readonly Func<TTgt, TSrc> _toSource;
	private bool _updating;

	public TwoWayBinding(
		TSource source,
		string sourcePropertyName,
		Func<TSource, TSrc> sourceGetter,
		Action<TSrc> sourceSetter,
		TControl target,
		string targetPropertyName,
		Func<TControl, TTgt> targetGetter,
		Action<TTgt> targetSetter,
		Func<TSrc, TTgt> toTarget,
		Func<TTgt, TSrc> toSource)
	{
		_source = source;
		_target = target;
		_sourcePropertyName = sourcePropertyName;
		_targetPropertyName = targetPropertyName;
		_sourceGetter = sourceGetter;
		_sourceSetter = sourceSetter;
		_targetGetter = targetGetter;
		_targetSetter = targetSetter;
		_toTarget = toTarget;
		_toSource = toSource;

		// Apply initial value (source → target)
		_targetSetter(_toTarget(_sourceGetter(_source)));

		// Subscribe to both sides
		_source.PropertyChanged += OnSourceChanged;
		_target.PropertyChanged += OnTargetChanged;
	}

	private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updating || e.PropertyName != _sourcePropertyName) return;
		_updating = true;
		try { _targetSetter(_toTarget(_sourceGetter(_source))); }
		finally { _updating = false; }
	}

	private void OnTargetChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updating || e.PropertyName != _targetPropertyName) return;
		_updating = true;
		try { _sourceSetter(_toSource(_targetGetter(_target))); }
		finally { _updating = false; }
	}

	public void Dispose()
	{
		_source.PropertyChanged -= OnSourceChanged;
		_target.PropertyChanged -= OnTargetChanged;
	}
}
