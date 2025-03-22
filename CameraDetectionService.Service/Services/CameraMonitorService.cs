using CameraDetectionService.Service.Models;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using RtspClientSharp;               // from RtspClientSharp package
using RtspClientSharp.RawFrames;     // needed for FrameReceived event

namespace CameraDetectionService.Service.Services;


/// <summary>
/// Monitors multiple cameras using RtspClientSharp for continuous streaming,
/// with a fallback RTSP handshake if frames stop arriving.
/// </summary>
public class CameraMonitorService
{
  /// <summary>
  /// Raised when a camera's status changes from online->offline or vice versa.
  /// </summary>
  public event EventHandler<CameraStatusChangedEventArgs> CameraStatusChanged;

  private readonly List<CameraConfig> _cameras = new();
  private CancellationTokenSource _cts;

  // Tracks whether each camera is currently considered online (true) or offline (false).
  private readonly ConcurrentDictionary<string, bool> _cameraOnlineStatus
      = new ConcurrentDictionary<string, bool>();

  // Holds the active RtspClient for each camera (so we can disconnect if needed).
  private readonly ConcurrentDictionary<string, RtspClient> _rtspClients
      = new ConcurrentDictionary<string, RtspClient>();

  // Tracks the last time we received a frame for each camera.
  private readonly ConcurrentDictionary<string, DateTime> _lastFrameTime
      = new ConcurrentDictionary<string, DateTime>();

  /// <summary>
  /// Start monitoring the given cameras (stop any previous monitoring).
  /// </summary>
  public void StartMonitoring(IEnumerable<CameraConfig> cameraConfigs)
  {
    StopMonitoring();

    _cameras.Clear();
    _cameras.AddRange(cameraConfigs);

    _cts = new CancellationTokenSource();

    // For each camera, start an async monitor loop
    foreach (var cam in _cameras)
    {
      // Default to "offline" until we confirm otherwise
      _cameraOnlineStatus[cam.CameraName] = false;
      _rtspClients.TryRemove(cam.CameraName, out _);
      _lastFrameTime[cam.CameraName] = DateTime.MinValue;

      _ = MonitorCameraAsync(cam, _cts.Token);
    }
  }

  /// <summary>
  /// Gracefully stop monitoring all cameras.
  /// </summary>
  public void StopMonitoring()
  {
    if (_cts != null && !_cts.IsCancellationRequested)
    {
      _cts.Cancel();
    }

    foreach (var kvp in _rtspClients)
    {
      try
      {
        kvp.Value?.Dispose();
      }
      catch { /* ignore */ }
    }

    _rtspClients.Clear();
  }

  /// <summary>
  /// Async loop that attempts to connect and stream from one camera.
  /// If frames stop arriving beyond OfflineTimeout, does a fallback check.
  /// Then raises offline event if truly unreachable.
  /// </summary>
  private async Task MonitorCameraAsync(CameraConfig camera, CancellationToken token)
  {
    while (!token.IsCancellationRequested)
    {
      // Attempt to connect & stream
      bool connected = await TryConnectAndStream(camera, token);
      if (!connected)
      {
        // Mark offline if we were online
        if (_cameraOnlineStatus[camera.CameraName])
        {
          _cameraOnlineStatus[camera.CameraName] = false;
          RaiseStatusChanged(camera, isOffline: true);
        }

        // Wait a bit before retry
        await Task.Delay(1000, token);
        continue;
      }

      // If we got here, we connected and are streaming frames
      // Wait until the streaming loop ends (due to error or offline detection),
      // then loop again to reconnect.
      await Task.Delay(500, token);
    }
  }

