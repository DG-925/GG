using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using GG.App.ViewModels;
using GG.App.Views;
using GG.Core.Logging;
using GG.Core.Services;
using GG.Core.Storage;

namespace GG.App;

/// <summary>Application entry: sets up storage, logging and the main window.</summary>
public partial class App : Application
{
    private GameLibrary? _library;
    private SettingsService? _settings;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        var paths = AppPaths.Default;
        try
        {
            paths.EnsureCreated();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"GG could not create its data folder:\n{paths.Root}\n\n{ex.Message}",
                "GG", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        Log.Initialize(paths.LogFile);
        Log.Info($"GG {Assembly.GetExecutingAssembly().GetName().Version} starting. Data folder: {paths.Root}");

        _settings = new SettingsService(paths);
        await _settings.LoadAsync();

        _library = new GameLibrary(paths);
        await _library.LoadAsync();

        // Development step 1: fill a brand-new library with sample games so the grid has content.
        // They are tagged "sample-" and will be cleared automatically by the first real import.
        if (!_library.ExistedOnDisk)
        {
            _library.AddRange(SampleData.Create());
            await _library.SaveAsync();
        }

        var window = new MainWindow { DataContext = new MainViewModel(_library, _settings, paths) };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Write any pending (debounced) saves before the process ends.
        try
        {
            Task.Run(async () =>
            {
                if (_library is not null) await _library.FlushAsync();
                if (_settings is not null) await _settings.FlushAsync();
            }).Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            Log.Error("Saving on exit failed", ex);
        }

        Log.Info("GG exiting.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}\n\nDetails were written to the log in %AppData%\\GG\\logs.",
            "GG", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
