using System.Collections.ObjectModel;
using TerminalMail.Models;

namespace TerminalMail.ViewModels;

/// <summary>Top-level view model: folders, the current folder's messages, and the selection.</summary>
public sealed class MailboxViewModel : ViewModelBase
{
    public IReadOnlyList<Folder> Folders { get; }

    /// <summary>Messages of the currently selected folder. Bound to the table via a data source.</summary>
    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    private Folder? _selectedFolder;
    public Folder? SelectedFolder
    {
        get => _selectedFolder;
        private set => SetProperty(ref _selectedFolder, value);
    }

    private MessageViewModel? _selectedMessage;
    public MessageViewModel? SelectedMessage
    {
        get => _selectedMessage;
        private set => SetProperty(ref _selectedMessage, value);
    }

    public MailboxViewModel(IReadOnlyList<Folder> folders)
    {
        Folders = folders;
        if (folders.Count > 0) LoadFolder(folders[0]);
    }

    /// <summary>Switch folders and refill <see cref="Messages"/> (raises CollectionChanged).</summary>
    public void SelectFolder(Folder folder)
    {
        LoadFolder(folder);
    }

    private void LoadFolder(Folder folder)
    {
        SelectedFolder = folder;
        Messages.Clear();
        foreach (var m in folder.Messages)
            Messages.Add(new MessageViewModel(m));
        // Set initial selection without marking read — MarkRead happens only via SelectMessage.
        _selectedMessage = Messages.Count > 0 ? Messages[0] : null;
        OnPropertyChanged(nameof(SelectedMessage));
    }

    /// <summary>Select (and mark read) a message; null clears the reading pane.</summary>
    public void SelectMessage(MessageViewModel? message)
    {
        SelectedMessage = message;
        message?.MarkRead();
    }
}
