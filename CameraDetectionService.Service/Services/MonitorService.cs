using CameraDetectionService.Service.Models;
using Microsoft.Extensions.Logging;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CameraDetectionService.Service.Services;

public class CameraStatusChangedEventArgs : EventArgs
{
  public CameraModel Camera { get; }
  public bool IsOffline { get; }
  public CameraStatusChangedEventArgs(CameraModel camera, bool isOffline)
  {
    Camera = camera;
    IsOffline = isOffline;
  }
}

public class MonitorService(ILogger<MonitorService> _logger)
{
  private Config _config { get; set; } = new();

  /// <summary>
  /// Raised when a camera's status changes from online->offline or vice versa.
  /// </summary>
  public event EventHandler<CameraStatusChangedEventArgs> CameraStatusChanged;

  private CancellationTokenSource _cts = new();

  // Holds the active RtspClient for each camera (so we can disconnect if needed).
  private readonly ConcurrentDictionary<string, CameraModel> _rtspClients = new ConcurrentDictionary<string, CameraModel>();


  public void SetConfig(Config config) => _config = config;

  public Config GetConfig() => _config;


  /// <summary>
  /// Start monitoring the given cameras (stop any previous monitoring).
  /// </summary>
  public void StartMonitoring()
  {
    StopMonitoring();

    _logger.LogInformation($"Starting camera monitoring. {_config.Cameras.Count()} found.");
    _cts = new CancellationTokenSource();

    // For each camera, start an async monitor loop
    foreach (var cam in _config.Cameras)
    {
      // Store the client in dictionary
      if (!_rtspClients.ContainsKey(cam.CameraName))
        _rtspClients[cam.CameraName] = new CameraModel() { Config = cam };
      
      _ = TryConnectAndStream(_rtspClients[cam.CameraName], _cts.Token);
    }
  }


  /// <summary>
  /// Gracefully stop monitoring all cameras.
  /// </summary>
  public void StopMonitoring()
  {
    _logger.LogInformation("Stopped camera monitoring.");

    if (_cts != null && !_cts.IsCancellationRequested)
    {
      _cts.Cancel();
    }

    foreach (var kvp in _rtspClients)
    {
      try
      {
        if(kvp.Value?.Client != null)
          kvp.Value?.Client.Dispose();
      }
      catch { /* ignore */ }
    }

    _rtspClients.Clear();
  }

  //public async Task<bool> TestConnection(CameraModel camera, CancellationToken token)
  //{
  //  try
  //  {
  //    _logger.LogInformation($"Testing connection to camera: {camera.Config.CameraName}");
  //    // Build connection parameters for RtspClientSharp
  //    var uri = new Uri(camera.Config.RtspUrl);

  //    ConnectionParameters connectionParams;
  //    // If you have camera credentials:
  //    if (!string.IsNullOrEmpty(camera.Config.Username) && !string.IsNullOrEmpty(camera.Config.Password))
  //    {
  //      _logger.LogInformation($"Testing using credentials for camera: {camera.Config.CameraName}");
  //      connectionParams = new ConnectionParameters(uri, new NetworkCredential(camera.Config.Username, camera.Config.Password))
  //      {
  //        RequiredTracks = RequiredTracks.Video,
  //      };
  //    }
  //    else
  //    {
  //      _logger.LogInformation($"Testing no credentials for camera: {camera.Config.CameraName}");
  //      connectionParams = new ConnectionParameters(uri)
  //      {
  //      };
  //    }

  //    var client = new RtspClient(connectionParams);
  //    var connected = false;

  //    // Subscribe to frame-received event
  //    client.FrameReceived += (sender, frame) =>
  //    {
  //      if (frame.Type == FrameType.Audio)
  //        return;

  //      // If we were offline, mark online
  //      connected = true;

  //      if (frame is RawH264Frame h264Frame)
  //      {
  //        // Parse frame to image
          
  //      }

  //    };

  //    // Actually connect
  //    await client.ConnectAsync(token);
  //    client.ReceiveAsync(token);

  //    _logger.LogInformation($"Testing onnection to Camera: {camera.Config.CameraName} established, attempting to receive data.");

