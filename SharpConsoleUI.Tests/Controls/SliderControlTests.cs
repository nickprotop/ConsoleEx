// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class SliderControlTests
{
	#region Helper Methods

	private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool ctrl = false)
		=> new ConsoleKeyInfo(keyChar, key, shift, alt, ctrl);

	#endregion

	#region SliderControl - Constructor Defaults

	[Fact]
	public void Constructor_Default_HasExpectedDefaults()
	{
		var slider = new SliderControl();

		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.Value);
		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.MinValue);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.MaxValue);
		Assert.Equal(ControlDefaults.SliderDefaultStep, slider.Step);
		Assert.Equal(ControlDefaults.SliderDefaultLargeStep, slider.LargeStep);
		Assert.Equal(SliderOrientation.Horizontal, slider.Orientation);
		Assert.True(slider.IsEnabled);
		Assert.False(slider.HasFocus);
		Assert.False(slider.IsDragging);
		Assert.False(slider.ShowValueLabel);
		Assert.False(slider.ShowMinMaxLabels);
	}

	[Fact]
	public void Constructor_CanReceiveFocus_WhenEnabled()
	{
		var slider = new SliderControl();

		Assert.True(slider.CanReceiveFocus);
		Assert.True(slider.WantsMouseEvents);
		Assert.True(slider.CanFocusWithMouse);
	}

	[Fact]
	public void Constructor_IsEnabled_WhenDisabled_CannotReceiveFocus()
	{
		var slider = new SliderControl();
		slider.IsEnabled = false;

		Assert.False(slider.CanReceiveFocus);
		Assert.False(slider.WantsMouseEvents);
		Assert.False(slider.CanFocusWithMouse);
	}

	#endregion

	#region SliderControl - Value Clamping

	[Fact]
	public void Value_AboveMax_ClampsToMax()
	{
		var slider = new SliderControl();

		slider.Value = 150;

		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.Value);
	}

	[Fact]
	public void Value_BelowMin_ClampsToMin()
	{
		var slider = new SliderControl();

		slider.Value = -50;

		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.Value);
	}

	[Fact]
	public void Value_SnapsToStep()
	{
		var slider = new SliderControl();
		slider.Step = 5;

		slider.Value = 13;

		Assert.Equal(15, slider.Value);
	}

	[Fact]
	public void Value_SnapsToStep_RoundDown()
	{
		var slider = new SliderControl();
		slider.Step = 5;

		slider.Value = 12;

		Assert.Equal(10, slider.Value);
	}

	#endregion

	#region SliderControl - Min/Max Enforcement

	[Fact]
	public void MinValue_CannotSetGreaterThanOrEqualToMax()
	{
		var slider = new SliderControl();

		slider.MinValue = 100;

		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.MinValue);
	}

	[Fact]
	public void MaxValue_CannotSetLessThanOrEqualToMin()
	{
		var slider = new SliderControl();

		slider.MaxValue = 0;

		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.MaxValue);
	}

	[Fact]
	public void MinValue_ClampsCurrentValue()
	{
		var slider = new SliderControl();
		slider.Value = 10;

		slider.MinValue = 20;

		Assert.Equal(20, slider.Value);
	}

	[Fact]
	public void MaxValue_ClampsCurrentValue()
	{
		var slider = new SliderControl();
		slider.Value = 80;

		slider.MaxValue = 50;

		Assert.Equal(50, slider.Value);
	}

	#endregion

	#region SliderControl - Step Minimum

	[Fact]
	public void Step_CannotBeLessThanMinStep()
	{
		var slider = new SliderControl();

		slider.Step = 0.0001;

		Assert.Equal(ControlDefaults.SliderMinStep, slider.Step);
	}

	[Fact]
	public void LargeStep_CannotBeLessThanMinStep()
	{
		var slider = new SliderControl();

		slider.LargeStep = 0.0001;

		Assert.Equal(ControlDefaults.SliderMinStep, slider.LargeStep);
	}

	#endregion

	#region SliderControl - Keyboard Horizontal

	[Fact]
	public void ProcessKey_RightArrow_AddsStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.True(handled);
		Assert.Equal(51, slider.Value);
	}

	[Fact]
	public void ProcessKey_LeftArrow_SubtractsStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.LeftArrow));

		Assert.True(handled);
		Assert.Equal(49, slider.Value);
	}

	[Fact]
	public void ProcessKey_ShiftRightArrow_AddsLargeStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow, shift: true));

		Assert.True(handled);
		Assert.Equal(60, slider.Value);
	}

	[Fact]
	public void ProcessKey_ShiftLeftArrow_SubtractsLargeStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.LeftArrow, shift: true));

		Assert.True(handled);
		Assert.Equal(40, slider.Value);
	}

	[Fact]
	public void ProcessKey_Home_SetsMinValue()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.Home));

		Assert.True(handled);
		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.Value);
	}

	[Fact]
	public void ProcessKey_End_SetsMaxValue()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.End));

		Assert.True(handled);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.Value);
	}

	[Fact]
	public void ProcessKey_PageUp_AddsLargeStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.PageUp));

		Assert.True(handled);
		Assert.Equal(60, slider.Value);
	}

	[Fact]
	public void ProcessKey_PageDown_SubtractsLargeStep()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.PageDown));

		Assert.True(handled);
		Assert.Equal(40, slider.Value);
	}

	#endregion

	#region SliderControl - Keyboard Vertical

	[Fact]
	public void ProcessKey_UpArrow_Vertical_IncreasesValue()
	{
		var slider = new SliderControl();
		slider.Orientation = SliderOrientation.Vertical;
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.True(handled);
		Assert.Equal(51, slider.Value);
	}

	[Fact]
	public void ProcessKey_DownArrow_Vertical_DecreasesValue()
	{
		var slider = new SliderControl();
		slider.Orientation = SliderOrientation.Vertical;
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.True(handled);
		Assert.Equal(49, slider.Value);
	}

	[Fact]
	public void ProcessKey_UpArrow_Horizontal_ReturnsFalse()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.UpArrow));

		Assert.False(handled);
		Assert.Equal(50, slider.Value);
	}

	[Fact]
	public void ProcessKey_DownArrow_Horizontal_ReturnsFalse()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.DownArrow));

		Assert.False(handled);
		Assert.Equal(50, slider.Value);
	}

	#endregion

	#region SliderControl - Keyboard Guard Clauses

	[Fact]
	public void ProcessKey_WhenDisabled_ReturnsFalse()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;
		slider.IsEnabled = false;
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(handled);
		Assert.Equal(50, slider.Value);
	}

	[Fact]
	public void ProcessKey_WhenNotFocused_ReturnsFalse()
	{
		var slider = new SliderControl();
		slider.Value = 50;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.False(handled);
		Assert.Equal(50, slider.Value);
	}

	[Fact]
	public void ProcessKey_UnhandledKey_ReturnsFalse()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;

		bool handled = slider.ProcessKey(Key(ConsoleKey.A, 'a'));

		Assert.False(handled);
	}

	#endregion

	#region SliderControl - Events

	[Fact]
	public void ValueChanged_FiresOnChange()
	{
		var slider = new SliderControl();
		double? receivedValue = null;
		slider.ValueChanged += (s, v) => receivedValue = v;

		slider.Value = 42;

		Assert.Equal(42, receivedValue);
	}

	[Fact]
	public void ValueChanged_DoesNotFireWhenValueUnchanged()
	{
		var slider = new SliderControl();
		slider.Value = 50;

		int fireCount = 0;
		slider.ValueChanged += (s, v) => fireCount++;

		slider.Value = 50;

		Assert.Equal(0, fireCount);
	}

	[Fact]
	public void GotFocus_FiresWhenFocusGained()
	{
		var slider = new SliderControl();
		bool fired = false;
		slider.GotFocus += (s, e) => fired = true;

		slider.HasFocus = true;

		Assert.True(fired);
	}

	[Fact]
	public void LostFocus_FiresWhenFocusLost()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;

		bool fired = false;
		slider.LostFocus += (s, e) => fired = true;

		slider.HasFocus = false;

		Assert.True(fired);
	}

	[Fact]
	public void GotFocus_DoesNotFireWhenAlreadyFocused()
	{
		var slider = new SliderControl();
		slider.HasFocus = true;

		bool fired = false;
		slider.GotFocus += (s, e) => fired = true;

		slider.HasFocus = true;

		Assert.False(fired);
	}

	#endregion

	#region SliderControl - Builder

	[Fact]
	public void Builder_SetsAllProperties()
	{
		var slider = new SliderBuilder()
			.WithValue(25)
			.WithRange(0, 200)
			.WithStep(5)
			.WithLargeStep(20)
			.WithName("TestSlider")
			.Build();

		Assert.Equal(25, slider.Value);
		Assert.Equal(0, slider.MinValue);
		Assert.Equal(200, slider.MaxValue);
		Assert.Equal(5, slider.Step);
		Assert.Equal(20, slider.LargeStep);
		Assert.Equal("TestSlider", slider.Name);
	}

	[Fact]
	public void Builder_WithRange_SetsMinAndMax()
	{
		var slider = new SliderBuilder()
			.WithRange(10, 50)
			.Build();

		Assert.Equal(10, slider.MinValue);
		Assert.Equal(50, slider.MaxValue);
	}

	[Fact]
	public void Builder_OnValueChanged_RegistersHandler()
	{
		double? received = null;
		var slider = new SliderBuilder()
			.OnValueChanged((s, v) => received = v)
			.Build();

		slider.Value = 75;

		Assert.Equal(75, received);
	}

	[Fact]
	public void Builder_Vertical_SetsOrientation()
	{
		var slider = new SliderBuilder()
			.Vertical()
			.Build();

		Assert.Equal(SliderOrientation.Vertical, slider.Orientation);
	}

	[Fact]
	public void Builder_DisplayProperties_SetCorrectly()
	{
		var slider = new SliderBuilder()
			.ShowValueLabel()
			.ShowMinMaxLabels()
			.WithValueFormat("F2")
			.Build();

		Assert.True(slider.ShowValueLabel);
		Assert.True(slider.ShowMinMaxLabels);
		Assert.Equal("F2", slider.ValueLabelFormat);
	}

	#endregion
}

