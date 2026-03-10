// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Controls
{
	public partial class SparklineControl
	{
		/// <summary>
		/// Gets the secondary data points collection (for bidirectional mode).
		/// </summary>
		public IReadOnlyList<double> SecondaryDataPoints
		{
			get
			{
				lock (_dataLock)
				{
					return new List<double>(_secondaryDataPoints);
				}
			}
		}

		/// <summary>
		/// Gets or sets the color for the secondary bars (in bidirectional mode).
		/// </summary>
		public Color SecondaryBarColor
		{
			get => _secondaryBarColor;
			set => SetProperty(ref _secondaryBarColor, value);
		}

		/// <summary>
		/// Gets or sets the maximum value for the secondary data series scale.
		/// When null, uses the same scale as the primary series or the max secondary data value.
		/// </summary>
		public double? SecondaryMaxValue
		{
			get => _secondaryMaxValue;
			set => SetProperty(ref _secondaryMaxValue, value);
		}

		/// <summary>
		/// Gets or sets the color gradient for the secondary series in bidirectional mode.
		/// When null in bidirectional mode, uses SecondaryBarColor or the primary Gradient.
		/// </summary>
		public ColorGradient? SecondaryGradient
		{
			get => _secondaryGradient;
			set => SetProperty(ref _secondaryGradient, value);
		}

		#region Public Methods - Secondary Data

		/// <summary>
		/// Adds a new secondary data point (for bidirectional mode).
		/// If the maximum number of points is exceeded, the oldest point is removed.
		/// </summary>
		public void AddSecondaryDataPoint(double value)
		{
			lock (_dataLock)
			{
				_secondaryDataPoints.Add(value);
				TrimSecondaryDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Clears all secondary data points from the graph.
		/// </summary>
		public void ClearSecondaryDataPoints()
		{
			lock (_dataLock)
			{
				_secondaryDataPoints.Clear();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the secondary data points for bidirectional mode, replacing any existing points.
		/// </summary>
		public void SetSecondaryDataPoints(IEnumerable<double> dataPoints)
		{
			lock (_dataLock)
			{
				_secondaryDataPoints = new List<double>(dataPoints);
				TrimSecondaryDataPoints();
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets both primary and secondary data points at once (for bidirectional mode).
		/// More efficient than calling SetDataPoints and SetSecondaryDataPoints separately.
		/// </summary>
		public void SetBidirectionalData(IEnumerable<double> primaryData, IEnumerable<double> secondaryData)
		{
			lock (_dataLock)
			{
				_dataPoints = new List<double>(primaryData);
				_secondaryDataPoints = new List<double>(secondaryData);
				TrimDataPoints();
				TrimSecondaryDataPoints();
			}
			Container?.Invalidate(true);
		}

		#endregion
	}
}
