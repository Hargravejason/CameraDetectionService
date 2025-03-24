using CameraDetectionService.Service.Models;
using CameraDetectionService.Service.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace CameraDetectionService;

/// <summary>
/// Interaction logic for ConfigurationWindow.xaml
/// </summary>
public partial class ConfigurationWindow : Window
{
  private readonly MonitorService _monitorService;
  public Config Config { get; set; }

  public ConfigurationWindow(MonitorService MonitorService)
  {
    InitializeComponent();
    _monitorService = MonitorService;

    Config = _monitorService.GetConfig();
    DataContext = this;
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

    var cts = new CancellationTokenSource(5000);

    // Test the camera config
    var connected = await _monitorService.TestConnection(new CameraModel() { Config = cameraConfig}, cts.Token);

    if (connected)
    {
      MessageBox.Show("Connected to camera!");
    }
    else
    {
      MessageBox.Show("Failed to connect to camera!");
    }
  }


  private void CloseButton_Click(object sender, RoutedEventArgs e)
  {
    // If you updated camera configs, you might call:
    // _monitorService.StartMonitoring(updatedCameraList);

    this.Close();
  }

  private void btnSave_Click(object sender, RoutedEventArgs e)
  {
    //save to the local config file
    File.WriteAllText("config.json", System.Text.Json.JsonSerializer.Serialize(Config));

    //overwrite the current config
    _monitorService.SetConfig(Config);

    //re-start the monitoring service
    _monitorService.StartMonitoring();

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
  }
}