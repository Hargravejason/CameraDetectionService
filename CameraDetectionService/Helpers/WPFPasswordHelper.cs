using CameraDetectionService.Service.Models;
using System.Windows.Controls;
using System.Windows;

namespace CameraDetectionService.Helpers;


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