﻿using CameraDetectionService.Service.Helpers;
using CameraDetectionService.Service.Models;
using CameraDetectionService.Service.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security;
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
    var cameraConfig = button.DataContext as CameraConfig;

    if(cameraConfig == null)
    {
      MessageBox.Show("Camera config is not set!");
      return;
    }

    _logger.LogInformation($"Testing camera connection: {cameraConfig.CameraName}");

    var cts = new CancellationTokenSource(5000);

    // Test the camera config
    var connected = await _monitorService.TestConnection(new CameraModel() { Config = cameraConfig}, cts.Token);

    if (connected)
    {
      _logger.LogInformation($"Connected to camera! {cameraConfig.CameraName}");
      MessageBox.Show("Connected to camera!");
    }
    else
    {
      _logger.LogInformation($"Failed to connect to camera! {cameraConfig.CameraName}");
      MessageBox.Show("Failed to connect to camera!");
    }
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


public static class PasswordHelper
{
  public static readonly DependencyProperty BoundPassword =
      DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordHelper), new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

  public static readonly DependencyProperty BindPassword =
      DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordHelper), new PropertyMetadata(false, OnBindPasswordChanged));

  private static readonly DependencyProperty UpdatingPassword =
      DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordHelper), new PropertyMetadata(false));

  public static string GetBoundPassword(DependencyObject dp)
  {
    return (string)dp.GetValue(BoundPassword);
  }

  public static void SetBoundPassword(DependencyObject dp, string value)
  {
    dp.SetValue(BoundPassword, value);
  }

  public static bool GetBindPassword(DependencyObject dp)
  {
    return (bool)dp.GetValue(BindPassword);
  }

  public static void SetBindPassword(DependencyObject dp, bool value)
  {
    dp.SetValue(BindPassword, value);
  }

  private static void OnBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
  {
    if (dp is PasswordBox passwordBox)
    {
      passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

      if (!(bool)dp.GetValue(UpdatingPassword))
      {
        passwordBox.Password = (string)e.NewValue;
      }

      passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
    }
  }

  private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
  {
    if (dp is PasswordBox passwordBox)
    {
      if ((bool)e.OldValue)
      {
        passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
      }

      if ((bool)e.NewValue)
      {
        passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
      }
    }
  }

  private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
  {
    var passwordBox = sender as PasswordBox;
    passwordBox.SetValue(UpdatingPassword, true);
    SetBoundPassword(passwordBox, passwordBox.Password);
    passwordBox.SetValue(UpdatingPassword, false);

    if (passwordBox.DataContext is CameraConfig cameraConfig)
    {
      cameraConfig.Password = passwordBox.Password;
    }
  }
}