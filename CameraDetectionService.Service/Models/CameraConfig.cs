using System.Collections.ObjectModel;

namespace CameraDetectionService.Service.Models;

public class Config
{
  // Fault tolerance config
  public TimeSpan OfflineTimeout { get; set; } = TimeSpan.FromSeconds(1);

  public ObservableCollection<CameraConfig> Cameras { get; set; } = new ObservableCollection<CameraConfig>();

}

public class CameraConfig 
{
  public string CameraName { get; set; }
  public string Password { get; set; }
  public string Username { get; set; }

  public string IPAddress { get; set; }
  public int Port { get; set; }

  // If you have a specific RTSP URL, you can store it directly:
  public string RtspUrl { get; set; }
  public int RtspPort { get; set; }
}