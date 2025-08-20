using System;
using System.IO;
using System.Threading.Tasks;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Conay.Data;
using Conay.Factories;
using Conay.Services;
using Conay.Services.Logger;
using Conay.ViewModels;
using Conay.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conay;

public class App : Application
{
    private ILogger<App>? _logger;

    public override void Initialize()
    {
        AppContext.SetSwitch("System.Net.DisableIPv6", true);

        AvaloniaXamlLoader.Load(this);
        DataTemplates.Add(new ViewLocator());

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine($"Conay v{Utils.Meta.GetVersion()}");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SplashScreenView splashScreen = new()
            {
                DataContext = new SplashScreenViewModel()
            };

            desktop.MainWindow = splashScreen;
            splashScreen.Show();

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
                    InitializeApplication(desktop, splashScreen);
                }
                catch (Exception ex)
                {
                    LogFatalException("Failed to initialize application", ex);
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeApplication(IClassicDesktopStyleApplicationLifetime desktop,
        SplashScreenView splashScreen)
    {
        BindingPlugins.DataValidators.RemoveAt(0);

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        ServiceCollection collection = new();
        ConfigureServices(collection);

        ServiceProvider services = collection.BuildServiceProvider();
        _logger = services.GetRequiredService<ILogger<App>>();

        LauncherConfig launcherConfig = services.GetRequiredService<LauncherConfig>();
        if (launcherConfig.Data.UseCache)
        {
            if (Directory.Exists("cache"))
            {
                string[] cacheFiles = Directory.GetFiles("cache");
                foreach (string file in cacheFiles)
                {
                    FileInfo fi = new(file);
                    if (fi.LastWriteTime < DateTime.Now.AddDays(-7) && fi.Extension != ".json")
                        fi.Delete();
                }
            }

            ImageLoader.AsyncImageLoader.Dispose();
            ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader("cache/");
        }

        desktop.MainWindow = new MainView
        {
            DataContext = services.GetRequiredService<MainViewModel>()
        };

        desktop.MainWindow.Show();
        splashScreen.Close();
    }

    private static void ConfigureServices(IServiceCollection collection)
    {
        collection.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFile("logs/conay.log");
#if DEBUG
            logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Information);
#endif
        });

        collection.AddSingleton<Steam>();
        collection.AddSingleton<ModList>();
        collection.AddSingleton<Router>();
        collection.AddSingleton<HttpService>();
        collection.AddSingleton<NotifyService>();
        collection.AddSingleton<LaunchState>();
        collection.AddSingleton<LaunchWorker>();
        collection.AddSingleton<LauncherConfig>();
        collection.AddSingleton<GameConfig>();
        collection.AddSingleton<LocalPresets>();
        collection.AddSingleton<ServerList>();
        collection.AddSingleton<SelfUpdater>();

        collection.AddSingleton<ServerPresetFactory>();
        collection.AddSingleton<PresetSourceFactory>();
        collection.AddSingleton<ModItemFactory>();
        collection.AddSingleton<ModSourceFactory>();

        collection.AddSingleton<MainViewModel>();
        collection.AddTransient<LaunchViewModel>();
        collection.AddSingleton<FavoriteViewModel>();
        collection.AddSingleton<ServersViewModel>();
        collection.AddTransient<PresetsViewModel>();
        collection.AddSingleton<SettingsViewModel>();

        collection.AddSingleton<Func<Type, PageViewModel>>(x => type =>
        {
            if (!typeof(PageViewModel).IsAssignableFrom(type))
                throw new ArgumentException($"Type must be a PageViewModel: {type.FullName}");

            return (PageViewModel)x.GetRequiredService(type);
        });

        collection.AddSingleton<PageFactory>();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        LogFatalException("Unhandled application exception", (Exception)e.ExceptionObject);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogFatalException("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    private void LogFatalException(string message, Exception? exception)
    {
        try
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");

            if (_logger != null)
            {
                _logger.LogCritical(exception, "Fatal error ({Type})", message);
            }
            else
            {
                string logMessage = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} [Critical] {message}\n{exception}";
                File.AppendAllText("logs/fatal.log", logMessage + "\n");
            }
        }
        catch
        {
            Console.Error.WriteLine($"FATAL ERROR: {message}");
            Console.Error.WriteLine(exception);
        }
    }
}