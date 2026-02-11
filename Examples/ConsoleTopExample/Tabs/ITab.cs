using ConsoleTopExample.Stats;
using SharpConsoleUI.Controls;

namespace ConsoleTopExample.Tabs;

internal interface ITab
{
    string Name { get; }
    string PanelControlName { get; }
    IWindowControl BuildPanel(SystemSnapshot initialSnapshot, int windowWidth);
    void UpdatePanel(SystemSnapshot snapshot);
    void HandleResize(int newWidth, int newHeight);
}