  //    while (!connected && !token.IsCancellationRequested)
  //    {
  //      await Task.Delay(500);
  //    }
  //    client.Dispose();
  //    return connected;
  //  }
  //  catch (Exception ex)
  //  {
  //    _logger.LogError(ex, $"Error testing camera connection: {camera.Config.CameraName}");
  //    return false;
  //  }
  //}




  /// <summary>
  /// Attempts to create an RtspClient, connect, and read frames.
  /// Returns true if we connected successfully; false otherwise.
  /// </summary>
  private async Task TryConnectAndStream(CameraModel camera, CancellationToken token)
  {
    try
    {
      _logger.LogInformation($"Connecting to camera: {camera.Config.CameraName}");
      // Build connection parameters for RtspClientSharp
      var uri = new Uri(camera.Config.RtspUrl);

      ConnectionParameters connectionParams;
      // If you have camera credentials:
      if(!string.IsNullOrEmpty(camera.Config.Username) && !string.IsNullOrEmpty(camera.Config.Password))
      {
        _logger.LogInformation($"Using credentials for camera: {camera.Config.CameraName}");
        connectionParams = new ConnectionParameters(uri, new NetworkCredential(camera.Config.Username, camera.Config.Password))
        {
          RequiredTracks = RequiredTracks.Video
        };
      }
      else
      {
        _logger.LogInformation($"No credentials for camera: {camera.Config.CameraName}");
        connectionParams = new ConnectionParameters(uri)
        {
        }; 
      }

      var client = new RtspClient(connectionParams);

      // Subscribe to frame-received event
      client.FrameReceived += (sender, frame) =>
      {
        // Update lastFrameTime on each frame
        camera.LastFrameTime = DateTime.UtcNow;

        // If we were offline, mark online
        if (!camera.Online)
        {
          camera.Online = true;
          CameraStatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(camera, !camera.Online));
        }
      };

      camera.Client = client;
           
      // Actually connect
      await client.ConnectAsync(token);
      _ = client.ReceiveAsync(token);

      _logger.LogInformation($"Connection to Camera: {camera.Config.CameraName} established, attempting to receive data.");

      // Start a mini-loop to watch for frame dropouts
      _ = Task.Run(() => FrameWatcherLoop(camera, token));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, $"Error connecting to camera: {camera.Config.CameraName}");
      if (camera.Online)
      {
        camera.Online = false;
        CameraStatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(camera, !camera.Online));
      }
      Task.Delay(1000, token).Wait(); // Wait 1 seconds before retrying
      _ = Task.Run(() => TryConnectAndStream(camera, token));
      _logger.LogError($"Retrying connection to camera: {camera.Config.CameraName}");
    }
  }


  /// <summary>
  /// Continuously checks if frames have stopped arriving beyond OfflineTimeout.
  /// If so, tries a fallback RTSP ping. If that fails, declares offline.
  /// </summary>
  private async Task FrameWatcherLoop(CameraModel camera, CancellationToken token)
  {
    while (!token.IsCancellationRequested)
    {
      await Task.Delay(100, token); // Check ~10x per second

      
      if (DateTime.UtcNow - camera.LastFrameTime > camera.OfflineTimeout.Subtract(TimeSpan.FromMilliseconds(500)))
      {
        // No frames for too long => do a quick fallback check
        //bool fallbackOk = await QuickRtspPingAsync(camera.Config, TimeSpan.FromMilliseconds(500));
        //if (!fallbackOk)
        //{
        if (camera.Online)
        {
          camera.Online = false;
          // Definitely offline
          CameraStatusChanged?.Invoke(this, new CameraStatusChangedEventArgs(camera, !camera.Online));
        }
        //}

        // Disconnect & break out => main loop will try reconnect
        try
        {
          if (!camera.Online) 
          { 
            camera.Client.Dispose();
            Task.Delay(1000, token).Wait(); // Wait 1 seconds before retrying
            await TryConnectAndStream(camera, token);
            return;
          }
        }
        catch (Exception ex)
        { 
          _logger.LogError(ex, $"Error disconnecting camera: {camera.Config.CameraName}");
        }
      }
    }
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

