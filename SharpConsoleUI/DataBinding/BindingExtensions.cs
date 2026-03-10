using System.ComponentModel;
using System.Linq.Expressions;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Extension methods for declarative one-way and two-way data binding.
/// </summary>
public static class BindingExtensions
{
	#region One-way binding on controls

	/// <summary>
	/// Creates a one-way binding from a source property to a control property (same type).
	/// </summary>
	public static TControl Bind<TControl, TSource, TValue>(
		this TControl control,
		TSource source,
		Expression<Func<TSource, TValue>> sourceExpr,
		Expression<Func<TControl, TValue>> targetExpr)
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		return Bind(control, source, sourceExpr, targetExpr, v => v);
	}

	/// <summary>
	/// Creates a one-way binding from a source property to a control property with a converter.
	/// </summary>
	public static TControl Bind<TControl, TSource, TSrc, TTgt>(
		this TControl control,
		TSource source,
		Expression<Func<TSource, TSrc>> sourceExpr,
		Expression<Func<TControl, TTgt>> targetExpr,
		Func<TSrc, TTgt> converter)
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		var sourceName = BindingHelper.GetPropertyName(sourceExpr);
		var sourceGetter = sourceExpr.Compile();
		var targetSetter = BindingHelper.CreateSetter(targetExpr);

		var binding = new OneWayBinding<TSource, TSrc, TTgt>(
			source,
			sourceName,
			sourceGetter,
			value => targetSetter(control, value),
			converter);

		control.Bindings.Add(binding);
		return control;
	}

	#endregion

	#region Two-way binding on controls

	/// <summary>
	/// Creates a two-way binding between a source property and a control property (same type).
	/// Both sides must implement <see cref="INotifyPropertyChanged"/>.
	/// </summary>
	public static TControl BindTwoWay<TControl, TSource, TValue>(
		this TControl control,
		TSource source,
		Expression<Func<TSource, TValue>> sourceExpr,
		Expression<Func<TControl, TValue>> targetExpr)
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		return BindTwoWay(control, source, sourceExpr, targetExpr, v => v, v => v);
	}

	/// <summary>
	/// Creates a two-way binding with converters in both directions.
	/// </summary>
	public static TControl BindTwoWay<TControl, TSource, TSrc, TTgt>(
		this TControl control,
		TSource source,
		Expression<Func<TSource, TSrc>> sourceExpr,
		Expression<Func<TControl, TTgt>> targetExpr,
		Func<TSrc, TTgt> toTarget,
		Func<TTgt, TSrc> toSource)
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		var sourceName = BindingHelper.GetPropertyName(sourceExpr);
		var targetName = BindingHelper.GetPropertyName(targetExpr);
		var sourceGetter = sourceExpr.Compile();
		var sourceSetter = BindingHelper.CreateSetter(sourceExpr);
		var targetGetter = targetExpr.Compile();
		var targetSetter = BindingHelper.CreateSetter(targetExpr);

		var binding = new TwoWayBinding<TSource, TControl, TSrc, TTgt>(
			source,
			sourceName,
			sourceGetter,
			value => sourceSetter(source, value),
			control,
			targetName,
			targetGetter,
			value => targetSetter(control, value),
			toTarget,
			toSource);

		control.Bindings.Add(binding);
		return control;
	}

	#endregion

	#region Builder binding support

	/// <summary>
	/// Creates a deferred one-way binding on a builder (same type).
	/// </summary>
	public static TBuilder Bind<TBuilder, TControl, TSource, TValue>(
		this TBuilder builder,
		TSource source,
		Expression<Func<TSource, TValue>> sourceExpr,
		Expression<Func<TControl, TValue>> targetExpr)
		where TBuilder : IControlBuilder<TControl>
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		return Bind(builder, source, sourceExpr, targetExpr, v => v);
	}

	/// <summary>
	/// Creates a deferred one-way binding on a builder with a converter.
	/// </summary>
	public static TBuilder Bind<TBuilder, TControl, TSource, TSrc, TTgt>(
		this TBuilder builder,
		TSource source,
		Expression<Func<TSource, TSrc>> sourceExpr,
		Expression<Func<TControl, TTgt>> targetExpr,
		Func<TSrc, TTgt> converter)
		where TBuilder : IControlBuilder<TControl>
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		var sourceName = BindingHelper.GetPropertyName(sourceExpr);
		var sourceGetter = sourceExpr.Compile();
		var targetSetter = BindingHelper.CreateSetter(targetExpr);

		BindingHelper.AddDeferredBinding(builder, baseControl =>
		{
			var control = (TControl)baseControl;
			var binding = new OneWayBinding<TSource, TSrc, TTgt>(
				source,
				sourceName,
				sourceGetter,
				value => targetSetter(control, value),
				converter);
			control.Bindings.Add(binding);
		});

		return builder;
	}

	/// <summary>
	/// Creates a deferred two-way binding on a builder (same type).
	/// </summary>
	public static TBuilder BindTwoWay<TBuilder, TControl, TSource, TValue>(
		this TBuilder builder,
		TSource source,
		Expression<Func<TSource, TValue>> sourceExpr,
		Expression<Func<TControl, TValue>> targetExpr)
		where TBuilder : IControlBuilder<TControl>
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		return BindTwoWay(builder, source, sourceExpr, targetExpr, v => v, v => v);
	}

	/// <summary>
	/// Creates a deferred two-way binding on a builder with converters.
	/// </summary>
	public static TBuilder BindTwoWay<TBuilder, TControl, TSource, TSrc, TTgt>(
		this TBuilder builder,
		TSource source,
		Expression<Func<TSource, TSrc>> sourceExpr,
		Expression<Func<TControl, TTgt>> targetExpr,
		Func<TSrc, TTgt> toTarget,
		Func<TTgt, TSrc> toSource)
		where TBuilder : IControlBuilder<TControl>
		where TControl : BaseControl
		where TSource : INotifyPropertyChanged
	{
		var sourceName = BindingHelper.GetPropertyName(sourceExpr);
		var targetName = BindingHelper.GetPropertyName(targetExpr);
		var sourceGetter = sourceExpr.Compile();
		var sourceSetter = BindingHelper.CreateSetter(sourceExpr);
		var targetGetter = targetExpr.Compile();
		var targetSetter = BindingHelper.CreateSetter(targetExpr);

		BindingHelper.AddDeferredBinding(builder, baseControl =>
		{
			var control = (TControl)baseControl;
			var binding = new TwoWayBinding<TSource, TControl, TSrc, TTgt>(
				source,
				sourceName,
				sourceGetter,
				value => sourceSetter(source, value),
				control,
				targetName,
				targetGetter,
				value => targetSetter(control, value),
				toTarget,
				toSource);
			control.Bindings.Add(binding);
		});

		return builder;
	}

	#endregion
}