  /// <summary>
  /// Attempts to create an RtspClient, connect, and read frames.
  /// Returns true if we connected successfully; false otherwise.
  /// </summary>
  private async Task<bool> TryConnectAndStream(CameraConfig camera, CancellationToken token)
  {
    try
    {
      // Build connection parameters for RtspClientSharp
      var uri = new Uri(camera.RtspUrl);
      var connectionParams = new ConnectionParameters(uri)
      {
        // If you have camera credentials:
        // Credentials = new NetworkCredential("username", "password"),
        // etc.
        // You can also set various buffer settings, 
        // receive timeout, etc.
      };

      var rtspClient = new RtspClient(connectionParams);

      // Subscribe to frame-received event
      rtspClient.FrameReceived += (sender, frame) =>
      {
        // Update lastFrameTime on each frame
        _lastFrameTime[camera.CameraName] = DateTime.UtcNow;

        // If we were offline, mark online
        if (!_cameraOnlineStatus[camera.CameraName])
        {
          _cameraOnlineStatus[camera.CameraName] = true;
          RaiseStatusChanged(camera, isOffline: false);
        }

        // You can also store the last frame if you need:
        // camera.LastFrame = ConvertFrameToBitmapImage(frame);
      };

      

      //// If connection is lost:
      //rtspClient.ConnectionLost += (sender, ex) =>
      //{
      //  // This typically fires if the socket or stream breaks unexpectedly.
      //  // We'll handle final offline detection in the main loop though,
      //  // but let's do an immediate check:
      //  CheckAndHandleOffline(camera);
      //};

      //// If an error occurs:
      //rtspClient.ConnectionExceptionRaised += (sender, ex) =>
      //{
      //  // We'll do offline detection below as well
      //  CheckAndHandleOffline(camera);
      //};

      // Store the client in dictionary
      _rtspClients[camera.CameraName] = rtspClient;

      // Actually connect
      await rtspClient.ConnectAsync(token);

      // Mark the last frame time to "now," because we just connected
      _lastFrameTime[camera.CameraName] = DateTime.UtcNow;

      // Start a mini-loop to watch for frame dropouts
      _ = Task.Run(() => FrameWatcherLoop(camera, rtspClient, token));

      // If connect succeeded, we assume "true"
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Continuously checks if frames have stopped arriving beyond OfflineTimeout.
  /// If so, tries a fallback RTSP ping. If that fails, declares offline.
  /// </summary>
  private async Task FrameWatcherLoop(CameraConfig camera, RtspClient rtspClient, CancellationToken token)
  {
    while (!token.IsCancellationRequested)
    {
      await Task.Delay(100, token); // Check ~10x per second

      DateTime lastFrame = _lastFrameTime[camera.CameraName];
      if (DateTime.UtcNow - lastFrame > TimeSpan.FromSeconds(2))
      {
        // No frames for too long => do a quick fallback check
        bool fallbackOk = await QuickRtspPingAsync(camera, TimeSpan.FromMilliseconds(500));
        if (!fallbackOk)
        {
          // Definitely offline
          CheckAndHandleOffline(camera);

          // Disconnect & break out => main loop will try reconnect
          try { rtspClient.Dispose(); } catch { }
          break;
        }
        else
        {
          // The camera responded to ping => perhaps a streaming glitch
          // We'll mark offline so we can re-init streaming, but the device is reachable
          CheckAndHandleOffline(camera);

          // Disconnect
          try { rtspClient.Dispose(); } catch { }
          break;
        }
      }
    }
  }

  /// <summary>
  /// Helper to set camera offline status and raise event if it was online.
  /// </summary>
  private void CheckAndHandleOffline(CameraConfig camera)
  {
    if (_cameraOnlineStatus[camera.CameraName])
    {
      _cameraOnlineStatus[camera.CameraName] = false;
      RaiseStatusChanged(camera, isOffline: true);
    }
  }

  /// <summary>
  /// Raise the event that the camera came online/offline.
  /// </summary>
  private void RaiseStatusChanged(CameraConfig camera, bool isOffline)
  {
    //CameraStatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(camera, isOffline));
  }

  /// <summary>
  /// Minimal RTSP handshake ("OPTIONS") to confirm if camera is reachable.
  /// If it times out or fails, we consider it offline.
  /// </summary>
  private async Task<bool> QuickRtspPingAsync(CameraConfig camera, TimeSpan timeout)
  {
    // This is a raw socket approach. If your camera is at
    // rtsp://someip:554, parse out the host and port from the URL. 
    // For simplicity, let's parse from camera.RtspUrl:
    if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var uri))
      return false;

    string host = uri.Host;
    int port = uri.Port > 0 ? uri.Port : 554;

    string request =
        $"OPTIONS {camera.RtspUrl} RTSP/1.0\r\n" +
        "CSeq: 1\r\n\r\n";

    using var client = new TcpClient();
    try
    {
      var connectTask = client.ConnectAsync(host, port);
      if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
      {
        return false; // connection timeout
      }

      using var stream = client.GetStream();
      byte[] reqBytes = Encoding.ASCII.GetBytes(request);
      await stream.WriteAsync(reqBytes, 0, reqBytes.Length);

      byte[] buffer = new byte[1024];
      using var cts = new CancellationTokenSource(timeout);
      var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

      if (await Task.WhenAny(readTask, Task.Delay(timeout)) != readTask)
      {
        return false; // read timeout
      }

      int bytesRead = readTask.Result;
      if (bytesRead == 0)
        return false; // closed connection

      string response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
      if (response.StartsWith("RTSP/1.0 200"))
      {
        return true; // OK
      }
      return false;
    }
    catch
    {
      return false;
    }
  }
}
