using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Utility methods for expression-based data binding.
/// </summary>
public static class BindingHelper
{
	/// <summary>
	/// Extracts the property name from a member-access expression.
	/// </summary>
	public static string GetPropertyName<T, TValue>(Expression<Func<T, TValue>> expression)
	{
		if (expression.Body is MemberExpression member)
			return member.Member.Name;

		if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
			return unaryMember.Member.Name;

		throw new ArgumentException("Expression must be a property access.", nameof(expression));
	}

	/// <summary>
	/// Compiles a setter action from a property-access expression.
	/// </summary>
	public static Action<TTarget, TValue> CreateSetter<TTarget, TValue>(
		Expression<Func<TTarget, TValue>> expression)
	{
		var memberExpr = expression.Body as MemberExpression
			?? (expression.Body as UnaryExpression)?.Operand as MemberExpression
			?? throw new ArgumentException("Expression must be a property access.", nameof(expression));

		var targetParam = Expression.Parameter(typeof(TTarget), "target");
		var valueParam = Expression.Parameter(typeof(TValue), "value");
		var property = Expression.MakeMemberAccess(targetParam, memberExpr.Member);
		var assign = Expression.Assign(property, valueParam);

		return Expression.Lambda<Action<TTarget, TValue>>(assign, targetParam, valueParam).Compile();
	}

	// Deferred bindings for builder support
	private static readonly ConditionalWeakTable<object, List<Action<BaseControl>>> _deferredBindings = new();

	/// <summary>
	/// Stores a binding action to be applied when the builder produces its control.
	/// </summary>
	public static void AddDeferredBinding(object builder, Action<BaseControl> apply)
	{
		var list = _deferredBindings.GetOrCreateValue(builder);
		list.Add(apply);
	}

	/// <summary>
	/// Applies all deferred bindings to the built control, then removes them.
	/// Called from <c>Build()</c> methods on builders that implement <see cref="IControlBuilder{TControl}"/>.
	/// </summary>
	public static void ApplyDeferredBindings(object builder, BaseControl control)
	{
		if (_deferredBindings.TryGetValue(builder, out var list))
		{
			foreach (var apply in list)
				apply(control);
			_deferredBindings.Remove(builder);
		}
	}
}
