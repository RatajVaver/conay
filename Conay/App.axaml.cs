using System;
using System.IO;
using System.Threading.Tasks;
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

            Dispatcher.UIThread.Post(() => { InitializeApplication(desktop, splashScreen); });
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

        desktop.MainWindow = new MainView()
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
                _logger.LogCritical(exception, message);
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