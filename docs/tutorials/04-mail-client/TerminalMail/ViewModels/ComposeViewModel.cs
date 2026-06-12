namespace TerminalMail.ViewModels;

/// <summary>Form state for the compose dialog (two-way bound to inputs).</summary>
public sealed class ComposeViewModel : ViewModelBase
{
    private string _to = "";
    public string To { get => _to; set => SetProperty(ref _to, value); }

    private string _subject = "";
    public string Subject { get => _subject; set => SetProperty(ref _subject, value); }

    private string _body = "";
    public string Body { get => _body; set => SetProperty(ref _body, value); }
}
