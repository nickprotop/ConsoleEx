namespace SharpConsoleUI.Animation;

/// <summary>
/// Interpolates between two values of type T based on a normalized progress [0,1].
/// </summary>
public interface IInterpolator<T>
{
	/// <summary>Computes the interpolated value between <paramref name="from"/> and <paramref name="to"/>.</summary>
	T Interpolate(T from, T to, double t);
}
