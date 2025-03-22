using RtspClientSharp;
using RtspClientSharp.RawFrames;

namespace CameraDetectionService.Service.Models;

public class CameraModel
{
  public CameraConfig Config { get; set; }
  public RtspClient Client { get; set; }

  public TimeSpan OfflineTimeout { get; set; } = TimeSpan.FromSeconds(1);

  public bool Online { get; set; }
  public RawFrame? LastFrame { get; set; }
  public DateTime LastFrameTime { get; set; } = DateTime.MinValue;
}