public class RangeSliderControlTests
{
	#region Helper Methods

	private static ConsoleKeyInfo Key(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool ctrl = false)
		=> new ConsoleKeyInfo(keyChar, key, shift, alt, ctrl);

	#endregion

	#region Constructor Defaults

	[Fact]
	public void Constructor_Default_HasExpectedDefaults()
	{
		var slider = new RangeSliderControl();

		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.LowValue);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.HighValue);
		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.MinValue);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.MaxValue);
		Assert.Equal(ControlDefaults.SliderDefaultStep, slider.Step);
		Assert.Equal(ControlDefaults.SliderDefaultLargeStep, slider.LargeStep);
		Assert.Equal(ControlDefaults.RangeSliderDefaultMinRange, slider.MinRange);
		Assert.Equal(ActiveThumb.Low, slider.ActiveThumb);
		Assert.Equal(SliderOrientation.Horizontal, slider.Orientation);
		Assert.True(slider.IsEnabled);
		Assert.False(slider.HasFocus);
	}

	#endregion

	#region LowValue / HighValue Enforcement

	[Fact]
	public void LowValue_AboveHighValue_PushesHighValue()
	{
		var slider = new RangeSliderControl();
		slider.LowValue = 0;
		slider.HighValue = 50;

		slider.LowValue = 60;

		Assert.Equal(60, slider.LowValue);
		Assert.True(slider.HighValue >= slider.LowValue);
	}

	[Fact]
	public void HighValue_BelowLowValue_PushesLowValue()
	{
		var slider = new RangeSliderControl();
		slider.LowValue = 50;
		slider.HighValue = 80;

		slider.HighValue = 40;

		Assert.Equal(40, slider.HighValue);
		Assert.True(slider.LowValue <= slider.HighValue);
	}

	[Fact]
	public void MinRange_EnforcedBetweenValues()
	{
		var slider = new RangeSliderControl();
		slider.MinRange = 10;
		slider.LowValue = 50;
		slider.HighValue = 60;

		// Try to set low close to high
		slider.LowValue = 55;

		Assert.True(slider.HighValue - slider.LowValue >= 10);
	}

	[Fact]
	public void MinRange_SetAfterValues_EnforcesConstraint()
	{
		var slider = new RangeSliderControl();
		slider.LowValue = 45;
		slider.HighValue = 50;

		slider.MinRange = 10;

		Assert.True(slider.HighValue - slider.LowValue >= 10);
	}

	#endregion

	#region Value Clamping

	[Fact]
	public void LowValue_BelowMin_ClampsToMin()
	{
		var slider = new RangeSliderControl();

		slider.LowValue = -50;

		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.LowValue);
	}

	[Fact]
	public void HighValue_AboveMax_ClampsToMax()
	{
		var slider = new RangeSliderControl();

		slider.HighValue = 200;

		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.HighValue);
	}

	[Fact]
	public void LowValue_ClampsToMaxMinusMinRange()
	{
		var slider = new RangeSliderControl();
		slider.MinRange = 10;

		slider.LowValue = 95;

		Assert.True(slider.LowValue <= slider.MaxValue - slider.MinRange);
	}

	#endregion

	#region Keyboard - Tab Switches Thumb

	[Fact]
	public void ProcessKey_Tab_SwitchesActiveThumb()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		Assert.Equal(ActiveThumb.Low, slider.ActiveThumb);

		bool handled = slider.ProcessKey(Key(ConsoleKey.Tab));

		Assert.True(handled);
		Assert.Equal(ActiveThumb.High, slider.ActiveThumb);
	}

	[Fact]
	public void ProcessKey_Tab_OnHighThumb_ReturnsFalse()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;

		// Tab on Low → switches to High (handled)
		bool handled1 = slider.ProcessKey(Key(ConsoleKey.Tab));
		Assert.True(handled1);
		Assert.Equal(ActiveThumb.High, slider.ActiveThumb);

		// Tab on High → returns false to let focus move to next control
		bool handled2 = slider.ProcessKey(Key(ConsoleKey.Tab));
		Assert.False(handled2);
	}

	[Fact]
	public void ProcessKey_ShiftTab_OnHighThumb_SwitchesToLow()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.ActiveThumb = ActiveThumb.High;

		bool handled = slider.ProcessKey(Key(ConsoleKey.Tab, shift: true));
		Assert.True(handled);
		Assert.Equal(ActiveThumb.Low, slider.ActiveThumb);
	}

	[Fact]
	public void ProcessKey_ShiftTab_OnLowThumb_ReturnsFalse()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.ActiveThumb = ActiveThumb.Low;

		bool handled = slider.ProcessKey(Key(ConsoleKey.Tab, shift: true));
		Assert.False(handled);
	}

	#endregion

	#region Keyboard - Arrow Keys Move Active Thumb

	[Fact]
	public void ProcessKey_RightArrow_MovesLowThumb()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.LowValue = 20;
		slider.HighValue = 80;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.True(handled);
		Assert.Equal(21, slider.LowValue);
	}

	[Fact]
	public void ProcessKey_RightArrow_MovesHighThumb_WhenActive()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.LowValue = 20;
		slider.HighValue = 80;
		slider.ActiveThumb = ActiveThumb.High;

		bool handled = slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.True(handled);
		Assert.Equal(81, slider.HighValue);
	}

	[Fact]
	public void ProcessKey_LeftArrow_MovesActiveThumbDown()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.LowValue = 20;
		slider.HighValue = 80;

		slider.ProcessKey(Key(ConsoleKey.LeftArrow));

		Assert.Equal(19, slider.LowValue);
	}

	#endregion

	#region Keyboard - Home/End Per Active Thumb

	[Fact]
	public void ProcessKey_Home_LowThumb_SetsToMin()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.LowValue = 30;
		slider.HighValue = 80;

		bool handled = slider.ProcessKey(Key(ConsoleKey.Home));

		Assert.True(handled);
		Assert.Equal(ControlDefaults.SliderDefaultMinValue, slider.LowValue);
	}

	[Fact]
	public void ProcessKey_End_HighThumb_SetsToMax()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.LowValue = 20;
		slider.HighValue = 80;
		slider.ActiveThumb = ActiveThumb.High;

		bool handled = slider.ProcessKey(Key(ConsoleKey.End));

		Assert.True(handled);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, slider.HighValue);
	}

	[Fact]
	public void ProcessKey_Home_HighThumb_SetsToLowPlusMinRange()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.MinRange = 10;
		slider.LowValue = 30;
		slider.HighValue = 80;
		slider.ActiveThumb = ActiveThumb.High;

		slider.ProcessKey(Key(ConsoleKey.Home));

		Assert.Equal(40, slider.HighValue);
	}

	[Fact]
	public void ProcessKey_End_LowThumb_SetsToHighMinusMinRange()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.MinRange = 10;
		slider.LowValue = 20;
		slider.HighValue = 80;

		slider.ProcessKey(Key(ConsoleKey.End));

		Assert.Equal(70, slider.LowValue);
	}

	#endregion

	#region Keyboard - MinRange Enforced During Movement

	[Fact]
	public void ProcessKey_RightArrow_LowThumb_RespectsMinRange()
	{
		var slider = new RangeSliderControl();
		slider.HasFocus = true;
		slider.MinRange = 10;
		slider.LowValue = 69;
		slider.HighValue = 80;

		// Move low up - should push high if needed
		slider.ProcessKey(Key(ConsoleKey.RightArrow));

		Assert.True(slider.HighValue - slider.LowValue >= 10);
	}

	#endregion

	#region Events

	[Fact]
	public void LowValueChanged_FiresOnChange()
	{
		var slider = new RangeSliderControl();
		double? received = null;
		slider.LowValueChanged += (s, v) => received = v;

		slider.LowValue = 25;

		Assert.Equal(25, received);
	}

	[Fact]
	public void HighValueChanged_FiresOnChange()
	{
		var slider = new RangeSliderControl();
		double? received = null;
		slider.HighValueChanged += (s, v) => received = v;

		slider.HighValue = 75;

		Assert.Equal(75, received);
	}

	[Fact]
	public void RangeChanged_FiresWithBothValues()
	{
		var slider = new RangeSliderControl();
		(double Low, double High)? received = null;
		slider.RangeChanged += (s, v) => received = v;

		slider.LowValue = 25;

		Assert.NotNull(received);
		Assert.Equal(25, received!.Value.Low);
		Assert.Equal(ControlDefaults.SliderDefaultMaxValue, received!.Value.High);
	}

	[Fact]
	public void HighValueChanged_FiresWhenPushedByLowValue()
	{
		var slider = new RangeSliderControl();
		slider.MinRange = 10;
		slider.LowValue = 20;
		slider.HighValue = 25;

		double? highReceived = null;
		slider.HighValueChanged += (s, v) => highReceived = v;

		slider.LowValue = 20; // Already at 20, so re-set triggers push if needed
		// Better: force a push
		slider = new RangeSliderControl();
		slider.MinRange = 10;
		slider.HighValue = 50;
		slider.HighValueChanged += (s, v) => highReceived = v;
		slider.LowValue = 45;

		Assert.NotNull(highReceived);
		Assert.True(highReceived >= 55);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_SetsAllProperties()
	{
		var slider = new RangeSliderBuilder()
			.WithValues(20, 80)
			.WithRange(0, 200)
			.WithStep(5)
			.WithLargeStep(20)
			.WithMinRange(10)
			.WithName("TestRange")
			.Build();

		Assert.Equal(20, slider.LowValue);
		Assert.Equal(80, slider.HighValue);
		Assert.Equal(0, slider.MinValue);
		Assert.Equal(200, slider.MaxValue);
		Assert.Equal(5, slider.Step);
		Assert.Equal(20, slider.LargeStep);
		Assert.Equal(10, slider.MinRange);
		Assert.Equal("TestRange", slider.Name);
	}

	[Fact]
	public void Builder_WithValues_SetsBothValues()
	{
		var slider = new RangeSliderBuilder()
			.WithValues(30, 70)
			.Build();

		Assert.Equal(30, slider.LowValue);
		Assert.Equal(70, slider.HighValue);
	}

	[Fact]
	public void Builder_WithMinRange_SetsMinRange()
	{
		var slider = new RangeSliderBuilder()
			.WithMinRange(15)
			.Build();

		Assert.Equal(15, slider.MinRange);
	}

	[Fact]
	public void Builder_OnRangeChanged_RegistersHandler()
	{
		(double Low, double High)? received = null;
		var slider = new RangeSliderBuilder()
			.OnRangeChanged((s, v) => received = v)
			.Build();

		slider.LowValue = 25;

		Assert.NotNull(received);
		Assert.Equal(25, received!.Value.Low);
	}

	[Fact]
	public void Builder_OnLowValueChanged_RegistersHandler()
	{
		double? received = null;
		var slider = new RangeSliderBuilder()
			.OnLowValueChanged((s, v) => received = v)
			.Build();

		slider.LowValue = 30;

		Assert.Equal(30, received);
	}

	[Fact]
	public void Builder_OnHighValueChanged_RegistersHandler()
	{
		double? received = null;
		var slider = new RangeSliderBuilder()
			.OnHighValueChanged((s, v) => received = v)
			.Build();

		slider.HighValue = 60;

		Assert.Equal(60, received);
	}

	[Fact]
	public void Builder_Vertical_SetsOrientation()
	{
		var slider = new RangeSliderBuilder()
			.Vertical()
			.Build();

		Assert.Equal(SliderOrientation.Vertical, slider.Orientation);
	}

	#endregion
}
