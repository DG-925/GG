using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using GG.App.Services;
using GG.App.ViewModels;
using GG.App.Views;
using GG.Core.Logging;
using GG.Core.Services;
using GG.Core.Models;
using GG.Core.Storage;
using GG.Scanners;

namespace GG.App;

/// <summary>Application entry: sets up storage, logging and the main window.</summary>
public partial class App : Application
{
    private GameLibrary? _library;
    private SettingsService? _settings;

    /// <summary>
    /// "--smoke-test": start normally, close by itself once the window has rendered, and exit with
    /// code 1 if anything threw. CI runs this so startup crashes are caught before you see them.
    /// </summary>
    private bool _smokeTest;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _smokeTest = e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        // GG_DATA_DIR lets tests/CI use a throwaway data folder instead of %AppData%\GG.
        var dataDir = Environment.GetEnvironmentVariable("GG_DATA_DIR");
        var paths = string.IsNullOrWhiteSpace(dataDir) ? AppPaths.Default : new AppPaths(dataDir);
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

        // Registry/folder checks; quick, but kept off the UI thread.
        var installedLaunchers = await Task.Run(LauncherDetector.DetectInstalled);

        // Development step 1: fill a brand-new library with sample games so the grid has content.
        // They are tagged "sample-" and will be cleared automatically by the first real import.
        if (!_library.ExistedOnDisk)
        {
            _library.AddRange(SampleData.Create(installedLaunchers));
            await _library.SaveAsync();
        }
        else
        {
            // Libraries created by the first test build had samples for every launcher; drop the uninstalled ones.
            _library.RemoveWhere(g => SampleData.IsSample(g)
                && GameSourceInfo.Launchers.Contains(g.Source) && !installedLaunchers.Contains(g.Source));
        }

        var window = new MainWindow { DataContext = new MainViewModel(_library, _settings, paths, installedLaunchers,
            new DialogService(), ScanCoordinator.CreateDefault()) };
        MainWindow = window;
        if (_smokeTest)
        {
            window.ContentRendered += async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2)); // let bindings, hover styles and layout settle

                // Also exercise a real scan when the test data folder has a shortcut folder configured.
                if (window.DataContext is MainViewModel smokeVm)
                {
                    smokeVm.StartupScan();
                    while (smokeVm.IsScanning)
                        await Task.Delay(200);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                Log.Info("Smoke test passed.");
                Shutdown(0);
            };
        }
        window.Show();

        if (!_smokeTest && window.DataContext is MainViewModel vm)
            vm.StartupScan();
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
        if (_smokeTest)
        {
            e.Handled = true;
            Shutdown(1);
            return;
        }
        MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}\n\nDetails were written to the log in %AppData%\\GG\\logs.",
            "GG", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
