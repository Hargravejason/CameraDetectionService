using System.IO;
using System.Windows;
using CameraDetectionService.Service.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CameraDetectionService;

public partial class App : Application
{
  private IServiceProvider _serviceProvider;

  public App()
  {
    var serviceCollection = new ServiceCollection();
    ConfigureServices(serviceCollection);
    _serviceProvider = serviceCollection.BuildServiceProvider();
  }

  private void ConfigureServices(IServiceCollection services)
  {
    // Load configuration from appsettings.json
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    // Configure logging
    services.AddLogging(configure =>
    {
      configure.AddProvider(new FileLoggerProvider(Path.Combine(Directory.GetCurrentDirectory(),"Logs"), configuration));
    });

    // Add other services here
  }
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);
    // Perform any app-level initialization or logging

    // Get the logger service
    var logger = _serviceProvider.GetRequiredService<ILogger<ShellWindow>>();
    logger.LogInformation("Application started.");

    // Create and show the ShellWindow
    var shellWindow = new ShellWindow(_serviceProvider);
    
  }

  protected override void OnExit(ExitEventArgs e)
  {
    // Get the logger service
    var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
    logger.LogInformation("Application exiting.");

    // Cleanup or finalize resources here
    base.OnExit(e);
  }
}
