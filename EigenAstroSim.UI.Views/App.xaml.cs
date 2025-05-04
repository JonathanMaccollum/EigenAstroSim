using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EigenAstroSim;
using EigenAstroSim.Domain;
using EigenAstroSim.UI;
using Serilog;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System.IO;

namespace EigenAstroSim.UI.Views;

public partial class App : Application
{
    private static ILoggerFactory _loggerFactory;
    private static SimulationEngine _simulationEngine = new SimulationEngine();
    private readonly SimulationTimer _timer = new SimulationTimer(_simulationEngine);

    protected override void OnStartup(StartupEventArgs e)
    {
        SetupExceptionHandling();
        var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EigenAstroSim",
                "Logs");
        Directory.CreateDirectory(appDataPath);
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(appDataPath, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        Log.Logger = serilogLogger;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(serilogLogger, dispose: false);
        });
        _loggerFactory.ConfigureFSharpLogging();
        Logger.Info("Application starting");

        this.MainWindow = new MainWindow()
        {
            DataContext = new MainViewModel(_simulationEngine)
        };

        Logger.Info("Showing main window");
        this.MainWindow.Show();

        Logger.Info("Starting timer service");
        _timer.Start();

        Logger.Info("Application startup complete");
    }

    private void SetupExceptionHandling()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Logger.FatalException(e.Exception, "Unhandled exception in UI thread");
            MessageBox.Show(
                $"An unexpected error occurred. The application will now close.\n\nError details have been logged.\n\nError: {e.Exception.Message}",
                "Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(1);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"A critical error occurred while handling another error.\nOriginal error: {e.Exception.Message}\nHandler error: {ex.Message}",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var exception = e.ExceptionObject as Exception;
            Logger.FatalException(
                exception ?? new Exception($"Unknown error: {e.ExceptionObject}"),
                $"Unhandled exception in non-UI thread. IsTerminating: {e.IsTerminating}");
            if (!e.IsTerminating)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"A serious error occurred in a background thread. The application may become unstable.\n\nError details have been logged.",
                        "Background Thread Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }
        catch
        {
            // Nothing we can do if we can't log - the app is already crashing
        }
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            Logger.FatalException(e.Exception, "Unobserved task exception");
            e.SetObserved();
            Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    "A background task encountered an error. The application will continue, but some operations may have failed.\n\nError details have been logged.",
                    "Background Task Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }
        catch
        {
            // Nothing we can do if we can't log
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info($"Application exiting with code: {e.ApplicationExitCode}");
        (_timer as IDisposable)?.Dispose();
        Log.CloseAndFlush();
        _loggerFactory?.Dispose();
        base.OnExit(e);
    }
}