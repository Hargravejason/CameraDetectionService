﻿using CameraDetectionService.Helpers;
using CameraDetectionService.Service.Helpers;
using CameraDetectionService.Service.Models;
using CameraDetectionService.Service.Services;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace CameraDetectionService;

public partial class ShellWindow : Window
{
  private IServiceProvider _serviceProvider;
  private readonly ILogger<ShellWindow> _logger;

  private TaskbarIcon _trayIcon;
  private Dictionary<string,OfflineWindow> _offlineWindows = new Dictionary<string, OfflineWindow>();

  // The monitoring service for all cameras
  private MonitorService _cameraMonitorService;

  private Dictionary<string, bool> _cameraStatusList = new();
  DateTime startingUp = DateTime.UtcNow;
  public ShellWindow(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
    _logger = _serviceProvider.GetRequiredService<ILogger<ShellWindow>>();

    InitializeComponent();

    // Hide the ShellWindow if you don’t want it visible
    this.Hide();

    // If you want direct reference to the tray icon in code:
    _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
    _trayIcon.ContextMenu = (ContextMenu)FindResource("TrayIconContextMenu");

    // Initialize the camera monitor
    _cameraMonitorService = new MonitorService(_serviceProvider.GetRequiredService<ILogger<MonitorService>>());

    // Subscribe to its status-changed event
    _cameraMonitorService.CameraStatusChanged += OnCameraStatusChanged;

    // set default config
    _cameraMonitorService.SetConfig(new Config());

    // Load the config file if it exists
    if (File.Exists("config.json"))
    {
      // Load the config file
      var config = SettingsHelper.LoadConfig();

      messages.Add(new("Connecting", $"Starting up...", BalloonIcon.Info));
      Dispatcher.Invoke(() => showBalloon());

      // If there are cameras to monitor, start the service
      if (config.Cameras.Count != 0)
      {
        //update the list
        _cameraStatusList = config.Cameras.ToDictionary(x => x.CameraName, x => false);
        Dispatcher.Invoke(() => UpdateContextMenu());

        //start the service
        _cameraMonitorService.SetConfig(config);
        _cameraMonitorService.StartMonitoring();
      }
    }

    // If there are no cameras to monitor, show the config window
    if (_cameraMonitorService.GetConfig().Cameras.Count == 0)
    {
      //no cameras to monitor so show the config window
      var configWindow = new ConfigurationWindow(_cameraMonitorService, _serviceProvider.GetRequiredService<ILogger<ConfigurationWindow>>());
      configWindow.ShowDialog();
    }
  }

  #region Shell / Taskbar items
  private void ConfigMenuItem_Click(object sender, RoutedEventArgs e)
  {
    // Show the config window. In that window, you might
    // let the user add/edit cameras, then call StartMonitoring again.
    var configWindow = new ConfigurationWindow(_cameraMonitorService, _serviceProvider.GetRequiredService<ILogger<ConfigurationWindow>>());
    configWindow.ShowDialog();

    if (configWindow.Saved)
    {
      //update the list
      _cameraStatusList = _cameraMonitorService.GetConfig().Cameras.ToDictionary(x => x.CameraName, x => false);
      Dispatcher.Invoke(() => UpdateContextMenu());

      //re-start the monitoring service
      startingUp = DateTime.UtcNow;
      _cameraMonitorService.StartMonitoring();

      messages.Add(new("Connecting", $"Re-connecting...", BalloonIcon.Info));
      Dispatcher.Invoke(() => showBalloon());
    }
  }

  private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
  {
    // Stop monitoring and shutdown
    _cameraMonitorService.StopMonitoring();
    Application.Current.Shutdown();
  }

  #endregion

  #region Taskbar Balloon Notification / Show Offline Items
  List<BalloonItem> messages = new ();
  private void OnCameraStatusChanged(object sender, CameraStatusChangedEventArgs e)
  {
    if (e.IsOffline && _cameraStatusList.ContainsKey(e.Camera.Config.CameraName) && startingUp <= DateTime.UtcNow.AddSeconds(-30))
    {
      // Show the offline window
      Dispatcher.Invoke(() => ShowHideOffline(e.Camera.Config.CameraName, true));

      //remove any messages for this camera
      messages.RemoveAll(messages => messages.Message.Contains(e.Camera.Config.CameraName));

      //add a new message for this camera
      messages.Add(new("Camera Offline", $"{e.Camera.Config.CameraName} is offline!", BalloonIcon.Error));
      Dispatcher.Invoke(() => showBalloon());
    }
    else if(startingUp <= DateTime.UtcNow.AddSeconds(-30))
    {
      //camera is back online hide the offline window
      Dispatcher.Invoke(() => ShowHideOffline(e.Camera.Config.CameraName, false));

      //remove any messages for this camera
      messages.RemoveAll(messages => messages.Message.Contains(e.Camera.Config.CameraName));

      //add a new message for this camera
      messages.Add(new("Camera Online", $"{e.Camera.Config.CameraName} is {(_cameraStatusList.ContainsKey(e.Camera.Config.CameraName) ? "online" : "back online")}!", BalloonIcon.Info));
      Dispatcher.Invoke(() => showBalloon());
    }

    if (!_cameraStatusList.ContainsKey(e.Camera.Config.CameraName))
      _cameraStatusList.Add(e.Camera.Config.CameraName, !e.IsOffline);
    else
      _cameraStatusList[e.Camera.Config.CameraName] = !e.IsOffline;

    Dispatcher.Invoke(() => UpdateContextMenu());

    if (_cameraStatusList.All(x => x.Value) && startingUp > DateTime.UtcNow.AddSeconds(-30))
    {
      if (!messages.Any(x => x.Title == "Cameras Online"))
      {
        messages.Clear();
        messages.Add(new("Cameras Online", "All cameras connected...", BalloonIcon.Info));
        Dispatcher.Invoke(() => showBalloon());
      }
    }
  }

