using OpenCvSharp;

public static class CameraTestingHelper
{
  /// <summary>
  /// Asynchronously connects to the given RTSP URL, reads frames for a few seconds,
  /// and returns the resolution + average FPS in a human-readable string.
  /// 
  /// Note: Internally uses Task.Run to offload blocking OpenCV calls to a background thread.
  /// </summary>
  /// <param name="rtspUrl">Full RTSP URL (including credentials if needed).</param>
  /// <param name="testSeconds">How many seconds to capture frames for FPS measurement.</param>
  /// <param name="token">Optional CancellationToken to cancel early.</param>
  /// <returns>(success, message) - If success=true, message has "Resolution WxH, FPS: X". Otherwise, an error msg.</returns>
  public static async Task<(bool success, string message)> TestConnectionStats(
      string rtspUrl,
      int testSeconds = 5,
      CancellationToken token = default)
  {
    try
    {
      // Offload the synchronous OpenCV capture to a background thread via Task.Run
      return await Task.Run<(bool, string)>(() =>
      {
        // 1) Open capture
        using var capture = new VideoCapture(rtspUrl);
        if (!capture.IsOpened())
        {
          return (false, "Failed to open RTSP feed. Camera might be offline or credentials are invalid.");
        }

        DateTime startTime = DateTime.Now;
        int frameCount = 0;
        int width = 0;
        int height = 0;

        // 2) Read frames until we hit testSeconds or are canceled
        while ((DateTime.Now - startTime).TotalSeconds < testSeconds)
        {
          // Check if cancellation was requested
          if (token.IsCancellationRequested)
          {
            return (false, "Test was canceled.");
          }

          using var frame = new Mat();
          if (!capture.Read(frame) || frame.Empty())
          {
            // No frame arrived. We'll wait briefly and keep trying
            Thread.Sleep(50);
            continue;
          }

          frameCount++;
          width = frame.Width;
          height = frame.Height;
        }

        if (frameCount == 0)
        {
          return (false, "No frames received - camera might be offline or no video stream available.");
        }

        // 3) Compute FPS
        double elapsed = (DateTime.Now - startTime).TotalSeconds;
        double fps = frameCount / elapsed;
        string stats = $"Resolution: {width}x{height}, FPS: {fps:F2}";
        return (true, stats);

      }, token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      return (false, "Test was canceled.");
    }
    catch (Exception ex)
    {
      return (false, $"Error while testing connection: {ex.Message}");
    }
  }
}