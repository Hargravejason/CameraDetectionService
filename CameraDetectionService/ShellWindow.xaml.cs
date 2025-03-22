using CameraDetectionService.Service.Models;
using CameraDetectionService.Service.Services;
using Hardcodet.Wpf.TaskbarNotification;
using System.IO;
using System.Media;
using System.Windows;

namespace CameraDetectionService;

public partial class ShellWindow : Window
{
  private TaskbarIcon _trayIcon;

  // The monitoring service for all cameras
  private MonitorService _cameraMonitorService;

  public ShellWindow()
  {
    InitializeComponent();

    // Hide the ShellWindow if you don’t want it visible
    this.Hide();

    // If you want direct reference to the tray icon in code:
    _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

    // Initialize the camera monitor
    _cameraMonitorService = new MonitorService();

    // Subscribe to its status-changed event
    _cameraMonitorService.CameraStatusChanged += OnCameraStatusChanged;

    _cameraMonitorService.SetConfig(new Config());


    if (File.Exists("config.json"))
    {
      var config = System.Text.Json.JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"));
      if (config != null)
        //start the service
        _cameraMonitorService.SetConfig(config);
      _cameraMonitorService.StartMonitoring();
    }

    if (_cameraMonitorService.GetConfig().Cameras.Count == 0)
    {
      //no cameras to monitor so show the config window
      var configWindow = new ConfigurationWindow(_cameraMonitorService);
      configWindow.ShowDialog();
    }
  }

  private void OnCameraStatusChanged(object sender, CameraStatusChangedEventArgs e)
  {
    // e.IsOffline == true if the camera went offline
    // e.Camera contains camera info

    if (e.IsOffline)
    {

      Dispatcher.Invoke(() => ShowHideOffline(e.Camera.Config.CameraName, true));
      // Show a balloon tip or log
      _trayIcon.ShowBalloonTip(
          "Camera Offline",
          $"{e.Camera.Config.CameraName} is offline!",
          BalloonIcon.Error
      );
      Task.Run(async () =>
      {
        await Task.Delay(5000);
        _trayIcon.HideBalloonTip();
      });
    }
    else
    {
      Dispatcher.Invoke(() => ShowHideOffline(e.Camera.Config.CameraName, false));
      _trayIcon.ShowBalloonTip(
          "Camera Online",
          $"{e.Camera.Config.CameraName} is back online!",
          BalloonIcon.Info
      );
      Task.Run(async () =>
      {
        await Task.Delay(5000);
        _trayIcon.HideBalloonTip();
      });
    }
  }

  public OfflineWindow? _offlineWindow { get; set; }
  private void ShowHideOffline(string textToShow, bool show)
  {
    if (show)
    {
      SystemSounds.Exclamation.Play();
      _offlineWindow = new OfflineWindow(string.Format("{0} is offline!", textToShow));
      _offlineWindow.Show();
    }
    else
    {
      SystemSounds.Asterisk.Play();
      if (_offlineWindow != null)
      {
        _offlineWindow.Hide();
        _offlineWindow = null;
      }
    }
  }

  private void ConfigMenuItem_Click(object sender, RoutedEventArgs e)
  {
    // Show the config window. In that window, you might
    // let the user add/edit cameras, then call StartMonitoring again.
    var configWindow = new ConfigurationWindow(_cameraMonitorService);
    configWindow.ShowDialog();
  }

  private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
  {
    // Stop monitoring and shutdown
    _cameraMonitorService.StopMonitoring();
    Application.Current.Shutdown();
  }
}