  private void UpdateContextMenu()
  {
    var contextMenu = _trayIcon.ContextMenu;
    var itemstoKeep = new List<object>();
    foreach (var item in contextMenu.Items)
    {
      if(item is Separator || (item is MenuItem && ((MenuItem)item).IsEnabled))
        itemstoKeep.Add(item);
    }
    contextMenu.Items.Clear();  


    // Add camera status items
    foreach (var camera in _cameraStatusList)
    {
      var menuItem = new MenuItem
      {
        Header = camera.Key,
        Icon = new Image
        {
          Source = new BitmapImage(new Uri(camera.Value ? "pack://application:,,,/Resources/green_icon.ico" : "pack://application:,,,/Resources/red_icon.ico")),
          Width = 16,
          Height = 16
        },
        IsEnabled = false
      };
      contextMenu.Items.Add(menuItem);
    }

    foreach (var item in itemstoKeep)
      contextMenu.Items.Add(item);
  }

  Task? HideBalloon;
  CancellationTokenSource CancellationSource = new CancellationTokenSource();
  public void showBalloon()
  {
    //cancel any previous hide tasks
    _trayIcon.HideBalloonTip();
    CancellationSource.Cancel();
    HideBalloon = null;

    //get the first message to show
    var title = messages[0].Title;
    var message = $"{string.Join("\n", messages.Select(x => x.Message))}"; 
    var icon = messages[0].icon;

    //if there are multiple messages with different titles, show a warning icon and message
    if (title != "Connecting" && messages.Any(x=>x.Title != title))
    {
      title = "Multiple Cameras";
      icon = BalloonIcon.Warning;
    }

    // Show a balloon tip or log
    //_trayIcon.ShowBalloonTip(title, message, icon);
    var customBalloon = new CustomBalloon
    {
      Title = title,
      Message = message,
      Icon = icon
    };
    _trayIcon.ShowCustomBalloon(customBalloon, PopupAnimation.Slide, 5000);

    //create new cancellation token source
    CancellationSource = new CancellationTokenSource();
  }
  private void ShowHideOffline(string textToShow, bool show)
  {
    if (show)
    {
      this.Focus();
      _logger.LogWarning($"Camera offline: {textToShow}");
      SystemSounds.Exclamation.Play();

      // Create and show an OfflineWindow on each monitor
      foreach (var screen in System.Windows.Forms.Screen.AllScreens)
      {
        if (!_offlineWindows.ContainsKey(screen.DeviceName))
        {
          var offlineWindow = new OfflineWindow();

          // Show the Window
          offlineWindow.Show();

          // Get the DPI scale for the screen
          var source = PresentationSource.FromVisual(offlineWindow);
          double dpiX = 1.0, dpiY = 1.0;
          if (source != null)
          {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
          }

          // Adjust the position and size based on the DPI scale
          offlineWindow.Top = screen.WorkingArea.Top / dpiY + ((screen.WorkingArea.Height / dpiY / 2) - (offlineWindow.Height / 2));
          offlineWindow.Left = screen.WorkingArea.Left / dpiX + ((screen.WorkingArea.Width / dpiX / 2) - (offlineWindow.Width / 2));

          // Update the text on the screen
          offlineWindow.AddCameratoOffline(textToShow);

          // Add to the List
          _offlineWindows.Add(screen.DeviceName, offlineWindow);
        }
        else
        {
          // Update the text on the screen
          _offlineWindows[screen.DeviceName].AddCameratoOffline(textToShow);

          // Show the Window
          _offlineWindows[screen.DeviceName].Show();
        }
      }
    }
    else
    {
      _logger.LogWarning($"Camera back online: {textToShow}");
      SystemSounds.Asterisk.Play();

      // Hide and remove all OfflineWindow instances
      foreach (var offlineWindow in _offlineWindows.Values)
      {
        offlineWindow.RemoveCameraFromOffline(textToShow);
        offlineWindow.Hide();
      }
    }
  }
  #endregion

}

record BalloonItem(string Title, string Message, BalloonIcon icon);
