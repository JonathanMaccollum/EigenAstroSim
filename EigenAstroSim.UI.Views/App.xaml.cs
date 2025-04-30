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
        this.MainWindow = new MainWindow()
        {
            DataContext = new MainViewModel(_simulationEngine)
        };
        this.MainWindow.Show();

        _timerService.Start();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        (_timerService as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}