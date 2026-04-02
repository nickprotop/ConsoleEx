using ClinicFlow.Data;
using ClinicFlow.UI;
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;

namespace ClinicFlow;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var patients = SeedData.CreatePatients();

            var windowSystem = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer));

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                windowSystem.Shutdown(0);
            };

            var clinicFlow = new ClinicFlowWindow(windowSystem, patients);
            clinicFlow.Create();

            await Task.Run(() => windowSystem.Run());
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            ExceptionFormatter.WriteException(ex);
            return 1;
        }
    }
}
