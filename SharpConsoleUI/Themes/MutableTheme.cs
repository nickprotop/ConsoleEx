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
	/// Derives from <see cref="ThemeBase"/>, so a bare <c>new MutableTheme()</c> is a blank canvas
	/// (transparent/default member values) rather than inheriting any concrete theme's values. This
	/// makes a member that <see cref="CopyFrom"/> forgets to copy visibly wrong instead of silently
	/// adopting some base theme's value. It exposes settable <c>Name</c>/<c>Description</c> via
	/// <see cref="NameValue"/>/<see cref="DescriptionValue"/> for the builder/generator.
	/// </remarks>
	public sealed class MutableTheme : ThemeBase
	{
		/// <summary>Sets the theme name (alias of <see cref="ITheme.Name"/> for the builder/generator).</summary>
		public string NameValue { get => Name; set => Name = value ?? "Custom"; }

		/// <summary>Sets the theme description (alias of <see cref="ITheme.Description"/>).</summary>
		public string DescriptionValue { get => Description; set => Description = value ?? string.Empty; }

		/// <summary>Creates a blank mutable theme (transparent/default member values).</summary>
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
			Name = source.Name;
			Description = source.Description;
			Mode = source.Mode;

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
			DropdownFocusedForegroundColor = source.DropdownFocusedForegroundColor;
			DropdownFocusedBackgroundColor = source.DropdownFocusedBackgroundColor;
			DropdownDisabledForegroundColor = source.DropdownDisabledForegroundColor;
			DropdownDisabledBackgroundColor = source.DropdownDisabledBackgroundColor;

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
			ListForegroundColor = source.ListForegroundColor;
			ListFocusedForegroundColor = source.ListFocusedForegroundColor;
			ListSelectedForegroundColor = source.ListSelectedForegroundColor;
			ListSelectedBackgroundColor = source.ListSelectedBackgroundColor;
			ListDisabledForegroundColor = source.ListDisabledForegroundColor;
			ListDisabledBackgroundColor = source.ListDisabledBackgroundColor;
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
			DatePickerDisabledBackgroundColor = source.DatePickerDisabledBackgroundColor;
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
			CheckboxForegroundColor = source.CheckboxForegroundColor;
			CheckboxFocusedForegroundColor = source.CheckboxFocusedForegroundColor;
			CheckboxDisabledForegroundColor = source.CheckboxDisabledForegroundColor;
			CheckboxCheckmarkColor = source.CheckboxCheckmarkColor;

			// Tree (nullable)
			TreeBackgroundColor = source.TreeBackgroundColor;
			TreeSelectionBackgroundColor = source.TreeSelectionBackgroundColor;
			TreeUnfocusedSelectionBackgroundColor = source.TreeUnfocusedSelectionBackgroundColor;

			// Graphs (nullable)
			LineGraphBackgroundColor = source.LineGraphBackgroundColor;
			BarGraphBackgroundColor = source.BarGraphBackgroundColor;
			SparklineBackgroundColor = source.SparklineBackgroundColor;

			// Html
			HtmlForegroundColor = source.HtmlForegroundColor;

			// Start menu (nullable)
			StartMenuHeaderBackgroundColor = source.StartMenuHeaderBackgroundColor;
			StartMenuHeaderForegroundColor = source.StartMenuHeaderForegroundColor;
			StartMenuSectionHeaderBackgroundColor = source.StartMenuSectionHeaderBackgroundColor;
			StartMenuInfoStripForegroundColor = source.StartMenuInfoStripForegroundColor;


			return this;
		}
	}
}
