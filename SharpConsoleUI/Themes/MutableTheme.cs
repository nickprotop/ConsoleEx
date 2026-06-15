// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// A fully settable <see cref="ITheme"/> used as the working buffer and result of theme
	/// derivation (see <see cref="Theme.From(ITheme)"/>). Every theme member is mutable, so a derived
	/// theme can override any subset of colors after copying a base theme via <see cref="CopyFrom"/>.
	/// </summary>
	/// <remarks>
	/// Inherits every settable theme member + its defaults from <see cref="ModernGrayTheme"/>, so a
	/// bare <c>new MutableTheme()</c> is already a valid theme (ModernGray's values). It only adds
	/// settable <c>Name</c>/<c>Description</c> (which the base exposes as get-only) via
	/// <see cref="NameValue"/>/<see cref="DescriptionValue"/>.
	/// </remarks>
	public sealed class MutableTheme : ModernGrayTheme
	{
		private string _name = "Custom";
		private string _description = "Custom theme";

		/// <inheritdoc/>
		public override string Name => _name;

		/// <inheritdoc/>
		public override string Description => _description;

		/// <summary>Sets the theme name.</summary>
		public string NameValue { get => _name; set => _name = value ?? "Custom"; }

		/// <summary>Sets the theme description.</summary>
		public string DescriptionValue { get => _description; set => _description = value ?? string.Empty; }

		/// <summary>Creates a mutable theme pre-populated with ModernGray's values.</summary>
		public MutableTheme()
		{
		}

		/// <summary>
		/// Copies every <see cref="ITheme"/> member value from <paramref name="source"/> into this theme.
		/// Hand-written (reflection-free, AOT-safe). When adding a new <see cref="ITheme"/> member, ADD IT
		/// HERE — the CopyFrom completeness test will fail until you do.
		/// </summary>
		/// <param name="source">The theme to copy all member values from.</param>
		/// <returns>This instance (for chaining).</returns>
		public MutableTheme CopyFrom(ITheme source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			// Identity
			_name = source.Name;
			_description = source.Description;

			// Window / borders / titles
			WindowBackgroundColor = source.WindowBackgroundColor;
			WindowForegroundColor = source.WindowForegroundColor;
			ActiveBorderForegroundColor = source.ActiveBorderForegroundColor;
			InactiveBorderForegroundColor = source.InactiveBorderForegroundColor;
			ActiveTitleForegroundColor = source.ActiveTitleForegroundColor;
			InactiveTitleForegroundColor = source.InactiveTitleForegroundColor;

			// Top / bottom bars
			TopBarBackgroundColor = source.TopBarBackgroundColor;
			TopBarForegroundColor = source.TopBarForegroundColor;
			BottomBarBackgroundColor = source.BottomBarBackgroundColor;
			BottomBarForegroundColor = source.BottomBarForegroundColor;

			// Buttons
			ButtonBackgroundColor = source.ButtonBackgroundColor;
			ButtonForegroundColor = source.ButtonForegroundColor;
			ButtonFocusedBackgroundColor = source.ButtonFocusedBackgroundColor;
			ButtonFocusedForegroundColor = source.ButtonFocusedForegroundColor;
			ButtonSelectedBackgroundColor = source.ButtonSelectedBackgroundColor;
			ButtonSelectedForegroundColor = source.ButtonSelectedForegroundColor;
			ButtonDisabledBackgroundColor = source.ButtonDisabledBackgroundColor;
			ButtonDisabledForegroundColor = source.ButtonDisabledForegroundColor;

			// List / collapsible
			ListUnfocusedHighlightBackgroundColor = source.ListUnfocusedHighlightBackgroundColor;
			ListUnfocusedHighlightForegroundColor = source.ListUnfocusedHighlightForegroundColor;
			CollapsibleHeaderFocusedBackgroundColor = source.CollapsibleHeaderFocusedBackgroundColor;
			CollapsibleHeaderFocusedForegroundColor = source.CollapsibleHeaderFocusedForegroundColor;

			// Desktop
			DesktopBackgroundColor = source.DesktopBackgroundColor;
			DesktopForegroundColor = source.DesktopForegroundColor;
			DesktopBackgroundChar = source.DesktopBackgroundChar;
			DesktopBackgroundGradient = source.DesktopBackgroundGradient;

			// Modal / notifications
			ModalBackgroundColor = source.ModalBackgroundColor;
			ModalBorderForegroundColor = source.ModalBorderForegroundColor;
			ModalTitleForegroundColor = source.ModalTitleForegroundColor;
			ModalFlashColor = source.ModalFlashColor;
			ShowModalShadow = source.ShowModalShadow;
			UseDoubleLineBorderForModal = source.UseDoubleLineBorderForModal;
			NotificationWindowBackgroundColor = source.NotificationWindowBackgroundColor;
			NotificationInfoWindowBackgroundColor = source.NotificationInfoWindowBackgroundColor;
			NotificationSuccessWindowBackgroundColor = source.NotificationSuccessWindowBackgroundColor;
			NotificationWarningWindowBackgroundColor = source.NotificationWarningWindowBackgroundColor;
			NotificationDangerWindowBackgroundColor = source.NotificationDangerWindowBackgroundColor;

			// Prompt / text edit
			PromptInputBackgroundColor = source.PromptInputBackgroundColor;
			PromptInputForegroundColor = source.PromptInputForegroundColor;
			PromptInputFocusedBackgroundColor = source.PromptInputFocusedBackgroundColor;
			PromptInputFocusedForegroundColor = source.PromptInputFocusedForegroundColor;
			TextEditFocusedNotEditing = source.TextEditFocusedNotEditing;

			// Progress bar
			ProgressBarFilledColor = source.ProgressBarFilledColor;
			ProgressBarUnfilledColor = source.ProgressBarUnfilledColor;
			ProgressBarPercentageColor = source.ProgressBarPercentageColor;

			// Dropdown
			DropdownBackgroundColor = source.DropdownBackgroundColor;
			DropdownForegroundColor = source.DropdownForegroundColor;
			DropdownHighlightBackgroundColor = source.DropdownHighlightBackgroundColor;
			DropdownHighlightForegroundColor = source.DropdownHighlightForegroundColor;

			// Menu bar / dropdown
			MenuBarHighlightBackgroundColor = source.MenuBarHighlightBackgroundColor;
			MenuBarHighlightForegroundColor = source.MenuBarHighlightForegroundColor;
			MenuDropdownBackgroundColor = source.MenuDropdownBackgroundColor;
			MenuDropdownForegroundColor = source.MenuDropdownForegroundColor;
			MenuDropdownHighlightBackgroundColor = source.MenuDropdownHighlightBackgroundColor;
			MenuDropdownHighlightForegroundColor = source.MenuDropdownHighlightForegroundColor;

			// Tabs
			TabHeaderBackgroundColor = source.TabHeaderBackgroundColor;
			TabHeaderForegroundColor = source.TabHeaderForegroundColor;
			TabHeaderActiveBackgroundColor = source.TabHeaderActiveBackgroundColor;
			TabHeaderActiveForegroundColor = source.TabHeaderActiveForegroundColor;
			TabHeaderFocusedBackgroundColor = source.TabHeaderFocusedBackgroundColor;
			TabHeaderFocusedForegroundColor = source.TabHeaderFocusedForegroundColor;
			TabHeaderActiveFocusedBackgroundColor = source.TabHeaderActiveFocusedBackgroundColor;
			TabHeaderActiveFocusedForegroundColor = source.TabHeaderActiveFocusedForegroundColor;
			TabHeaderDisabledBackgroundColor = source.TabHeaderDisabledBackgroundColor;
			TabHeaderDisabledForegroundColor = source.TabHeaderDisabledForegroundColor;

			// Table
			TableBackgroundColor = source.TableBackgroundColor;
			TableForegroundColor = source.TableForegroundColor;
			TableHeaderBackgroundColor = source.TableHeaderBackgroundColor;
			TableHeaderForegroundColor = source.TableHeaderForegroundColor;
			TableSelectionBackgroundColor = source.TableSelectionBackgroundColor;
			TableSelectionForegroundColor = source.TableSelectionForegroundColor;
			TableUnfocusedSelectionBackgroundColor = source.TableUnfocusedSelectionBackgroundColor;
			TableUnfocusedSelectionForegroundColor = source.TableUnfocusedSelectionForegroundColor;
			TableHoverBackgroundColor = source.TableHoverBackgroundColor;
			TableHoverForegroundColor = source.TableHoverForegroundColor;
			TableScrollbarThumbColor = source.TableScrollbarThumbColor;
			TableScrollbarTrackColor = source.TableScrollbarTrackColor;

			// General scrollbar colors
			ScrollbarThumbColor = source.ScrollbarThumbColor;
			ScrollbarThumbUnfocusedColor = source.ScrollbarThumbUnfocusedColor;
			ScrollbarTrackColor = source.ScrollbarTrackColor;
			ScrollbarTrackUnfocusedColor = source.ScrollbarTrackUnfocusedColor;


			// List / toolbar / menu-bar / separator (nullable)
			ListHoverBackgroundColor = source.ListHoverBackgroundColor;
			ListHoverForegroundColor = source.ListHoverForegroundColor;
			ListBackgroundColor = source.ListBackgroundColor;
			ToolbarBackgroundColor = source.ToolbarBackgroundColor;
			ToolbarForegroundColor = source.ToolbarForegroundColor;
			SeparatorForegroundColor = source.SeparatorForegroundColor;
			MenuBarBackgroundColor = source.MenuBarBackgroundColor;
			MenuBarForegroundColor = source.MenuBarForegroundColor;

			// Table / tab content borders (nullable)
			TableBorderColor = source.TableBorderColor;
			TabContentBorderColor = source.TabContentBorderColor;
			TabContentBackgroundColor = source.TabContentBackgroundColor;

			// Date picker (nullable)
			DatePickerBackgroundColor = source.DatePickerBackgroundColor;
			DatePickerForegroundColor = source.DatePickerForegroundColor;
			DatePickerFocusedBackgroundColor = source.DatePickerFocusedBackgroundColor;
			DatePickerFocusedForegroundColor = source.DatePickerFocusedForegroundColor;
			DatePickerSegmentBackgroundColor = source.DatePickerSegmentBackgroundColor;
			DatePickerSegmentForegroundColor = source.DatePickerSegmentForegroundColor;
			DatePickerDisabledForegroundColor = source.DatePickerDisabledForegroundColor;
			DatePickerCalendarTodayColor = source.DatePickerCalendarTodayColor;
			DatePickerCalendarSelectedColor = source.DatePickerCalendarSelectedColor;
			DatePickerCalendarHeaderColor = source.DatePickerCalendarHeaderColor;

			// Time picker (nullable)
			TimePickerBackgroundColor = source.TimePickerBackgroundColor;
			TimePickerForegroundColor = source.TimePickerForegroundColor;
			TimePickerFocusedBackgroundColor = source.TimePickerFocusedBackgroundColor;
			TimePickerFocusedForegroundColor = source.TimePickerFocusedForegroundColor;
			TimePickerSegmentBackgroundColor = source.TimePickerSegmentBackgroundColor;
			TimePickerSegmentForegroundColor = source.TimePickerSegmentForegroundColor;
			TimePickerDisabledForegroundColor = source.TimePickerDisabledForegroundColor;

			// Status bar (nullable)
			StatusBarBackgroundColor = source.StatusBarBackgroundColor;
			StatusBarForegroundColor = source.StatusBarForegroundColor;
			StatusBarShortcutForegroundColor = source.StatusBarShortcutForegroundColor;

			// Slider (nullable)
			SliderTrackColor = source.SliderTrackColor;
			SliderFilledTrackColor = source.SliderFilledTrackColor;
			SliderThumbColor = source.SliderThumbColor;
			SliderFocusedThumbColor = source.SliderFocusedThumbColor;

			// Checkbox (nullable)
			CheckboxBackgroundColor = source.CheckboxBackgroundColor;
			CheckboxFocusedBackgroundColor = source.CheckboxFocusedBackgroundColor;
			CheckboxDisabledBackgroundColor = source.CheckboxDisabledBackgroundColor;

			// Tree (nullable)
			TreeBackgroundColor = source.TreeBackgroundColor;
			TreeSelectionBackgroundColor = source.TreeSelectionBackgroundColor;
			TreeUnfocusedSelectionBackgroundColor = source.TreeUnfocusedSelectionBackgroundColor;

			// Graphs (nullable)
			LineGraphBackgroundColor = source.LineGraphBackgroundColor;
			BarGraphBackgroundColor = source.BarGraphBackgroundColor;
			SparklineBackgroundColor = source.SparklineBackgroundColor;

			// Start menu (nullable)
			StartMenuHeaderBackgroundColor = source.StartMenuHeaderBackgroundColor;
			StartMenuHeaderForegroundColor = source.StartMenuHeaderForegroundColor;
			StartMenuSectionHeaderBackgroundColor = source.StartMenuSectionHeaderBackgroundColor;
			StartMenuInfoStripForegroundColor = source.StartMenuInfoStripForegroundColor;


			return this;
		}
	}
}
