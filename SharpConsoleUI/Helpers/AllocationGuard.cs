// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Guards against integer overflow and unbounded allocations when computing
	/// buffer sizes from potentially untrusted dimensions. Use this whenever
	/// allocating arrays whose size derives from external input (image dimensions,
	/// video frame sizes, terminal coordinates from protocol responses).
	/// </summary>
	public static class AllocationGuard
	{
		/// <summary>
		/// Default maximum total elements allowed in a single allocation (64 million).
		/// Covers up to ~256 MB for byte arrays or ~512 MB for int arrays.
		/// </summary>
		public const long DefaultMaxElements = 64_000_000;

		/// <summary>
		/// Validates that width * height does not overflow and does not exceed
		/// the specified maximum. Returns the product as a checked long.
		/// </summary>
		/// <param name="width">First dimension.</param>
		/// <param name="height">Second dimension.</param>
		/// <param name="maxElements">Maximum allowed product.</param>
		/// <returns>The validated product (width * height).</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown if either dimension is non-positive or the product exceeds the limit.
		/// </exception>
		public static long ValidateDimensions(int width, int height, long maxElements = DefaultMaxElements)
		{
			if (width <= 0)
				throw new ArgumentOutOfRangeException(nameof(width), width, "Dimension must be positive.");
			if (height <= 0)
				throw new ArgumentOutOfRangeException(nameof(height), height, "Dimension must be positive.");

			long product = (long)width * height;

			if (product > maxElements)
				throw new ArgumentOutOfRangeException(
					$"Allocation of {width}x{height} ({product:N0} elements) exceeds maximum of {maxElements:N0}.");

			return product;
		}

		/// <summary>
		/// Validates that width * height * depth does not overflow and does not
		/// exceed the specified maximum. Used for RGB byte buffers (depth=3).
		/// </summary>
		/// <param name="width">First dimension.</param>
		/// <param name="height">Second dimension.</param>
		/// <param name="depth">Third dimension (e.g., bytes per pixel).</param>
		/// <param name="maxElements">Maximum allowed product.</param>
		/// <returns>The validated product (width * height * depth).</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// Thrown if any dimension is non-positive or the product exceeds the limit.
		/// </exception>
		public static long ValidateDimensions(int width, int height, int depth, long maxElements = DefaultMaxElements)
		{
			if (width <= 0)
				throw new ArgumentOutOfRangeException(nameof(width), width, "Dimension must be positive.");
			if (height <= 0)
				throw new ArgumentOutOfRangeException(nameof(height), height, "Dimension must be positive.");
			if (depth <= 0)
				throw new ArgumentOutOfRangeException(nameof(depth), depth, "Dimension must be positive.");

			long product = (long)width * height * depth;

			if (product > maxElements)
				throw new ArgumentOutOfRangeException(
					$"Allocation of {width}x{height}x{depth} ({product:N0} elements) exceeds maximum of {maxElements:N0}.");

			return product;
		}

		/// <summary>
		/// Returns true if the given dimensions are within the specified limits.
		/// Does not throw. Useful for early-return guards where throwing is not appropriate.
		/// </summary>
		/// <param name="width">First dimension.</param>
		/// <param name="height">Second dimension.</param>
		/// <param name="maxDimension">Maximum value for each individual dimension.</param>
		/// <param name="maxElements">Maximum allowed product.</param>
		/// <returns>True if dimensions are valid and within limits.</returns>
		public static bool AreDimensionsValid(int width, int height, int maxDimension = int.MaxValue, long maxElements = DefaultMaxElements)
		{
			if (width <= 0 || height <= 0)
				return false;
			if (width > maxDimension || height > maxDimension)
				return false;
			return (long)width * height <= maxElements;
		}
	}
}
