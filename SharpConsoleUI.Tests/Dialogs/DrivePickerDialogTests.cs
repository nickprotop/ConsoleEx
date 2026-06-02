// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using System;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Dialogs;

public class DrivePickerDialogTests
{
	private static ConsoleKeyInfo CtrlD()
		=> new ConsoleKeyInfo('', ConsoleKey.D, shift: false, alt: false, control: true);

	private static ConsoleKeyInfo Esc()
		=> new ConsoleKeyInfo('', ConsoleKey.Escape, shift: false, alt: false, control: false);

	[Fact]
	public void FolderPicker_CtrlD_PopulatesFolderListWithPlaces()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(100, 30);

		// Launch the dialog (do not await — it completes when the modal closes).
		_ = FolderPickerDialog.ShowAsync(sys, startPath: Environment.CurrentDirectory);

		var modal = sys.WindowStateService.ActiveWindow;
		Assert.NotNull(modal);
		var folderList = modal!.FindControl<ListControl>("FolderList");
		Assert.NotNull(folderList);

		// Send Ctrl+D via the preview hook (fires before the focused list).
		modal.OnPreviewKeyPressed(CtrlD());

		// The list should now contain at least the root place, and every item's
		// Tag should be a string path (Places mode), not a FolderEntry.
		Assert.NotEmpty(folderList!.Items);
		Assert.All(folderList.Items, i => Assert.IsType<string>(i.Tag));

		// Esc returns to browsing (does NOT close the dialog).
		modal.OnPreviewKeyPressed(Esc());
		modal.Close();
	}

	[Fact]
	public void FolderPicker_Esc_InPlacesMode_ReturnsToBrowsing_DoesNotClose()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		_ = FolderPickerDialog.ShowAsync(sys, startPath: Environment.CurrentDirectory);

		var modal = sys.WindowStateService.ActiveWindow!;
		var folderList = modal.FindControl<ListControl>("FolderList")!;

		modal.OnPreviewKeyPressed(CtrlD());
		// In Places mode the tags are bare string paths.
		Assert.All(folderList.Items, i => Assert.IsType<string>(i.Tag));

		// Esc in Places mode: handled, returns to browsing, modal still active.
		bool handled = modal.OnPreviewKeyPressed(Esc());
		Assert.True(handled);
		Assert.Same(modal, sys.WindowStateService.ActiveWindow); // not closed
		// Back to normal browsing: '..' parent entry tag is a FolderEntry, not a string.
		Assert.Contains(folderList.Items, i => i.Tag is not string && i.Tag is not null);

		modal.Close();
	}

	[Fact]
	public void FolderPicker_EnterOnPlace_NavigatesAndExitsPlacesMode()
	{
		// Start somewhere with a known parent so a place ("/" or a drive root) exists.
		var sys = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		_ = FolderPickerDialog.ShowAsync(sys, startPath: Environment.CurrentDirectory);

		var modal = sys.WindowStateService.ActiveWindow!;
		var folderList = modal.FindControl<ListControl>("FolderList")!;

		modal.OnPreviewKeyPressed(CtrlD());

		// Pick a place whose path is an existing directory and activate it via Enter.
		int idx = -1;
		for (int i = 0; i < folderList.Items.Count; i++)
		{
			if (folderList.Items[i].Tag is string p && System.IO.Directory.Exists(p)) { idx = i; break; }
		}
		Assert.True(idx >= 0, "expected at least one existing place directory");
		folderList.SelectedIndex = idx;

		var placePath = (string)folderList.Items[idx].Tag!;

		// Enter through the list's own key processing raises ItemActivated.
		folderList.ProcessKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

		// Back in browsing mode: the footer reverted from the Places hint.
		var footer = modal.FindControl<MarkupControl>("FooterHint")!;
		Assert.DoesNotContain("Back to browsing", footer.Text);
		Assert.Contains("Ctrl+D: Places", footer.Text);

		// And the path display now reflects the place we navigated into.
		var pathCtrl = modal.FindControl<MarkupControl>("PathDisplay")!;
		var (seg, _) = DrivePlaces.SplitDriveSegment(placePath);
		Assert.Contains(seg.Replace("[", "[["), pathCtrl.Text);

		modal.Close();
	}

	[Fact]
	public void SaveDialog_CtrlD_WorksEvenWithFilenameFieldFocused()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		_ = FileDialogs.ShowSaveFileAsync(sys, startPath: Environment.CurrentDirectory, defaultFileName: "untitled.txt");

		var modal = sys.WindowStateService.ActiveWindow;
		Assert.NotNull(modal);
		var folderList = modal!.FindControl<ListControl>("FolderList");
		Assert.NotNull(folderList);

		// Ctrl+D should enter Places mode regardless of focus.
		modal.OnPreviewKeyPressed(CtrlD());
		Assert.NotEmpty(folderList!.Items);
		Assert.All(folderList.Items, i =>
			Assert.StartsWith("PLACE:", Assert.IsType<string>(i.Tag)));

		modal.Close();
	}

	[Fact]
	public void FilePicker_CtrlD_EntersPlacesMode()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		_ = FileDialogs.ShowFilePickerAsync(sys, startPath: Environment.CurrentDirectory);

		var modal = sys.WindowStateService.ActiveWindow;
		Assert.NotNull(modal);
		var folderList = modal!.FindControl<ListControl>("FolderList");
		Assert.NotNull(folderList);

		modal.OnPreviewKeyPressed(CtrlD());
		Assert.NotEmpty(folderList!.Items);
		Assert.All(folderList.Items, i =>
			Assert.StartsWith("PLACE:", Assert.IsType<string>(i.Tag)));

		modal.Close();
	}
}
