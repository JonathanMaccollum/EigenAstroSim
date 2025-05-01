using System.Configuration;
using System.Data;
using System.Windows;
using EigenAstroSim.UI;
using EigenAstroSim.UI.Services;

namespace EigenAstroSim.UI.Views;

public partial class App : Application
{
    private static SimulationEngine _simulationEngine = new SimulationEngine();
    private readonly SimulationTimerService _timerService = new SimulationTimerService(_simulationEngine);
    protected override void OnStartup(StartupEventArgs e)
    {
        Logger.log("Application starting");
        Logger.log("Creating main window");
        this.MainWindow = new MainWindow()
        {
            DataContext = new MainViewModel(_simulationEngine)
        };
        Logger.log("Showing main window");
        this.MainWindow.Show();
        Logger.log("Starting timer service");
        _timerService.Start();
        Logger.log("Application startup complete");
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Logger.log("Application exiting");
        (_timerService as IDisposable)?.Dispose();
        Logger.dispose();  // Dispose the logger
        base.OnExit(e);
    }
}