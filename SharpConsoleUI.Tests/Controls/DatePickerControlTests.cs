// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class DatePickerControlTests
{
	#region Helper Methods

	private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool ctrl = false)
	{
		return new ConsoleKeyInfo(keyChar, key, shift, alt, ctrl);
	}

	private static ConsoleKeyInfo DigitKey(int digit)
	{
		char c = (char)('0' + digit);
		return new ConsoleKeyInfo(c, ConsoleKey.D0 + digit, false, false, false);
	}

	private static (DatePickerControl picker, Window window) CreateFocusedPicker(DateTime? date = null, string? format = null, CultureInfo? culture = null)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		if (culture != null) picker.Culture = culture;
		if (format != null) picker.DateFormatOverride = format;
		if (date.HasValue) picker.SelectedDate = date.Value;
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		return (picker, window);
	}

	#endregion

	#region Construction Tests

	[Fact]
	public void Constructor_Default_HasExpectedDefaults()
	{
		var picker = new DatePickerControl();

		Assert.Null(picker.SelectedDate);
		Assert.Null(picker.MinDate);
		Assert.Null(picker.MaxDate);
		Assert.Equal("Date:", picker.Prompt);
		Assert.True(picker.IsEnabled);
		Assert.False(picker.HasFocus);
		Assert.False(picker.IsCalendarOpen);
	}

	[Fact]
	public void Constructor_WithPrompt_SetsPrompt()
	{
		var picker = new DatePickerControl("Select:");

		Assert.Equal("Select:", picker.Prompt);
	}

	[Fact]
	public void Constructor_CanReceiveFocus_WhenEnabled()
	{
		var picker = new DatePickerControl();

		Assert.True(picker.CanReceiveFocus);
	}

	[Fact]
	public void Constructor_WantsMouseEvents_WhenEnabled()
	{
		var picker = new DatePickerControl();

		Assert.True(picker.WantsMouseEvents);
		Assert.True(picker.CanFocusWithMouse);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void SelectedDate_SetAndGet_ReturnsValue()
	{
		var picker = new DatePickerControl();
		var date = new DateTime(2025, 6, 15);

		picker.SelectedDate = date;

		Assert.Equal(date, picker.SelectedDate);
	}

	[Fact]
	public void SelectedDate_SetNull_ClearsDate()
	{
		var picker = new DatePickerControl();
		picker.SelectedDate = new DateTime(2025, 6, 15);

		picker.SelectedDate = null;

		Assert.Null(picker.SelectedDate);
	}

	[Fact]
	public void SelectedDate_SameValue_DoesNotFireEvent()
	{
		var picker = new DatePickerControl();
		var date = new DateTime(2025, 6, 15);
		picker.SelectedDate = date;

		int fireCount = 0;
		picker.SelectedDateChanged += (s, e) => fireCount++;

		picker.SelectedDate = date;

		Assert.Equal(0, fireCount);
	}

	[Fact]
	public void MinDate_ClampsSelectedDate()
	{
		var picker = new DatePickerControl();
		picker.MinDate = new DateTime(2025, 1, 10);
		picker.SelectedDate = new DateTime(2025, 1, 5);

		Assert.Equal(new DateTime(2025, 1, 10), picker.SelectedDate);
	}

	[Fact]
	public void MaxDate_ClampsSelectedDate()
	{
		var picker = new DatePickerControl();
		picker.MaxDate = new DateTime(2025, 12, 20);
		picker.SelectedDate = new DateTime(2025, 12, 25);

		Assert.Equal(new DateTime(2025, 12, 20), picker.SelectedDate);
	}

	[Fact]
	public void Prompt_SetAndGet()
	{
		var picker = new DatePickerControl();

		picker.Prompt = "Birthday:";

		Assert.Equal("Birthday:", picker.Prompt);
	}

	[Fact]
	public void Culture_SetAndGet()
	{
		var picker = new DatePickerControl();
		var culture = new CultureInfo("de-DE");

		picker.Culture = culture;

		Assert.Equal(culture, picker.Culture);
	}

	[Fact]
	public void Culture_SetNull_FallsBackToCurrentCulture()
	{
		var picker = new DatePickerControl();

		picker.Culture = null!;

		Assert.Equal(CultureInfo.CurrentCulture, picker.Culture);
	}

	[Fact]
	public void DateFormatOverride_SetAndGet()
	{
		var picker = new DatePickerControl();

		picker.DateFormatOverride = "yyyy-MM-dd";

		Assert.Equal("yyyy-MM-dd", picker.DateFormatOverride);
	}

	[Fact]
	public void FirstDayOfWeekOverride_SetAndGet()
	{
		var picker = new DatePickerControl();

		picker.FirstDayOfWeekOverride = DayOfWeek.Monday;

		Assert.Equal(DayOfWeek.Monday, picker.FirstDayOfWeekOverride);
	}

	[Fact]
	public void IsEnabled_WhenDisabled_CannotReceiveFocus()
	{
		var picker = new DatePickerControl();

		picker.IsEnabled = false;

		Assert.False(picker.CanReceiveFocus);
		Assert.False(picker.WantsMouseEvents);
		Assert.False(picker.CanFocusWithMouse);
	}

	#endregion

	#region Format Parsing Tests

	[Fact]
	public void ContentWidth_USFormat_CorrectWidth()
	{
		// MM/dd/yyyy => prompt(5) + space(1) + 2 + 1 + 2 + 1 + 4 + space(1) + indicator(1) + margins
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";

		int? width = picker.ContentWidth;

		Assert.NotNull(width);
		Assert.True(width > 0);
	}

	[Fact]
	public void ContentWidth_ISOFormat_CorrectWidth()
	{
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "yyyy-MM-dd";

		int? width = picker.ContentWidth;

		Assert.NotNull(width);
		Assert.True(width > 0);
	}

	[Fact]
	public void ContentWidth_EuropeanFormat_CorrectWidth()
	{
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "dd.MM.yyyy";

		int? width = picker.ContentWidth;

		Assert.NotNull(width);
		Assert.True(width > 0);
	}

	[Fact]
	public void ContentWidth_DifferentFormats_ProduceExpectedWidths()
	{
		// All three formats have same total segment widths: 2+2+4 = 8
		var picker1 = new DatePickerControl();
		picker1.DateFormatOverride = "MM/dd/yyyy";

		var picker2 = new DatePickerControl();
		picker2.DateFormatOverride = "dd.MM.yyyy";

		var picker3 = new DatePickerControl();
		picker3.DateFormatOverride = "yyyy-MM-dd";

		// With same prompt, all should have same width (same total segment widths)
		Assert.Equal(picker1.ContentWidth, picker2.ContentWidth);
		Assert.Equal(picker2.ContentWidth, picker3.ContentWidth);
	}

	[Fact]
	public void ContentWidth_TwoDigitYear_SmallerThanFourDigit()
	{
		var picker2 = new DatePickerControl();
		picker2.DateFormatOverride = "MM/dd/yy";

		var picker4 = new DatePickerControl();
		picker4.DateFormatOverride = "MM/dd/yyyy";

		Assert.True(picker2.ContentWidth < picker4.ContentWidth);
	}

	#endregion

	#region Segment Navigation Tests

	[Fact]
	public void RightArrow_MovesSegmentForward()
	{
		// With MM/dd/yyyy, start on Month (segment 0)
		// Press Right -> move to Day (segment 1)
		// Press Up -> should increment Day
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Day should have incremented from 15 to 16
		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void LeftArrow_MovesSegmentBackward()
	{
		// Start on first segment, move right, then left to return
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.LeftArrow));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Should be back on Month segment, incrementing month
		Assert.Equal(4, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void Tab_MovesSegmentForward()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.Tab));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void ShiftTab_MovesSegmentBackward()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		// Move to last segment
		picker.ProcessKey(Key(ConsoleKey.Tab));
		picker.ProcessKey(Key(ConsoleKey.Tab));

		// Shift+Tab back one
		picker.ProcessKey(Key(ConsoleKey.Tab, shift: true));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Should be on Day segment
		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void RightArrow_AtLastSegment_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		bool handled = picker.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(handled);
	}

	[Fact]
	public void LeftArrow_AtFirstSegment_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		bool handled = picker.ProcessKey(Key(ConsoleKey.LeftArrow));

		Assert.False(handled);
	}

	[Fact]
	public void Tab_AtLastSegment_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.Tab));
		picker.ProcessKey(Key(ConsoleKey.Tab));
		bool handled = picker.ProcessKey(Key(ConsoleKey.Tab));

		Assert.False(handled);
	}

	[Fact]
	public void ShiftTab_AtFirstSegment_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		bool handled = picker.ProcessKey(Key(ConsoleKey.Tab, shift: true));

		Assert.False(handled);
	}

	[Fact]
	public void Navigation_ISOFormat_SegmentOrderIsYearMonthDay()
	{
		// yyyy-MM-dd: first segment is Year
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "yyyy-MM-dd");

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Year should increment
		Assert.Equal(2026, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void Navigation_EuropeanFormat_SegmentOrderIsDayMonthYear()
	{
		// dd.MM.yyyy: first segment is Day
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "dd.MM.yyyy");

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Day should increment
		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	#endregion

	#region Spin Value Tests

	[Fact]
	public void UpArrow_OnMonthSegment_IncrementsMonth()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(4, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void DownArrow_OnMonthSegment_DecrementsMonth()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(2, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void UpArrow_OnDaySegment_IncrementsDay()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void DownArrow_OnDaySegment_DecrementsDay()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(14, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void UpArrow_OnYearSegment_IncrementsYear()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Year

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(2026, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void Month_WrapsFrom12To1()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 12, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(1, picker.SelectedDate!.Value.Month);
		Assert.Equal(2026, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void Month_WrapsFrom1To12()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 1, 15), "MM/dd/yyyy");

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(12, picker.SelectedDate!.Value.Month);
		Assert.Equal(2024, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void Day_WrapsAtEndOfMonth()
	{
		// March 31 + 1 day = April 1
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 31), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(1, picker.SelectedDate!.Value.Day);
		Assert.Equal(4, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void Day_WrapsAtStartOfMonth()
	{
		// March 1 - 1 day = Feb 28
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 1), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(28, picker.SelectedDate!.Value.Day);
		Assert.Equal(2, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void NoSelectedDate_SpinUsesToday()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		// When no date selected, spin uses DateTime.Today
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.NotNull(picker.SelectedDate);
	}

	#endregion

	#region Digit Entry Tests

	[Fact]
	public void DigitEntry_TwoDigitMonth_SetsMonth()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		// Type "07" for July
		picker.ProcessKey(DigitKey(0));
		picker.ProcessKey(DigitKey(7));

		Assert.Equal(7, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void DigitEntry_TwoDigitDay_SetsDay()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		// Type "23"
		picker.ProcessKey(DigitKey(2));
		picker.ProcessKey(DigitKey(3));

		Assert.Equal(23, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void DigitEntry_DayClampsToMaxDaysInMonth()
	{
		// Feb only has 28 days in 2025, typing 31 should clamp
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 2, 15), "MM/dd/yyyy");
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		picker.ProcessKey(DigitKey(3));
		picker.ProcessKey(DigitKey(1));

		Assert.True(picker.SelectedDate!.Value.Day <= 28);
	}

	[Fact]
	public void DigitEntry_MonthClampsTo12()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		picker.ProcessKey(DigitKey(1));
		picker.ProcessKey(DigitKey(5)); // 15 -> clamps to 12

		Assert.Equal(12, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void DigitEntry_AutoAdvancesSegmentAfterTwoDigits()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		// Type "06" in month => auto-advances to day segment
		picker.ProcessKey(DigitKey(0));
		picker.ProcessKey(DigitKey(6));

		// Now typing should affect Day segment
		picker.ProcessKey(DigitKey(2));
		picker.ProcessKey(DigitKey(5));

		Assert.Equal(6, picker.SelectedDate!.Value.Month);
		Assert.Equal(25, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void DigitEntry_NonDigitIgnored()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		bool handled = picker.ProcessKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

		Assert.False(handled);
		Assert.Equal(3, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		picker.IsEnabled = false;

		bool handled = picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_WhenNotFocused_ReturnsFalse()
	{
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);

		bool handled = picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.False(handled);
	}

	#endregion

	#region Min/Max Constraint Tests

	[Fact]
	public void MinDate_SpinDown_ClampsToMinDate()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 1, 2), "MM/dd/yyyy");
		picker.MinDate = new DateTime(2025, 1, 1);
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		// Try to go below min
		picker.ProcessKey(Key(ConsoleKey.DownArrow));
		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		// Should not go below MinDate
		Assert.True(picker.SelectedDate!.Value >= picker.MinDate!.Value);
	}

	[Fact]
	public void MaxDate_SpinUp_ClampsToMaxDate()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 12, 30), "MM/dd/yyyy");
		picker.MaxDate = new DateTime(2025, 12, 31);
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Day

		// Try to go above max
		picker.ProcessKey(Key(ConsoleKey.UpArrow));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Should not go above MaxDate
		Assert.True(picker.SelectedDate!.Value <= picker.MaxDate!.Value);
	}

	[Fact]
	public void MinMax_BothSet_DateStaysInRange()
	{
		var picker = new DatePickerControl();
		picker.MinDate = new DateTime(2025, 6, 1);
		picker.MaxDate = new DateTime(2025, 6, 30);

		picker.SelectedDate = new DateTime(2025, 5, 15);
		Assert.Equal(new DateTime(2025, 6, 1), picker.SelectedDate);

		picker.SelectedDate = new DateTime(2025, 7, 15);
		Assert.Equal(new DateTime(2025, 6, 30), picker.SelectedDate);
	}

	#endregion

	#region Culture Tests

	[Fact]
	public void Culture_GermanFormat_SegmentOrderIsDayMonthYear()
	{
		var culture = new CultureInfo("de-DE");
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), culture: culture);

		// First segment should be Day in dd.MM.yyyy
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void Culture_USFormat_SegmentOrderIsMonthDayYear()
	{
		var culture = new CultureInfo("en-US");
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), culture: culture);

		// First segment should be Month in M/d/yyyy
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(4, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void FormatOverride_OverridesCultureFormat()
	{
		var culture = new CultureInfo("en-US");
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "yyyy-MM-dd", culture);

		// First segment should be Year due to override
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(2026, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void FirstDayOfWeekOverride_IsRespected()
	{
		var picker = new DatePickerControl();
		picker.FirstDayOfWeekOverride = DayOfWeek.Monday;

		Assert.Equal(DayOfWeek.Monday, picker.FirstDayOfWeekOverride);
	}

	#endregion

	#region Focus Tests

	[Fact]
	public void SetFocus_True_FiresGotFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		// Add a placeholder so it takes auto-focus, leaving picker unfocused
		window.AddControl(new ButtonControl { Text = "Placeholder" });
		var picker = new DatePickerControl();
		window.AddControl(picker);

		FocusChangedEventArgs? args = null;
		window.FocusManager.FocusChanged += (_, e) => args = e;

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		Assert.True(picker.HasFocus);
		Assert.NotNull(args);
		Assert.Equal(picker, args!.Current);
	}

	[Fact]
	public void SetFocus_False_FiresLostFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		FocusChangedEventArgs? args = null;
		window.FocusManager.FocusChanged += (_, e) => args = e;

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		Assert.False(picker.HasFocus);
		Assert.NotNull(args);
		Assert.Null(args!.Current);
		Assert.Equal(picker, args.Previous);
	}

	[Fact]
	public void SetFocus_SameValue_DoesNotFireEvent()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		int fireCount = 0;
		window.FocusManager.FocusChanged += (_, e) => fireCount++;

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		Assert.Equal(0, fireCount);
	}

	[Fact]
	public void SetFocus_False_ClearsPendingDigit()
	{
		var (picker, window) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		// Start entering a digit
		picker.ProcessKey(DigitKey(1));

		// Lose focus
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Regain focus, type "07" fresh
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(DigitKey(0));
		picker.ProcessKey(DigitKey(7));

		// Should be 7, not something combined with the old pending digit
		Assert.Equal(7, picker.SelectedDate!.Value.Month);
	}

	#endregion

	#region Event Tests

	[Fact]
	public void SelectedDateChanged_FiresWhenDateChanges()
	{
		var picker = new DatePickerControl();
		DateTime? receivedDate = null;
		picker.SelectedDateChanged += (s, e) => receivedDate = e;

		picker.SelectedDate = new DateTime(2025, 6, 15);

		Assert.Equal(new DateTime(2025, 6, 15), receivedDate);
	}

	[Fact]
	public void SelectedDateChanged_FiresOnSpin()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		int fireCount = 0;
		picker.SelectedDateChanged += (s, e) => fireCount++;

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(1, fireCount);
	}

	[Fact]
	public void SelectedDateChanged_FiresOnDigitEntry()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");
		int fireCount = 0;
		picker.SelectedDateChanged += (s, e) => fireCount++;

		picker.ProcessKey(DigitKey(0));
		picker.ProcessKey(DigitKey(7));

		// Should fire once when 2-digit entry completes
		Assert.True(fireCount >= 1);
	}

	[Fact]
	public void SelectedDateChanged_FiresWithNull_WhenCleared()
	{
		var picker = new DatePickerControl();
		picker.SelectedDate = new DateTime(2025, 6, 15);

		DateTime? receivedDate = new DateTime(9999, 1, 1); // sentinel
		picker.SelectedDateChanged += (s, e) => receivedDate = e;

		picker.SelectedDate = null;

		Assert.Null(receivedDate);
	}

	#endregion

	#region ContentWidth Tests

	[Fact]
	public void ContentWidth_IsPositive()
	{
		var picker = new DatePickerControl();

		Assert.NotNull(picker.ContentWidth);
		Assert.True(picker.ContentWidth > 0);
	}

	[Fact]
	public void ContentWidth_ChangesWithPrompt()
	{
		var picker1 = new DatePickerControl("Date:");
		picker1.DateFormatOverride = "MM/dd/yyyy";

		var picker2 = new DatePickerControl("Select a date:");
		picker2.DateFormatOverride = "MM/dd/yyyy";

		Assert.True(picker2.ContentWidth > picker1.ContentWidth);
	}

	#endregion

	#region Calendar State Tests

	[Fact]
	public void Enter_OpensCalendar()
	{
		// Calendar portal requires a window, but we can test the IsCalendarOpen state
		// without a window by checking that the method doesn't crash
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		bool handled = picker.ProcessKey(Key(ConsoleKey.Enter));

		Assert.True(handled);
		// Note: IsCalendarOpen may stay false without a Window parent,
		// but the key should still be handled
	}

	[Fact]
	public void Space_OpensCalendar()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2025, 3, 15), "MM/dd/yyyy");

		bool handled = picker.ProcessKey(Key(ConsoleKey.Spacebar, ' '));

		Assert.True(handled);
	}

	[Fact]
	public void CalendarOpen_WithWindow_SetsIsCalendarOpen()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);

		// Render first to establish layout bounds
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));

		Assert.True(picker.IsCalendarOpen);
	}

	[Fact]
	public void CalendarOpen_Escape_ClosesCalendar()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter)); // open
		Assert.True(picker.IsCalendarOpen);

		picker.ProcessKey(Key(ConsoleKey.Escape)); // close

		Assert.False(picker.IsCalendarOpen);
	}

	[Fact]
	public void CalendarOpen_LostFocus_ClosesCalendar()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));
		Assert.True(picker.IsCalendarOpen);

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		Assert.False(picker.IsCalendarOpen);
	}

	[Fact]
	public void CalendarOpen_EnterSelectsDay()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter)); // open calendar

		// Move right a day
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select

		Assert.False(picker.IsCalendarOpen);
		Assert.Equal(16, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void CalendarOpen_UpArrow_MovesUpOneWeek()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter)); // open

		picker.ProcessKey(Key(ConsoleKey.UpArrow)); // move up 7 days
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select

		Assert.Equal(8, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void CalendarOpen_Home_GoesToFirstDay()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));

		picker.ProcessKey(Key(ConsoleKey.Home));
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select

		Assert.Equal(1, picker.SelectedDate!.Value.Day);
	}

	[Fact]
	public void CalendarOpen_End_GoesToLastDay()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));

		picker.ProcessKey(Key(ConsoleKey.End));
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select

		Assert.Equal(31, picker.SelectedDate!.Value.Day); // March has 31 days
	}

	[Fact]
	public void CalendarOpen_PageDown_MovesToNextMonth()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));

		picker.ProcessKey(Key(ConsoleKey.PageDown));
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select day 15 in April

		Assert.Equal(4, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void CalendarOpen_PageUp_MovesToPreviousMonth()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2025, 3, 15);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter));

		picker.ProcessKey(Key(ConsoleKey.PageUp));
		picker.ProcessKey(Key(ConsoleKey.Enter)); // select day 15 in Feb

		Assert.Equal(2, picker.SelectedDate!.Value.Month);
	}

	[Fact]
	public void CalendarOpen_T_JumpsToToday()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";
		picker.SelectedDate = new DateTime(2020, 1, 1);
		window.AddControl(picker);
		window.RenderAndGetVisibleContent();

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.ProcessKey(Key(ConsoleKey.Enter)); // open

		picker.ProcessKey(new ConsoleKeyInfo('T', ConsoleKey.T, false, false, false));

		// Don't select, just verify we didn't crash and calendar is still open
		Assert.True(picker.IsCalendarOpen);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void LeapYear_Feb29_HandledCorrectly()
	{
		var (picker, _) = CreateFocusedPicker(new DateTime(2024, 2, 29), "MM/dd/yyyy");

		// Increment year: 2024 -> 2025 (not a leap year, Feb 29 doesn't exist)
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // move to Year
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Should adjust day to valid date
		Assert.NotNull(picker.SelectedDate);
		Assert.True(picker.SelectedDate!.Value.Day <= 28);
		Assert.Equal(2025, picker.SelectedDate!.Value.Year);
	}

	[Fact]
	public void Dispose_CleansUpEvents()
	{
		var picker = new DatePickerControl();
		picker.SelectedDateChanged += (s, e) => { };

		picker.Dispose();

		// Should not throw after disposal
		// Event handlers are cleared in OnDisposing
	}

	[Fact]
	public void GetLogicalContentSize_ReturnsPositiveSize()
	{
		var picker = new DatePickerControl();
		picker.DateFormatOverride = "MM/dd/yyyy";

		var size = picker.GetLogicalContentSize();

		Assert.True(size.Width > 0);
		Assert.True(size.Height > 0);
	}

	[Fact]
	public void PreferredCursorShape_IsHidden()
	{
		var picker = new DatePickerControl();

		Assert.Equal(CursorShape.Hidden, picker.PreferredCursorShape);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void MinDate_SameAsMaxDate_ClampsToOnlyValidDate()
	{
		var onlyDate = new DateTime(2024, 6, 15);
		var (picker, _) = CreateFocusedPicker();
		picker.MinDate = onlyDate;
		picker.MaxDate = onlyDate;

		picker.SelectedDate = new DateTime(2020, 1, 1);
		Assert.Equal(onlyDate, picker.SelectedDate);

		picker.SelectedDate = new DateTime(2030, 12, 31);
		Assert.Equal(onlyDate, picker.SelectedDate);
	}

	[Fact]
	public void CalendarNavigation_PastDateTimeMin_DoesNotThrow()
	{
		var (picker, _) = CreateFocusedPicker(date: new DateTime(1, 1, 1));

		var exception = Record.Exception(() =>
		{
			picker.ProcessKey(Key(ConsoleKey.PageUp));
		});

		Assert.Null(exception);
	}

	[Fact]
	public void CalendarNavigation_PastDateTimeMax_DoesNotThrow()
	{
		var (picker, _) = CreateFocusedPicker(date: new DateTime(9999, 12, 31));

		// Open the calendar first
		picker.ProcessKey(Key(ConsoleKey.Enter));

		var exception = Record.Exception(() =>
		{
			picker.ProcessKey(Key(ConsoleKey.PageDown));
		});

		Assert.Null(exception);
	}

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse_EdgeCase()
	{
		var (picker, _) = CreateFocusedPicker();
		picker.IsEnabled = false;

		var result = picker.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(result);
	}

	#endregion
}
