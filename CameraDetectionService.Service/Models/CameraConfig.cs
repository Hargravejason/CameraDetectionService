using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CameraDetectionService.Service.Models;

public class Config
{
  // Fault tolerance config
  public TimeSpan OfflineTimeout { get; set; } = TimeSpan.FromSeconds(1);

  public ObservableCollection<CameraConfig> Cameras { get; set; } = new ObservableCollection<CameraConfig>();

}

public class CameraConfig : INotifyPropertyChanged
{
  private string _cameraName;
  private string _password;
  private string _username;
  private string _ipAddress;
  private int _port;
  private string _rtspUrl;
  private int _rtspPort;

  public string CameraName
  {
    get => _cameraName;
    set
    {
      _cameraName = value;
      OnPropertyChanged();
    }
  }

  public string Password
  {
    get => _password;
    set
    {
      _password = value;
      OnPropertyChanged();
    }
  }

  public string Username
  {
    get => _username;
    set
    {
      _username = value;
      OnPropertyChanged();
    }
  }

  public string IPAddress
  {
    get => _ipAddress;
    set
    {
      _ipAddress = value;
      OnPropertyChanged();
    }
  }

  public int Port
  {
    get => _port;
    set
    {
      _port = value;
      OnPropertyChanged();
    }
  }

  public string RtspUrl
  {
    get => _rtspUrl;
    set
    {
      _rtspUrl = value;
      OnPropertyChanged();
    }
  }

  public int RtspPort
  {
    get => _rtspPort;
    set
    {
      _rtspPort = value;
      OnPropertyChanged();
    }
  }

  public event PropertyChangedEventHandler PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}