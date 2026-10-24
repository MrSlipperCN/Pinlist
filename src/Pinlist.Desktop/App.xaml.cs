using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace Pinlist.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string ReleaseRepositoryUrl = "https://github.com/MrSlipperCN/Pinlist";
    private const string MutexName = @"Local\Pinlist.Desktop.SingleInstance";
    private const string ActivationEventName = @"Local\Pinlist.Desktop.Activate";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private bool _ownsMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first process is still starting and will show itself normally.
            }
            Shutdown();
            return;
        }
        _ownsMutex = true;

        base.OnStartup(e);
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

#if !DEBUG
        _ = DownloadAvailableUpdateAsync();
#endif

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationCancellation = new CancellationTokenSource();
        _ = WaitForActivationAsync(mainWindow, _activationCancellation.Token);
    }

    private static async Task DownloadAvailableUpdateAsync()
    {
        try
        {
            var manager = new UpdateManager(
                new GithubSource(ReleaseRepositoryUrl, accessToken: null, prerelease: false));
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;

            await manager.DownloadUpdatesAsync(update);
            AppLog.Write($"Velopack update {update.TargetFullRelease.Version} downloaded; it will apply on the next launch.");
        }
        catch (NotInstalledException)
        {
            // Release update checks are meaningful only from a Velopack installation.
        }
        catch (Exception exception)
        {
            AppLog.Write($"Velopack update check failed: {exception}");
        }
    }

    private async Task WaitForActivationAsync(MainWindow mainWindow, CancellationToken cancellationToken)
    {
        if (_activationEvent is null) return;
        await Task.Run(() =>
        {
            var handles = new WaitHandle[] { _activationEvent, cancellationToken.WaitHandle };
            while (!cancellationToken.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(handles) != 0) break;
                Dispatcher.Invoke(mainWindow.ShowFromExternalActivation);
            }
        }, cancellationToken);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

