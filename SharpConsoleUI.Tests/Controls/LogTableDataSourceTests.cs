// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Logging;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class LogTableDataSourceTests
{
	[Fact]
	public void SeverityColors_MapEachLevelToDistinctSensibleColor()
	{
		Assert.Equal(Color.Yellow, LogSeverityColors.ForLevel(LogLevel.Warning));
		Assert.Equal(Color.Red, LogSeverityColors.ForLevel(LogLevel.Error));
		Assert.Equal(Color.Red, LogSeverityColors.ForLevel(LogLevel.Critical));
		Assert.Equal(Color.Grey, LogSeverityColors.ForLevel(LogLevel.Trace));
		Assert.Equal(Color.Grey, LogSeverityColors.ForLevel(LogLevel.Debug));
	}

	private static ILogService MakeService(int count, LogLevel min = LogLevel.Trace)
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = min;
		for (int i = 0; i < count; i++)
			system.LogService.Log(LogLevel.Information, $"line {i}", "Cat");
		return system.LogService;
	}

	[Fact]
	public void DataSource_ProjectsFourColumns_WithHeaders()
	{
		var ds = new LogTableDataSource(MakeService(3));
		Assert.Equal(4, ds.ColumnCount);
		Assert.Equal(3, ds.RowCount);
		Assert.Equal("Time", ds.GetColumnHeader(0));
		Assert.Equal("Level", ds.GetColumnHeader(1));
		Assert.Equal("Category", ds.GetColumnHeader(2));
		Assert.Equal("Message", ds.GetColumnHeader(3));
	}

	[Fact]
	public void DataSource_GetCellValue_ReturnsEntryFields()
	{
		var ds = new LogTableDataSource(MakeService(1));
		Assert.Equal("INFO", ds.GetCellValue(0, 1).Trim().ToUpperInvariant()[..4] == "INFO" ? "INFO" : ds.GetCellValue(0, 1));
		Assert.Equal("Cat", ds.GetCellValue(0, 2));
		Assert.Equal("line 0", ds.GetCellValue(0, 3));
	}

	[Fact]
	public void DataSource_RowForegroundColor_FollowsSeverity()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.Log(LogLevel.Error, "boom", "Cat");
		var ds = new LogTableDataSource(system.LogService);
		Assert.Equal(Color.Red, ds.GetRowForegroundColor(0));
	}

	[Fact]
	public void DataSource_ViewFilterLevel_HidesLowerLevelsWithoutDiscarding()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.Log(LogLevel.Debug, "dbg", "Cat");
		system.LogService.Log(LogLevel.Error, "err", "Cat");
		var ds = new LogTableDataSource(system.LogService);
		Assert.Equal(2, ds.RowCount);

		ds.ViewFilterLevel = LogLevel.Error;
		Assert.Equal(1, ds.RowCount);
		Assert.Equal("err", ds.GetCellValue(0, 3));

		ds.ViewFilterLevel = LogLevel.Trace; // restored, not discarded
		Assert.Equal(2, ds.RowCount);
	}

	[Fact]
	public void DataSource_SearchText_FiltersMessageSubstring()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.Log(LogLevel.Information, "apple", "Cat");
		system.LogService.Log(LogLevel.Information, "banana", "Cat");
		var ds = new LogTableDataSource(system.LogService);

		ds.SearchText = "ban";
		Assert.Equal(1, ds.RowCount);
		Assert.Equal("banana", ds.GetCellValue(0, 3));
	}

	[Fact]
	public void DataSource_NewMatchingEntry_AppendsAndRaisesAdd()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		var ds = new LogTableDataSource(system.LogService);
		ds.AttachSystem(system); // no window loop in test -> EnqueueOnUIThread runs inline path

		NotifyCollectionChangedAction? action = null;
		ds.CollectionChanged += (_, e) => action = e.Action;

		system.LogService.Log(LogLevel.Information, "fresh", "Cat");
		system.DrainUiThreadQueueForTests(); // pump marshalled continuation

		Assert.Equal(1, ds.RowCount);
		Assert.Equal("fresh", ds.GetCellValue(0, 3));
		Assert.Equal(NotifyCollectionChangedAction.Add, action);
	}

	[Fact]
	public void DataSource_Clear_EmptiesAndRaisesReset()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.Log(LogLevel.Information, "x", "Cat");
		var ds = new LogTableDataSource(system.LogService);
		ds.AttachSystem(system);

		system.LogService.ClearLogs();
		system.DrainUiThreadQueueForTests();

		Assert.Equal(0, ds.RowCount);
	}

	[Fact]
	public void DataSource_ExceedingMaxBuffer_TrimsOldest()
	{
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.MaxBufferSize = 3;
		var ds = new LogTableDataSource(system.LogService);
		ds.AttachSystem(system);

		for (int i = 0; i < 5; i++)
		{
			system.LogService.Log(LogLevel.Information, $"m{i}", "Cat");
			system.DrainUiThreadQueueForTests();
		}

		Assert.Equal(3, ds.RowCount);
		Assert.Equal("m2", ds.GetCellValue(0, 3));
		Assert.Equal("m4", ds.GetCellValue(2, 3));
	}

	[Fact]
	public void DataSource_RestrictiveViewFilter_StaysBoundedAndSubsetOfSource()
	{
		// Under a restrictive view filter, _displayed stays small — the naive "trim only when
		// _displayed exceeds MaxBufferSize" never fired, so evicted-from-source entries lingered and
		// the projection could grow unbounded / hold stale rows. Reconciliation must keep _displayed a
		// true subset of the live source buffer and never exceed MaxBufferSize.
		var driver = new SharpConsoleUI.Drivers.HeadlessConsoleDriver(120, 30);
		var system = new ConsoleWindowSystem(driver);
		system.LogService.MinimumLevel = LogLevel.Trace;
		system.LogService.MaxBufferSize = 3;
		var ds = new LogTableDataSource(system.LogService);
		ds.ViewFilterLevel = LogLevel.Error; // only Errors are shown
		ds.AttachSystem(system);

		// Interleave many Info (filtered out) with a few Error, well beyond the 3-entry buffer.
		int errorCount = 0;
		for (int i = 0; i < 12; i++)
		{
			bool isError = (i % 4) == 3; // errors at i = 3, 7, 11
			if (isError)
			{
				errorCount++;
				system.LogService.Log(LogLevel.Error, $"err{errorCount}", "Cat");
			}
			else
			{
				system.LogService.Log(LogLevel.Information, $"info{i}", "Cat");
			}
			system.DrainUiThreadQueueForTests();

			// Projection is always bounded by the source buffer, never grows unbounded.
			Assert.True(ds.RowCount <= system.LogService.MaxBufferSize);
		}

		// The source buffer holds only the last 3 entries (info9, info10, err3). Only err3 passes the
		// Error view filter, so the projection must reflect exactly that surviving Error — not any
		// earlier evicted err1/err2.
		Assert.True(ds.RowCount <= system.LogService.MaxBufferSize);
		var displayed = ds.DisplayedEntries;
		Assert.All(displayed, e => Assert.True(e.Level >= LogLevel.Error));
		var sourceMessages = system.LogService.GetAllLogs().Select(x => x.Message).ToHashSet();
		Assert.All(displayed, e => Assert.Contains(e.Message, sourceMessages));
		Assert.Single(displayed);
		Assert.Equal("err3", displayed[0].Message);
	}
}
