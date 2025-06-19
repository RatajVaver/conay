using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DataTemplates.Add(new ViewLocator());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine($"Conay v{Utils.Meta.GetVersion()}");

        BindingPlugins.DataValidators.RemoveAt(0);

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        ServiceCollection collection = new();

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

        ServiceProvider services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainView()
            {
                DataContext = services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}