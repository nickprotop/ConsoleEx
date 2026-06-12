using TerminalMail.Models;

namespace TerminalMail.ViewModels;

/// <summary>Bindable wrapper around a <see cref="Message"/>.</summary>
public sealed class MessageViewModel : ViewModelBase
{
    private readonly Message _model;

    public MessageViewModel(Message model) => _model = model;

    public string From => _model.From;
    public string Subject => _model.Subject;
    public string Body => _model.Body;
    public DateTime Date => _model.Date;

    public bool IsRead
    {
        get => _model.IsRead;
        private set { if (_model.IsRead != value) { _model.IsRead = value; OnPropertyChanged(); } }
    }

    public bool IsFlagged => _model.IsFlagged;

    /// <summary>Markup shown in the reading-pane header.</summary>
    public string HeaderText =>
        $"[grey70]From:[/] {From}\n[grey70]Subject:[/] {Subject}\n[grey70]Date:[/] {Date:ddd, dd MMM yyyy HH:mm}";

    /// <summary>Short date used in the message table.</summary>
    public string ShortDate =>
        Date.Date == DateTime.Today ? Date.ToString("HH:mm") : Date.ToString("MMM dd");

    public void MarkRead() => IsRead = true;
}
