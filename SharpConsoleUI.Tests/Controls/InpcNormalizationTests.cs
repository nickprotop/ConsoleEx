// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Verifies the §6 control INPC normalizations: every two-way-bindable control value raises
/// INotifyPropertyChanged on its property setter (the binding engine listens to PropertyChanged).
/// </summary>
public class InpcNormalizationTests
{
	private static List<string?> Track(System.ComponentModel.INotifyPropertyChanged c)
	{
		var raised = new List<string?>();
		c.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
		return raised;
	}

	[Fact]
	public void Prompt_Input_setter_raises_INPC()
	{
		var prompt = new PromptControl();
		var raised = Track(prompt);

		prompt.Input = "hello";

		Assert.Contains(nameof(PromptControl.Input), raised);
	}

	[Fact]
	public void Prompt_SetInput_raises_INPC()
	{
		var prompt = new PromptControl();
		var raised = Track(prompt);

		prompt.SetInput("typed");

		Assert.Contains(nameof(PromptControl.Input), raised);
	}

	[Fact]
	public void MultilineEdit_Content_setter_raises_INPC()
	{
		var edit = new MultilineEditControl();
		var raised = Track(edit);

		edit.Content = "line1\nline2";

		Assert.Contains(nameof(MultilineEditControl.Content), raised);
	}

	[Fact]
	public void Table_SelectedRowIndex_setter_raises_INPC()
	{
		var table = new TableControl();
		table.AddColumn("A");
		table.AddRow("1");
		table.AddRow("2");
		var raised = Track(table);

		table.SelectedRowIndex = 1;

		Assert.Contains(nameof(TableControl.SelectedRowIndex), raised);
	}

	[Fact]
	public void CollapsiblePanel_IsExpanded_setter_raises_INPC()
	{
		var panel = new CollapsiblePanel { Title = "hdr" };
		var raised = Track(panel);

		panel.IsExpanded = !panel.IsExpanded;

		Assert.Contains(nameof(CollapsiblePanel.IsExpanded), raised);
	}

	[Fact]
	public void CollapsiblePanel_Toggle_raises_INPC()
	{
		var panel = new CollapsiblePanel { Title = "hdr" };
		var raised = Track(panel);

		panel.Toggle();

		Assert.Contains(nameof(CollapsiblePanel.IsExpanded), raised);
	}

	[Fact]
	public void TreeNode_is_INPC_and_IsExpanded_raises()
	{
		var node = new TreeNode("root");
		Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(node);

		var raised = Track(node);
		node.IsExpanded = !node.IsExpanded;
		Assert.Contains(nameof(TreeNode.IsExpanded), raised);
	}

	[Fact]
	public void TreeNode_Text_and_TextColor_raise_INPC()
	{
		var node = new TreeNode("root");
		var raised = Track(node);

		node.Text = "changed";
		node.TextColor = Color.Red;

		Assert.Contains(nameof(TreeNode.Text), raised);
		Assert.Contains(nameof(TreeNode.TextColor), raised);
	}

	[Fact]
	public void Spinner_IsSpinning_setter_raises_INPC()
	{
		var spinner = new SpinnerControl { IsSpinning = false };
		var raised = Track(spinner);

		spinner.IsSpinning = true;

		Assert.Contains(nameof(SpinnerControl.IsSpinning), raised);
	}

	[Fact]
	public void Unchanged_value_does_not_raise_on_guarded_setters()
	{
		// Setters with an equality guard must not re-raise for an unchanged value.
		// (TreeNode.IsExpanded and CollapsiblePanel.IsExpanded both guard; PromptControl.SetInput
		// intentionally always notifies because it also resets cursor/scroll, so it is excluded.)
		var node = new TreeNode("n") { IsExpanded = true };
		var nodeRaised = Track(node);
		node.IsExpanded = true; // same value
		Assert.DoesNotContain(nameof(TreeNode.IsExpanded), nodeRaised);

		var panel = new CollapsiblePanel { Title = "p" };
		bool expanded = panel.IsExpanded;
		var panelRaised = Track(panel);
		panel.IsExpanded = expanded; // same value
		Assert.DoesNotContain(nameof(CollapsiblePanel.IsExpanded), panelRaised);
	}
}
