using Spectre.Console;

namespace SharpConsoleUI.Helpers
{
    /// <summary>
    /// Helper for creating and interpolating smooth color gradients.
    /// Supports predefined gradients (cool, warm, spectrum, grayscale) and custom gradients.
    /// </summary>
    public class ColorGradient
    {
        private readonly List<Color> _stops;

        /// <summary>
        /// Predefined gradients available for use.
        /// </summary>
        public static readonly Dictionary<string, ColorGradient> Predefined = new()
        {
            ["cool"] = FromColors(Color.Blue, Color.Cyan1),
            ["warm"] = FromColors(Color.Yellow, Color.Orange1, Color.Red),
            ["spectrum"] = FromColors(Color.Blue, Color.Green, Color.Yellow, Color.Red),
            ["grayscale"] = FromColors(Color.Grey11, Color.Grey100)
        };

        private ColorGradient(List<Color> stops)
        {
            if (stops == null || stops.Count < 2)
                throw new ArgumentException("Gradient must have at least 2 color stops", nameof(stops));

            _stops = stops;
        }

        /// <summary>
        /// Creates a gradient from a sequence of colors.
        /// </summary>
        /// <param name="colors">Colors to use as gradient stops (minimum 2 required)</param>
        /// <returns>New ColorGradient instance</returns>
        public static ColorGradient FromColors(params Color[] colors)
        {
            if (colors == null || colors.Length < 2)
                throw new ArgumentException("At least 2 colors are required", nameof(colors));

            return new ColorGradient(new List<Color>(colors));
        }

        /// <summary>
        /// Parses a gradient specification string.
        /// Supports predefined names (cool, warm, spectrum, grayscale),
        /// arrow notation (blue→cyan→green), and :reverse suffix.
        /// </summary>
        /// <param name="spec">Gradient specification (e.g., "cool", "blue→cyan", "warm:reverse")</param>
        /// <returns>Parsed ColorGradient, or null if parsing fails</returns>
        public static ColorGradient? Parse(string? spec)
        {
            if (string.IsNullOrWhiteSpace(spec))
                return null;

            // Check for :reverse suffix
            bool reverse = false;
            if (spec.EndsWith(":reverse", StringComparison.OrdinalIgnoreCase))
            {
                reverse = true;
                spec = spec[..^8]; // Remove ":reverse"
            }

            ColorGradient? gradient = null;

            // Try predefined gradients first
            if (Predefined.TryGetValue(spec.ToLowerInvariant(), out var predefined))
            {
                gradient = predefined;
            }
            // Try arrow notation (e.g., "blue→cyan→green")
            else if (spec.Contains('→'))
            {
                var colorNames = spec.Split('→', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (colorNames.Length >= 2)
                {
                    var colors = new List<Color>();
                    foreach (var colorName in colorNames)
                    {
                        var color = ParseSpectreColor(colorName);
                        if (color.HasValue)
                        {
                            colors.Add(color.Value);
                        }
                        else
                        {
                            return null; // Failed to parse a color name
                        }
                    }

                    if (colors.Count >= 2)
                    {
                        gradient = new ColorGradient(colors);
                    }
                }
            }
            // Try single color name (will create a solid "gradient")
            else
            {
                var color = ParseSpectreColor(spec);
                if (color.HasValue)
                {
                    gradient = FromColors(color.Value, color.Value);
                }
            }

            // Apply reversal if requested
            return reverse && gradient != null ? gradient.Reverse() : gradient;
        }

        /// <summary>
        /// Gets a color at a specific position in the gradient.
        /// </summary>
        /// <param name="normalizedValue">Position in gradient (0.0 = first color, 1.0 = last color)</param>
        /// <returns>Interpolated color at the specified position</returns>
        public Color Interpolate(double normalizedValue)
        {
            // Clamp to valid range
            normalizedValue = Math.Clamp(normalizedValue, 0.0, 1.0);

            // Handle edge cases
            if (normalizedValue <= 0.0)
                return _stops[0];
            if (normalizedValue >= 1.0)
                return _stops[^1];

            // Calculate which segment we're in
            int segmentCount = _stops.Count - 1;
            double scaledValue = normalizedValue * segmentCount;
            int segmentIndex = (int)Math.Floor(scaledValue);

            // Handle exact matches
            if (segmentIndex >= segmentCount)
                return _stops[^1];

            // Calculate position within segment
            double segmentPosition = scaledValue - segmentIndex;

            // Blend between the two colors
            return BlendColors(_stops[segmentIndex], _stops[segmentIndex + 1], segmentPosition);
        }

        /// <summary>
        /// Returns a new gradient with reversed color order.
        /// </summary>
        /// <returns>New ColorGradient with colors in reverse order</returns>
        public ColorGradient Reverse()
        {
            var reversedStops = new List<Color>(_stops);
            reversedStops.Reverse();
            return new ColorGradient(reversedStops);
        }

        /// <summary>
        /// Linearly blends two colors based on position.
        /// </summary>
        /// <param name="c1">First color</param>
        /// <param name="c2">Second color</param>
        /// <param name="position">Blend position (0.0 = c1, 1.0 = c2)</param>
        /// <returns>Blended color</returns>
        private static Color BlendColors(Color c1, Color c2, double position)
        {
            position = Math.Clamp(position, 0.0, 1.0);

            byte r = (byte)(c1.R + (c2.R - c1.R) * position);
            byte g = (byte)(c1.G + (c2.G - c1.G) * position);
            byte b = (byte)(c1.B + (c2.B - c1.B) * position);

            return new Color(r, g, b);
        }

        /// <summary>
        /// Parses a Spectre.Console color name.
        /// Supports standard color names (red, blue, etc.) and RGB notation.
        /// </summary>
        /// <param name="colorName">Color name to parse</param>
        /// <returns>Parsed Color, or null if parsing fails</returns>
        private static Color? ParseSpectreColor(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return null;

            try
            {
                // Try parsing as a standard color name
                // Spectre.Console uses reflection to find color properties
                var colorType = typeof(Color);
                var property = colorType.GetProperty(colorName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);

                if (property != null && property.PropertyType == typeof(Color))
                {
                    return (Color?)property.GetValue(null);
                }

                // Try parsing as RGB hex (e.g., "#FF5733" or "FF5733")
                colorName = colorName.TrimStart('#');
                if (colorName.Length == 6 && int.TryParse(colorName, System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                {
                    byte r = (byte)((hexValue >> 16) & 0xFF);
                    byte g = (byte)((hexValue >> 8) & 0xFF);
                    byte b = (byte)(hexValue & 0xFF);
                    return new Color(r, g, b);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
