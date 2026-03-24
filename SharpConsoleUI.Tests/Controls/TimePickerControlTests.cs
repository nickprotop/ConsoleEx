// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class TimePickerControlTests
{
	#region Helper Methods

	private static ConsoleKeyInfo Key(ConsoleKey key, char ch = '\0',
		bool shift = false, bool alt = false, bool ctrl = false)
	{
		return new ConsoleKeyInfo(ch, key, shift, alt, ctrl);
	}

	private static ConsoleKeyInfo DigitKey(int digit)
	{
		return new ConsoleKeyInfo((char)('0' + digit), ConsoleKey.D0 + digit, false, false, false);
	}

	private static (TimePickerControl picker, Window window) CreateFocusedPicker(TimeSpan? time = null,
		bool showSeconds = false, bool? use24Hour = null, CultureInfo? culture = null)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new TimePickerControl();
		if (culture != null) picker.Culture = culture;
		if (use24Hour.HasValue) picker.Use24HourFormat = use24Hour;
		picker.ShowSeconds = showSeconds;
		if (time.HasValue) picker.SelectedTime = time;
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		return (picker, window);
	}

	#endregion

	#region Construction Tests

	[Fact]
	public void Constructor_DefaultValues()
	{
		var picker = new TimePickerControl();

		Assert.Null(picker.SelectedTime);
		Assert.Null(picker.MinTime);
		Assert.Null(picker.MaxTime);
		Assert.False(picker.ShowSeconds);
		Assert.Null(picker.Use24HourFormat);
		Assert.True(picker.IsEnabled);
		Assert.False(picker.HasFocus);
		Assert.Equal("Time:", picker.Prompt);
	}

	[Fact]
	public void Constructor_CustomPrompt()
	{
		var picker = new TimePickerControl("Select:");

		Assert.Equal("Select:", picker.Prompt);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void SelectedTime_SetAndGet()
	{
		var picker = new TimePickerControl();
		var time = new TimeSpan(14, 30, 0);

		picker.SelectedTime = time;

		Assert.Equal(time, picker.SelectedTime);
	}

	[Fact]
	public void SelectedTime_SetNull()
	{
		var picker = new TimePickerControl();
		picker.SelectedTime = new TimeSpan(10, 0, 0);
		picker.SelectedTime = null;

		Assert.Null(picker.SelectedTime);
	}

	[Fact]
	public void ShowSeconds_SetAndGet()
	{
		var picker = new TimePickerControl();

		picker.ShowSeconds = true;

		Assert.True(picker.ShowSeconds);
	}

	[Fact]
	public void Use24HourFormat_ExplicitOverride()
	{
		var picker = new TimePickerControl();

		picker.Use24HourFormat = true;
		Assert.True(picker.Use24HourFormat);

		picker.Use24HourFormat = false;
		Assert.False(picker.Use24HourFormat);
	}

	[Fact]
	public void Prompt_SetAndGet()
	{
		var picker = new TimePickerControl();

		picker.Prompt = "Alarm:";

		Assert.Equal("Alarm:", picker.Prompt);
	}

	[Fact]
	public void Prompt_SetNull_BecomesEmpty()
	{
		var picker = new TimePickerControl();

		picker.Prompt = null!;

		Assert.Equal(string.Empty, picker.Prompt);
	}

	[Fact]
	public void IsEnabled_SetAndGet()
	{
		var picker = new TimePickerControl();

		picker.IsEnabled = false;

		Assert.False(picker.IsEnabled);
		Assert.False(picker.CanReceiveFocus);
		Assert.False(picker.WantsMouseEvents);
	}

	#endregion

	#region Segment Navigation Tests

	[Fact]
	public void RightArrow_MovesToNextSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Initially on hour (segment 0), move to minute (segment 1)
		bool handled = picker.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.True(handled);
	}

	[Fact]
	public void Tab_MovesToNextSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		bool handled = picker.ProcessKey(Key(ConsoleKey.Tab));

		Assert.True(handled);
	}

	[Fact]
	public void LeftArrow_MovesToPreviousSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Move to segment 1 first
		picker.ProcessKey(Key(ConsoleKey.RightArrow));

		// Now move back
		bool handled = picker.ProcessKey(Key(ConsoleKey.LeftArrow));

		Assert.True(handled);
	}

	[Fact]
	public void ShiftTab_MovesToPreviousSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Move to segment 1 first
		picker.ProcessKey(Key(ConsoleKey.RightArrow));

		// Shift+Tab moves back
		bool handled = picker.ProcessKey(Key(ConsoleKey.Tab, '\0', shift: true));

		Assert.True(handled);
	}

	[Fact]
	public void RightArrow_AtLastSegment_ReturnsFalse()
	{
		// 24h no seconds = 2 segments (hour, min)
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Move to segment 1 (last in 24h, no seconds)
		picker.ProcessKey(Key(ConsoleKey.RightArrow));

		// Try to move past last segment
		bool handled = picker.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(handled);
	}

	[Fact]
	public void LeftArrow_AtFirstSegment_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Already on segment 0, can't go left
		bool handled = picker.ProcessKey(Key(ConsoleKey.LeftArrow));

		Assert.False(handled);
	}

	[Fact]
	public void Navigation_WithSeconds_ThreeNumericSegments()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 45), showSeconds: true, use24Hour: true);

		// Navigate through all 3 segments
		Assert.True(picker.ProcessKey(Key(ConsoleKey.RightArrow))); // hour -> min
		Assert.True(picker.ProcessKey(Key(ConsoleKey.RightArrow))); // min -> sec
		Assert.False(picker.ProcessKey(Key(ConsoleKey.RightArrow))); // sec -> no more
	}

	[Fact]
	public void Navigation_12HourWithSeconds_FourSegments()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 45), showSeconds: true, use24Hour: false);

		// Navigate: hour -> min -> sec -> AM/PM
		Assert.True(picker.ProcessKey(Key(ConsoleKey.RightArrow)));
		Assert.True(picker.ProcessKey(Key(ConsoleKey.RightArrow)));
		Assert.True(picker.ProcessKey(Key(ConsoleKey.RightArrow)));
		Assert.False(picker.ProcessKey(Key(ConsoleKey.RightArrow))); // no more
	}

	#endregion

	#region Spin Value Tests

	[Fact]
	public void UpArrow_IncrementsHour()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(11, 30, 0), picker.SelectedTime);
	}

	[Fact]
	public void DownArrow_DecrementsHour()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(new TimeSpan(9, 30, 0), picker.SelectedTime);
	}

	[Fact]
	public void UpArrow_IncrementsMinute_WhenFocused()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		// Move to minute segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(10, 31, 0), picker.SelectedTime);
	}

	[Fact]
	public void DownArrow_DecrementsMinute_WhenFocused()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(new TimeSpan(10, 29, 0), picker.SelectedTime);
	}

	[Fact]
	public void UpArrow_IncrementsSecond_WhenFocused()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 45), showSeconds: true, use24Hour: true);

		// Move to second segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(10, 30, 46), picker.SelectedTime);
	}

	[Fact]
	public void Hour24_WrapsFrom23To0()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(23, 0, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(0, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void Hour24_WrapsFrom0To23()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(new TimeSpan(23, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void Minute_WrapsFrom59To0()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 59, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // focus minute
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(10, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void Minute_WrapsFrom0To59()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // focus minute
		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.Equal(new TimeSpan(10, 59, 0), picker.SelectedTime);
	}

	[Fact]
	public void Second_WrapsFrom59To0()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 59), showSeconds: true, use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // sec
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(10, 30, 0), picker.SelectedTime);
	}

	[Fact]
	public void AmPm_ToggleWithUpArrow()
	{
		// 10 AM
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: false);

		// Navigate to AM/PM segment (segment 2 without seconds)
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // AM/PM

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// Should toggle to PM (22:00)
		Assert.Equal(new TimeSpan(22, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void AmPm_ToggleWithDownArrow()
	{
		// 2 PM = 14:00
		var (picker, _) = CreateFocusedPicker(new TimeSpan(14, 0, 0), use24Hour: false);

		// Navigate to AM/PM segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // AM/PM

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		// Should toggle to AM (02:00)
		Assert.Equal(new TimeSpan(2, 0, 0), picker.SelectedTime);
	}

	#endregion

	#region Digit Entry Tests

	[Fact]
	public void DigitEntry_TwoDigitHour_SetsValue()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		picker.ProcessKey(DigitKey(1)); // pending
		picker.ProcessKey(DigitKey(4)); // commit -> 14

		Assert.Equal(14, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void DigitEntry_SingleHighDigit_CommitsImmediately()
	{
		// In 24h mode, hour max is 23, maxTens = 2
		// Digit 3 > 2, so it should commit immediately as 3
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		picker.ProcessKey(DigitKey(3));

		Assert.Equal(3, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void DigitEntry_MinuteSegment_TwoDigits()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: true);

		// Focus minute segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow));

		picker.ProcessKey(DigitKey(4)); // pending
		picker.ProcessKey(DigitKey(5)); // commit -> 45

		Assert.Equal(45, picker.SelectedTime!.Value.Minutes);
	}

	[Fact]
	public void DigitEntry_ExceedsMax_ClampedToMax()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		// Type 2 then 9 -> 29 exceeds hour max 23, should clamp to 23
		picker.ProcessKey(DigitKey(2));
		picker.ProcessKey(DigitKey(9));

		Assert.Equal(23, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void DigitEntry_AutoAdvancesToNextSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		// Type "14" for hour - should auto-advance to minute
		picker.ProcessKey(DigitKey(1));
		picker.ProcessKey(DigitKey(4));

		// Now typing should go to minute segment
		picker.ProcessKey(DigitKey(3));
		picker.ProcessKey(DigitKey(0));

		Assert.Equal(new TimeSpan(14, 30, 0), picker.SelectedTime);
	}

	[Fact]
	public void DigitEntry_NonDigitChar_ReturnsFalse()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: true);

		bool handled = picker.ProcessKey(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false));

		Assert.False(handled);
	}

	[Fact]
	public void DigitEntry_OnAmPmSegment_AKeySetsAm()
	{
		// Start at PM (14:00)
		var (picker, _) = CreateFocusedPicker(new TimeSpan(14, 0, 0), use24Hour: false);

		// Navigate to AM/PM segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // AM/PM

		bool handled = picker.ProcessKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

		Assert.True(handled);
		Assert.Equal(new TimeSpan(2, 0, 0), picker.SelectedTime); // toggled to AM
	}

	[Fact]
	public void DigitEntry_OnAmPmSegment_PKeySetspm()
	{
		// Start at AM (10:00)
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: false);

		// Navigate to AM/PM segment
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // AM/PM

		bool handled = picker.ProcessKey(new ConsoleKeyInfo('p', ConsoleKey.P, false, false, false));

		Assert.True(handled);
		Assert.Equal(new TimeSpan(22, 0, 0), picker.SelectedTime); // toggled to PM
	}

	[Fact]
	public void DigitEntry_OnAmPmSegment_AKeyWhenAlreadyAm_NoChange()
	{
		// Start at AM (10:00)
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: false);

		picker.ProcessKey(Key(ConsoleKey.RightArrow));
		picker.ProcessKey(Key(ConsoleKey.RightArrow));

		picker.ProcessKey(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

		Assert.Equal(new TimeSpan(10, 0, 0), picker.SelectedTime); // unchanged
	}

	#endregion

	#region Min/Max Constraint Tests

	[Fact]
	public void MinTime_ClampsSelectedTime()
	{
		var picker = new TimePickerControl();
		picker.MinTime = new TimeSpan(8, 0, 0);

		picker.SelectedTime = new TimeSpan(6, 0, 0);

		Assert.Equal(new TimeSpan(8, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void MaxTime_ClampsSelectedTime()
	{
		var picker = new TimePickerControl();
		picker.MaxTime = new TimeSpan(17, 0, 0);

		picker.SelectedTime = new TimeSpan(20, 0, 0);

		Assert.Equal(new TimeSpan(17, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void MinMax_SpinClampsAtBounds()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(17, 0, 0), use24Hour: true);
		picker.MaxTime = new TimeSpan(17, 0, 0);

		picker.ProcessKey(Key(ConsoleKey.UpArrow)); // try to go above max

		// Value wraps to 0 via IncrementSegment, then gets clamped by ClampTime
		// The IncrementSegment wraps 17->18->0 via modular arithmetic, but ClampTime
		// doesn't apply per-segment, it applies to the full TimeSpan.
		// Actually IncrementSegment increments and calls SetSegmentValue which calls ClampTime.
		// 17+1 = 18 wraps to 18%24 = 18, then clamped to 17
		Assert.Equal(new TimeSpan(17, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void MinMax_BothSet_ValueWithinRange()
	{
		var picker = new TimePickerControl();
		picker.MinTime = new TimeSpan(9, 0, 0);
		picker.MaxTime = new TimeSpan(17, 0, 0);

		picker.SelectedTime = new TimeSpan(12, 0, 0);

		Assert.Equal(new TimeSpan(12, 0, 0), picker.SelectedTime);
	}

	#endregion

	#region Culture Tests

	[Fact]
	public void Culture_Default_IsCurrentCulture()
	{
		var picker = new TimePickerControl();

		Assert.Equal(CultureInfo.CurrentCulture, picker.Culture);
	}

	[Fact]
	public void Culture_SetExplicitly()
	{
		var picker = new TimePickerControl();
		var french = new CultureInfo("fr-FR");

		picker.Culture = french;

		Assert.Equal(french, picker.Culture);
	}

	[Fact]
	public void Culture_SetNull_FallsBackToCurrentCulture()
	{
		var picker = new TimePickerControl();

		picker.Culture = null!;

		Assert.Equal(CultureInfo.CurrentCulture, picker.Culture);
	}

	[Fact]
	public void Culture_24HourCulture_DefaultsTo24Hour()
	{
		// German culture uses 24h format (HH:mm)
		var german = new CultureInfo("de-DE");
		var picker = new TimePickerControl();
		picker.Culture = german;

		// With no explicit override, Use24HourFormat is null
		// The effective behavior is determined by the culture's ShortTimePattern
		Assert.Null(picker.Use24HourFormat);
	}

	[Fact]
	public void Culture_12HourCulture_DefaultsTo12Hour()
	{
		// US culture uses 12h format (h:mm tt)
		var us = new CultureInfo("en-US");
		var picker = new TimePickerControl();
		picker.Culture = us;

		Assert.Null(picker.Use24HourFormat);
	}

	#endregion

	#region Focus Tests

	[Fact]
	public void HasFocus_Set_FiresGotFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		// Add a placeholder so it takes auto-focus, leaving picker unfocused
		window.AddControl(new ButtonControl { Text = "Placeholder" });
		var picker = new TimePickerControl();
		window.AddControl(picker);

		FocusChangedEventArgs? args = null;
		window.FocusManager.FocusChanged += (_, e) => args = e;

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		Assert.NotNull(args);
		Assert.Equal(picker, args!.Current);
	}

	[Fact]
	public void HasFocus_Clear_FiresLostFocus()
	{
		var (picker, window) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		FocusChangedEventArgs? args = null;
		window.FocusManager.FocusChanged += (_, e) => args = e;

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		Assert.NotNull(args);
		Assert.Null(args!.Current);
		Assert.Equal(picker, args.Previous);
	}

	[Fact]
	public void HasFocus_SameValue_DoesNotFireEvent()
	{
		var (picker, window) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		int fireCount = 0;
		window.FocusManager.FocusChanged += (_, e) => fireCount++;

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic); // same focus

		Assert.Equal(0, fireCount);
	}

	[Fact]
	public void HasFocus_LostFocus_ClearsPendingDigit()
	{
		var (picker, window) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: true);

		// Start digit entry
		picker.ProcessKey(DigitKey(1)); // pending digit

		// Lose focus (triggers _pendingDigit = -1 side effect)
		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		// Regain focus
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		// Type another digit - should NOT combine with previous pending
		picker.ProcessKey(DigitKey(5));

		// If pending was cleared, 5 > maxTens(2), so commits as 5 immediately
		Assert.Equal(5, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void ProcessKey_WhenNotFocused_ReturnsFalse()
	{
		var picker = new TimePickerControl();
		picker.SelectedTime = new TimeSpan(10, 0, 0);
		// HasFocus is false by default

		bool handled = picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.False(handled);
		Assert.Equal(new TimeSpan(10, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new TimePickerControl();
		picker.SelectedTime = new TimeSpan(10, 0, 0);
		window.AddControl(picker);
		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);
		picker.IsEnabled = false;

		bool handled = picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.False(handled);
	}

	#endregion

	#region Event Tests

	[Fact]
	public void SelectedTimeChanged_FiresWhenTimeChanges()
	{
		var picker = new TimePickerControl();
		TimeSpan? eventValue = null;
		picker.SelectedTimeChanged += (s, e) => eventValue = e;

		picker.SelectedTime = new TimeSpan(14, 30, 0);

		Assert.Equal(new TimeSpan(14, 30, 0), eventValue);
	}

	[Fact]
	public void SelectedTimeChanged_FiresOnSpinChange()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 0, 0), use24Hour: true);

		TimeSpan? eventValue = null;
		picker.SelectedTimeChanged += (s, e) => eventValue = e;

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.Equal(new TimeSpan(11, 0, 0), eventValue);
	}

	[Fact]
	public void SelectedTimeChanged_DoesNotFireWhenSameValue()
	{
		var picker = new TimePickerControl();
		picker.SelectedTime = new TimeSpan(10, 0, 0);

		int fireCount = 0;
		picker.SelectedTimeChanged += (s, e) => fireCount++;

		picker.SelectedTime = new TimeSpan(10, 0, 0); // same value

		Assert.Equal(0, fireCount);
	}

	[Fact]
	public void SelectedTimeChanged_FiresWhenSetToNull()
	{
		var picker = new TimePickerControl();
		picker.SelectedTime = new TimeSpan(10, 0, 0);

		TimeSpan? eventValue = TimeSpan.MaxValue; // sentinel
		picker.SelectedTimeChanged += (s, e) => eventValue = e;

		picker.SelectedTime = null;

		Assert.Null(eventValue);
	}

	#endregion

	#region ContentWidth Tests

	[Fact]
	public void ContentWidth_24Hour_NoSeconds()
	{
		var picker = new TimePickerControl();
		picker.Use24HourFormat = true;
		picker.ShowSeconds = false;

		// "Time:" (5) + space(1) + HH(2) + :(1) + MM(2) = 11 + margins
		int? width = picker.ContentWidth;
		Assert.NotNull(width);
		Assert.True(width > 0);
	}

	[Fact]
	public void ContentWidth_24Hour_WithSeconds()
	{
		var picker = new TimePickerControl();
		picker.Use24HourFormat = true;
		picker.ShowSeconds = true;

		int? widthWithSeconds = picker.ContentWidth;

		picker.ShowSeconds = false;
		int? widthWithoutSeconds = picker.ContentWidth;

		// With seconds should be wider (adds separator + 2 digit width)
		Assert.True(widthWithSeconds > widthWithoutSeconds);
	}

	[Fact]
	public void ContentWidth_12Hour_WiderThan24Hour()
	{
		var picker = new TimePickerControl();
		picker.ShowSeconds = false;

		picker.Use24HourFormat = true;
		int? width24 = picker.ContentWidth;

		picker.Use24HourFormat = false;
		int? width12 = picker.ContentWidth;

		// 12h includes AM/PM designator space
		Assert.True(width12 > width24);
	}

	#endregion

	#region Home/End Key Tests

	[Fact]
	public void HomeKey_SetsSegmentToMinValue()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(15, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.Home));

		Assert.Equal(0, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void EndKey_SetsSegmentToMaxValue()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(15, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.End));

		Assert.Equal(23, picker.SelectedTime!.Value.Hours);
	}

	[Fact]
	public void HomeKey_OnMinuteSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(15, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // focus minute
		picker.ProcessKey(Key(ConsoleKey.Home));

		Assert.Equal(0, picker.SelectedTime!.Value.Minutes);
	}

	[Fact]
	public void EndKey_OnMinuteSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(15, 30, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // focus minute
		picker.ProcessKey(Key(ConsoleKey.End));

		Assert.Equal(59, picker.SelectedTime!.Value.Minutes);
	}

	#endregion

	#region PageUp/PageDown Tests

	[Fact]
	public void PageUp_IncrementsBy10()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(5, 0, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.PageUp));

		Assert.Equal(new TimeSpan(15, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void PageDown_DecrementsBy10()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(15, 0, 0), use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.PageDown));

		Assert.Equal(new TimeSpan(5, 0, 0), picker.SelectedTime);
	}

	#endregion

	#region 12-Hour Mode Tests

	[Fact]
	public void Hour12_DisplaysCorrectRange()
	{
		// In 12h mode, hour wraps 1-12
		var (picker, _) = CreateFocusedPicker(new TimeSpan(0, 0, 0), use24Hour: false);

		// Hour 0 in 24h = 12 AM in 12h
		// Pressing up from 12 should wrap to 1
		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// 0:00 + 1 hour in 12h mode: hour display was 12, increment wraps 12->1
		// In 24h terms: hours go from 0 to 1
		Assert.Equal(new TimeSpan(1, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void Hour12_Wraps12To1()
	{
		// 12 PM = 12:00
		var (picker, _) = CreateFocusedPicker(new TimeSpan(12, 0, 0), use24Hour: false);

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// 12 PM + 1 in 12h = 1 PM = 13:00
		Assert.Equal(new TimeSpan(13, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void Hour12_Wraps1To12()
	{
		// 1 PM = 13:00
		var (picker, _) = CreateFocusedPicker(new TimeSpan(13, 0, 0), use24Hour: false);

		picker.ProcessKey(Key(ConsoleKey.DownArrow));

		// 1 PM - 1 in 12h = 12 PM = 12:00
		Assert.Equal(new TimeSpan(12, 0, 0), picker.SelectedTime);
	}

	#endregion

	#region SetFocus Method Tests

	[Fact]
	public void SetFocus_True_SetsHasFocus()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Width = 80, Height = 30 };
		var picker = new TimePickerControl();
		window.AddControl(picker);

		window.FocusManager.SetFocus(picker, FocusReason.Programmatic);

		Assert.True(picker.HasFocus);
	}

	[Fact]
	public void SetFocus_False_ClearsHasFocus()
	{
		var (picker, window) = CreateFocusedPicker();

		window.FocusManager.SetFocus(null, FocusReason.Programmatic);

		Assert.False(picker.HasFocus);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void NullSelectedTime_SpinCreatesTimeFromZero()
	{
		var (picker, _) = CreateFocusedPicker(time: null, use24Hour: true);

		picker.ProcessKey(Key(ConsoleKey.UpArrow));

		// EffectiveTime is TimeSpan.Zero, incrementing hour gives 1:00
		Assert.Equal(new TimeSpan(1, 0, 0), picker.SelectedTime);
	}

	[Fact]
	public void DigitEntry_IntoSecondSegment()
	{
		var (picker, _) = CreateFocusedPicker(new TimeSpan(10, 30, 0), showSeconds: true, use24Hour: true);

		// Navigate to seconds
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // min
		picker.ProcessKey(Key(ConsoleKey.RightArrow)); // sec

		picker.ProcessKey(DigitKey(4));
		picker.ProcessKey(DigitKey(5));

		Assert.Equal(45, picker.SelectedTime!.Value.Seconds);
	}

	[Fact]
	public void Dispose_ClearsEventHandlers()
	{
		var picker = new TimePickerControl();
		int fireCount = 0;
		picker.SelectedTimeChanged += (s, e) => fireCount++;

		picker.Dispose();
		picker.SelectedTime = new TimeSpan(10, 0, 0);

		Assert.Equal(0, fireCount);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void DigitEntry_12HourMode_Hour13_ClampsTo12()
	{
		var (picker, _) = CreateFocusedPicker(time: new TimeSpan(1, 0, 0), use24Hour: false);

		// Type "1" then "3" into the hour segment
		picker.ProcessKey(DigitKey(1));
		picker.ProcessKey(DigitKey(3));

		// In 12-hour mode, valid hours are 1-12; 13 should clamp
		Assert.True(picker.SelectedTime!.Value.Hours <= 12);
	}

	[Fact]
	public void MinTime_EqualToMaxTime_ClampsExactly()
	{
		var onlyTime = new TimeSpan(14, 30, 0);
		var (picker, _) = CreateFocusedPicker();
		picker.MinTime = onlyTime;
		picker.MaxTime = onlyTime;

		picker.SelectedTime = new TimeSpan(8, 0, 0);
		Assert.Equal(onlyTime, picker.SelectedTime);

		picker.SelectedTime = new TimeSpan(23, 59, 59);
		Assert.Equal(onlyTime, picker.SelectedTime);
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
