// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	public class ChartDataTests
	{
		[Fact]
		public void SetValuesSpacesPointsEvenly()
		{
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetValues(new[] { 10d, 20, 30 });

			Assert.Equal(new[] { 0d, 1, 2 }, s.Points.Select(p => p.X));
			Assert.Equal(new[] { 10d, 20, 30 }, s.Points.Select(p => p.Y));
		}

		[Fact]
		public void SetCategoriesIndexesByPositionAndKeepsTheLabel()
		{
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetCategories(new[] { ("cpu", 40d), ("gpu", 70d) });

			Assert.Equal(0, s.Points[0].X);
			Assert.Equal("cpu", s.Points[0].Label);
			Assert.Equal(1, s.Points[1].X);
			Assert.Equal("gpu", s.Points[1].Label);
		}

		[Fact]
		public void SetTimeSeriesStoresTicksSoOneAxisEngineServesBoth()
		{
			var when = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetTimeSeries(new[] { (when, 5d) });

			Assert.Equal(when.Ticks, s.Points[0].X);
		}

		[Fact]
		public void NonFiniteValuesAreDropped()
		{
			// A real metrics feed produces these. Letting one through poisons every
			// axis bound computed from the data, so they are dropped at the door.
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetValues(new[] { 1d, double.NaN, 3, double.PositiveInfinity, 5 });

			Assert.Equal(new[] { 1d, 3, 5 }, s.Points.Select(p => p.Y));
		}

		[Fact]
		public void AnEmptySeriesIsLegal()
		{
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetValues(Array.Empty<double>());

			Assert.Empty(s.Points);
		}

		[Fact]
		public void AddPointAppendsForStreaming()
		{
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetValues(new[] { 1d });
			s.AddPoint(new ChartPoint(5, 9));

			Assert.Equal(2, s.Points.Count);
			Assert.Equal(9, s.Points[1].Y);
		}

		[Fact]
		public void TrimToKeepsTheMostRecentPoints()
		{
			// A live feed appends forever; without this the series is a memory leak.
			var s = new ChartSeries { Name = "a", Color = Color.Red };
			s.SetValues(new[] { 1d, 2, 3, 4, 5 });

			s.TrimTo(2);

			Assert.Equal(new[] { 4d, 5 }, s.Points.Select(p => p.Y));
		}
	}
}
