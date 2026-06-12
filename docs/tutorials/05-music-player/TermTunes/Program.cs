using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using TermTunes.Data;
using TermTunes.UI;
using TermTunes.ViewModels;

var ws = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: new ConsoleWindowSystemOptions(
        ShowTopPanel: false,
        ShowBottomPanel: false));

var player = new PlayerViewModel(SamplePlaylist.Build());
new PlayerWindow(ws, player).Create();

return ws.Run();
