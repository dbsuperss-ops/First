using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Infrastructure.Detection;
using DupeFinderPro.Infrastructure.FileSystem;
using DupeFinderPro.Infrastructure.Hashing;
using DupeFinderPro.Infrastructure.Storage;
using DupeFinderPro.ViewModels;
using DupeFinderPro.Views;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApp = Avalonia.Application;

namespace DupeFinderPro;

public sealed partial class App : AvaloniaApp
{
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        // Infrastructure
        sc.AddSingleton<IHashingService,      HashingService>();
        sc.AddSingleton<IFileScanner,         FileScanner>();
        sc.AddSingleton<IDuplicateDetector,   DuplicateDetector>();
        sc.AddSingleton<IFileOperationService, FileOperationService>();
        sc.AddSingleton<IAutoSelectStrategy,  PriorityAutoSelectStrategy>();
        sc.AddSingleton<IScanJobRepository,   InMemoryScanJobRepository>();

        // Application
        sc.AddSingleton<ScanOrchestrator>();
        sc.AddSingleton<CleanupOrchestrator>();
        sc.AddSingleton<ScanJobService>();

        // ViewModels
        sc.AddSingleton<DashboardViewModel>();
        sc.AddSingleton<NewScanViewModel>();
        sc.AddSingleton<ScanHistoryViewModel>();
        sc.AddSingleton<ResultsViewModel>();
        sc.AddSingleton<MainWindowViewModel>();

        return sc.BuildServiceProvider();
    }
}
