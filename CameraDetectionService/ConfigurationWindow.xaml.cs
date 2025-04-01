using CameraDetectionService.Service.Helpers;
using CameraDetectionService.Service.Models;
using CameraDetectionService.Service.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace CameraDetectionService;

/// <summary>
/// Interaction logic for ConfigurationWindow.xaml
/// </summary>
public partial class ConfigurationWindow : Window
{
  private readonly ILogger<ConfigurationWindow> _logger;
  private readonly MonitorService _monitorService;
  public Config Config { get; set; }
  public bool Saved { get; set; }

  public event PropertyChangedEventHandler PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

  public ConfigurationWindow(MonitorService MonitorService, ILogger<ConfigurationWindow> logger)
  {
    InitializeComponent();
    _monitorService = MonitorService;

    Config = _monitorService.GetConfig();
    DataContext = this;
    _logger = logger;

    _logger.LogInformation("Configuration window opened");
  }

  private void AddCameraConfig_Click(object sender, RoutedEventArgs e)
  {
    Config.Cameras.Add(new CameraConfig());
  }

  private void RemoveCameraConfig_Click(object sender, RoutedEventArgs e)
  {
    var button = sender as Button;
    var cameraConfig = button.DataContext as CameraConfig;
    Config.Cameras.Remove(cameraConfig);
  }

  private async void Test_Click(object sender, RoutedEventArgs e)
  {
    var button = sender as Button;
    button.IsEnabled = false;
    button.Content = "Testing Connection";
    var cameraConfig = button.DataContext as CameraConfig;

    if(cameraConfig == null)
    {
      MessageBox.Show("Camera config is not set!");
      return;
    }

    _logger.LogInformation($"Testing camera connection: {cameraConfig.CameraName}");

    var cts = new CancellationTokenSource(10000);

    // Test the camera config

    var (success, message) = await CameraTestingHelper.TestConnectionStats(cameraConfig.RtspUrl, 5, cts.Token);
    //var connected = await _monitorService.TestConnection(new CameraModel() { Config = cameraConfig}, cts.Token);

    if (success)
    {
      _logger.LogInformation($"Connected to camera! {cameraConfig.CameraName}");
      MessageBox.Show($"Connected to camera!\n{message}");
    }
    else
    {
      _logger.LogInformation($"Failed to connect to camera! {cameraConfig.CameraName}");
      MessageBox.Show("Failed to connect to camera!");
    }
    button.IsEnabled = true;
    button.Content = "Test Connection";
  }


  private void CloseButton_Click(object sender, RoutedEventArgs e)
  {
    // If you updated camera configs, you might call:
    // _monitorService.StartMonitoring(updatedCameraList);
    _logger.LogInformation("Closing configuration window");
    this.Close();
  }

  private void btnSave_Click(object sender, RoutedEventArgs e)
  {
    _logger.LogInformation("Saving configuration");
    SettingsHelper.SaveConfig(Config);

    //overwrite the current config
    _monitorService.SetConfig(Config);

    Saved = true;

    //close the window
    this.Close();
  }

}