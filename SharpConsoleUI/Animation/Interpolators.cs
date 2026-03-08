namespace SharpConsoleUI.Animation;

/// <summary>
/// Interpolates between two byte values, clamped to [0,255].
/// </summary>
public sealed class ByteInterpolator : IInterpolator<byte>
{
	/// <summary>Singleton instance.</summary>
	public static readonly ByteInterpolator Instance = new();

	/// <inheritdoc />
	public byte Interpolate(byte from, byte to, double t) =>
		(byte)Math.Clamp((int)Math.Round(from + (to - from) * t), 0, 255);
}

/// <summary>
/// Interpolates between two int values with rounding.
/// </summary>
public sealed class IntInterpolator : IInterpolator<int>
{
	/// <summary>Singleton instance.</summary>
	public static readonly IntInterpolator Instance = new();

	/// <inheritdoc />
	public int Interpolate(int from, int to, double t) =>
		(int)Math.Round(from + (to - from) * t);
}

/// <summary>
/// Interpolates between two float values.
/// </summary>
public sealed class FloatInterpolator : IInterpolator<float>
{
	/// <summary>Singleton instance.</summary>
	public static readonly FloatInterpolator Instance = new();

	/// <inheritdoc />
	public float Interpolate(float from, float to, double t) =>
		(float)(from + (to - from) * t);
}

/// <summary>
/// Interpolates between two Color values by blending each RGB channel independently.
/// </summary>
public sealed class ColorInterpolator : IInterpolator<Color>
{
	/// <summary>Singleton instance.</summary>
	public static readonly ColorInterpolator Instance = new();

	/// <inheritdoc />
	public Color Interpolate(Color from, Color to, double t)
	{
		byte r = ByteInterpolator.Instance.Interpolate(from.R, to.R, t);
		byte g = ByteInterpolator.Instance.Interpolate(from.G, to.G, t);
		byte b = ByteInterpolator.Instance.Interpolate(from.B, to.B, t);
		return new Color(r, g, b);
	}
}
