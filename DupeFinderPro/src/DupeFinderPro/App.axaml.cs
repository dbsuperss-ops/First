using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Infrastructure.Detection;
using DupeFinderPro.Infrastructure.FileSystem;
using DupeFinderPro.Infrastructure.Hashing;
using DupeFinderPro.Infrastructure.Organize;
using DupeFinderPro.Infrastructure.Storage;
using DupeFinderPro.ViewModels;
using DupeFinderPro.ViewModels.Organize;
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

            // Stop all watchers on app exit
            desktop.Exit += (_, _) => _services.GetRequiredService<IWatcherService>().StopAll();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        // Infrastructure — duplicate detection
        sc.AddSingleton<IHashingService,       HashingService>();
        sc.AddSingleton<IFileScanner,          FileScanner>();
        sc.AddSingleton<IDuplicateDetector,    DuplicateDetector>();
        sc.AddSingleton<IFileOperationService, FileOperationService>();
        sc.AddSingleton<IAutoSelectStrategy,   PriorityAutoSelectStrategy>();
        sc.AddSingleton<IScanJobRepository,    InMemoryScanJobRepository>();

        // Infrastructure — organize
        sc.AddSingleton<IScenarioRepository,       JsonScenarioRepository>();
        sc.AddSingleton<IOrganizeLogRepository,    JsonOrganizeLogRepository>();
        sc.AddSingleton<IClassifyRecordRepository, JsonClassifyRecordRepository>();
        sc.AddSingleton<IClassifyService,          ClassifyService>();
        sc.AddSingleton<IWatcherService,           WatcherService>();
        sc.AddSingleton<ISchedulerService,         WindowsSchedulerService>();

        // Application
        sc.AddSingleton<ScanOrchestrator>();
        sc.AddSingleton<CleanupOrchestrator>();
        sc.AddSingleton<ScanJobService>();
        sc.AddSingleton<OrganizeOrchestrator>();

        // ViewModels — duplicate detection
        sc.AddSingleton<DashboardViewModel>();
        sc.AddSingleton<NewScanViewModel>();
        sc.AddSingleton<ScanHistoryViewModel>();
        sc.AddSingleton<ResultsViewModel>();

        // ViewModels — organize
        sc.AddSingleton<ScenarioEditViewModel>();
        sc.AddSingleton<ScenarioListViewModel>();
        sc.AddSingleton<OrganizeViewModel>();
        sc.AddSingleton<OrganizeLogViewModel>();
        sc.AddSingleton<OrganizeStatisticsViewModel>();

        sc.AddSingleton<MainWindowViewModel>();

        return sc.BuildServiceProvider();
    }
}
