// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class TabControl
	{
	#region Tab Management Methods

	/// <summary>
	/// Removes a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab to remove.</param>
	public void RemoveTab(int index)
	{
		TabPage tabPage;
		lock (_tabLock)
		{
			if (index < 0 || index >= _tabPages.Count)
				return;

			tabPage = _tabPages[index];
			_tabPages.RemoveAt(index);
			tabPage.Content.Dispose();

			// Adjust active tab index
			if (_tabPages.Count == 0)
			{
				_activeTabIndex = -1;
			}
			else if (index == _activeTabIndex)
			{
				// Removing active tab
				if (_activeTabIndex >= _tabPages.Count)
				{
					// Was last tab, go to previous
					_activeTabIndex = _tabPages.Count - 1;
				}
				// else: stay at same index (next tab slides into position)
				_tabPages[_activeTabIndex].Content.Visible = true;
			}
			else if (index < _activeTabIndex)
			{
				// Removing tab before active, decrement index
				_activeTabIndex--;
			}
		}

		TabRemoved?.Invoke(this, new TabEventArgs(tabPage, index));
		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(true);
	}

	/// <summary>
	/// Removes a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab to remove.</param>
	public void RemoveTabAt(int index) => RemoveTab(index);

	/// <summary>
	/// Removes the first tab with the specified title.
	/// </summary>
	/// <param name="title">The title of the tab to remove.</param>
	/// <returns>True if a tab was removed, false otherwise.</returns>
	public bool RemoveTab(string title)
	{
		int index;
		lock (_tabLock) { index = _tabPages.FindIndex(t => t.Title == title); }
		if (index >= 0)
		{
			RemoveTab(index);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Removes all tabs from the control.
	/// </summary>
	public void ClearTabs()
	{
		while (_tabPages.Count > 0)
		{
			RemoveTab(0);
		}
	}

	/// <summary>
	/// Finds a tab by title.
	/// </summary>
	/// <param name="title">The title to search for.</param>
	/// <returns>The tab page, or null if not found.</returns>
	public TabPage? FindTab(string title)
	{
		lock (_tabLock) { return _tabPages.FirstOrDefault(t => t.Title == title); }
	}

	/// <summary>
	/// Gets a tab by index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <returns>The tab page, or null if index is out of range.</returns>
	public TabPage? GetTab(int index)
	{
		lock (_tabLock) { return index >= 0 && index < _tabPages.Count ? _tabPages[index] : null; }
	}

	/// <summary>
	/// Checks if a tab with the specified title exists.
	/// </summary>
	/// <param name="title">The title to search for.</param>
	/// <returns>True if a tab with the title exists, false otherwise.</returns>
	public bool HasTab(string title)
	{
		lock (_tabLock) { return _tabPages.Any(t => t.Title == title); }
	}

	/// <summary>
	/// Switches to the next tab (wraps around to first tab).
	/// </summary>
	public void NextTab()
	{
		lock (_tabLock)
		{
			if (_tabPages.Count > 0)
			{
				ActiveTabIndex = (_activeTabIndex + 1) % _tabPages.Count;
			}
		}
	}

	/// <summary>
	/// Switches to the previous tab (wraps around to last tab).
	/// </summary>
	public void PreviousTab()
	{
		lock (_tabLock)
		{
			if (_tabPages.Count > 0)
			{
				ActiveTabIndex = (_activeTabIndex - 1 + _tabPages.Count) % _tabPages.Count;
			}
		}
	}

	/// <summary>
	/// Switches to a tab by title.
	/// </summary>
	/// <param name="title">The title of the tab to switch to.</param>
	/// <returns>True if the tab was found and switched to, false otherwise.</returns>
	public bool SwitchToTab(string title)
	{
		int index;
		lock (_tabLock) { index = _tabPages.FindIndex(t => t.Title == title); }
		if (index >= 0)
		{
			ActiveTabIndex = index;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Sets the title of a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <param name="newTitle">The new title.</param>
	public void SetTabTitle(int index, string newTitle)
	{
		lock (_tabLock)
		{
			if (index >= 0 && index < _tabPages.Count)
			{
				_tabPages[index].Title = newTitle;
			}
			else
			{
				return;
			}
		}
		Invalidate(true);
	}

	/// <summary>
	/// Sets the content of a tab at the specified index.
	/// </summary>
	/// <param name="index">The index of the tab.</param>
	/// <param name="newContent">The new content control.</param>
	public void SetTabContent(int index, IWindowControl newContent)
	{
		lock (_tabLock)
		{
			if (index >= 0 && index < _tabPages.Count)
			{
				var oldContent = _tabPages[index].Content;
				oldContent.Dispose();
				_tabPages[index].Content = newContent;
				newContent.Container = this;
				newContent.Visible = index == _activeTabIndex;
			}
			else
			{
				return;
			}
		}
		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(true);
	}

	#endregion
	}
}
