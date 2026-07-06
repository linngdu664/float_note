using System.IO;
using System.Windows;
using System.Windows.Threading;
using FloatNote.Services;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class App : System.Windows.Application
{
    private AppStorage? _storage;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;
    private FloatingBallWindow? _floatingBallWindow;
    private CurrentTodosPreviewWindow? _previewWindow;
    private MainViewModel? _viewModel;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        EnsureWindowsEnvironment();
        RegisterCrashLogging();

        _storage = new AppStorage();
        var appState = await _storage.LoadAsync();
        ThemeService.Apply(appState.IsDarkTheme);
        var viewModel = new MainViewModel(appState, _storage);
        _viewModel = viewModel;

        _mainWindow = new MainWindow(viewModel, ShutdownApplication);
        _previewWindow = new CurrentTodosPreviewWindow(viewModel);
        _floatingBallWindow = new FloatingBallWindow(viewModel, _previewWindow, ToggleMainWindow, ShutdownApplication);
        _mainWindow.Activated += (_, _) => BringFloatingBallAboveMainWindow();
        _mainWindow.PreviewMouseDown += (_, _) => BringFloatingBallAboveMainWindow();
        _mainWindow.PreviewMouseUp += (_, _) => BringFloatingBallAboveMainWindow();
        _trayService = new TrayService(
            showWindow: ShowMainWindow,
            hideWindow: () => _mainWindow?.Hide(),
            exitApplication: ShutdownApplication);

        _mainWindow.Show();
        _floatingBallWindow.Show();
    }

    private static void RegisterCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteCrashLog(exception);
            }
        };

        Current.DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.Handled = false;
        };
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FloatNote");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void EnsureWindowsEnvironment()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            return;
        }

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            Environment.SetEnvironmentVariable("windir", systemRoot);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        BringFloatingBallAboveMainWindow();
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
            BringFloatingBallAboveMainWindow();
            return;
        }

        ShowMainWindow();
    }

    private void BringFloatingBallAboveMainWindow()
    {
        if (_floatingBallWindow is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () => _floatingBallWindow.BringAboveMainWindow(),
            DispatcherPriority.ApplicationIdle);
    }

    private async void ShutdownApplication()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        _trayService?.Dispose();
        _trayService = null;

        try
        {
            if (_viewModel is not null)
            {
                await _viewModel.SaveNowAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
        catch (Exception exception)
        {
            WriteCrashLog(exception);
        }

        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        _floatingBallWindow?.Close();
        _previewWindow?.Close();
        Shutdown(0);
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
