using Microsoft.Extensions.Logging;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using System.Net;

public static class CameraTestingHelper
{
  public static async Task<(bool success, string message)> TestConnectionStats(
      string rtspUrl,
      string username,
      string password,
      ILogger logger,
      int testSeconds = 5,
      CancellationToken token = default)
  {
    try
    {
      logger.LogInformation($"Performing Testing on RTSP URL: {rtspUrl}");
      ConnectionParameters connectionParameters;
      if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
      {
        logger.LogInformation($"Connecting with Username: {username} and password on RTSP URL: {rtspUrl}");
        var credentials = new NetworkCredential(username, password);
        connectionParameters = new ConnectionParameters(new Uri(rtspUrl), credentials);
      }
      else
      {
        logger.LogInformation($"Connecting with no Username and password on RTSP URL: {rtspUrl}");
        connectionParameters = new ConnectionParameters(new Uri(rtspUrl));
      }

      using var rtspClient = new RtspClient(connectionParameters);
      var frameQueue = new Queue<RawFrame>();

      int width = 0;
      int height = 0;
      int frameCount = 0;
      int NALType = 0;
      long totalBytes = 0;

      rtspClient.FrameReceived += (sender, frame) =>
      {
        if (frame is RawVideoFrame rawFrame)
        {
          var data = rawFrame.FrameSegment;
          int offset = data.Offset;
          int length = data.Count;
          var buffer = data.Array;

          int i = offset;
          int end = offset + length - 4;
          while (i < end)
          {
            if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00 && buffer[i + 3] == 0x01)
            {
              int nalType = buffer[i + 4] & 0x1F;
              NALType = nalType;
              i += 4;
            }
            else i++;
          }
          totalBytes += rawFrame.FrameSegment.Count;
          frameCount++;
        }
        if (frame is RawH264IFrame iFrame)
        {
          var spsNal = H264NalHelper.ExtractSpsNal(iFrame.FrameSegment.Array, iFrame.FrameSegment.Offset, iFrame.FrameSegment.Count);
          if (spsNal != null)
          {
            var sps = H264SpsParser.ParseSps(spsNal);
            if (sps != null)
            {
              width = sps.Width;
              height = sps.Height;
            }
          }
        }
      };

      await rtspClient.ConnectAsync(token);
      _ = rtspClient.ReceiveAsync(token);

      logger.LogInformation($"Connected to device, testing for {testSeconds} seconds on RTSP URL: {rtspUrl}");

      DateTime startTime = DateTime.Now;

      while ((DateTime.Now - startTime).TotalSeconds < testSeconds)
      {
        if (token.IsCancellationRequested)
        {
          return (false, "Test was canceled.");
        }

        await Task.Delay(50);
      }

      logger.LogInformation($"Test complete {frameCount} frames found in {testSeconds} seconds on RTSP URL: {rtspUrl}");

      if (frameCount == 0)
      {
        return (false, "No frames received - camera might be offline or no video stream available.");
      }

      double elapsed = (DateTime.Now - startTime).TotalSeconds;
      double fps = frameCount / elapsed;
      double bitrateMbps = (totalBytes * 8) / 1_000_000.0 / elapsed;
      string stats = $"BitRate: {bitrateMbps:F2} Mbps";

      if (width > 0 && height > 0)
        stats += $" Resolution: {width}x{height}";

      stats += $" FPS: {fps:F2} NALType: {NALType}";

      logger.LogInformation($"Connecting to camera stats: {stats} on RTSP URL: {rtspUrl}");

      return (true, stats);
    }
    catch (OperationCanceledException oex)
    {
      logger.LogError(oex, $"Error performing Test on URL: {rtspUrl}");
      return (false, "Test was canceled.");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, $"Error performing Test on URL: {rtspUrl}");
      return (false, $"Error while testing connection: {ex.Message}");
    }
  }

  public static class H264NalHelper
  {
    public static byte[] ExtractSpsNal(byte[] buffer, int offset, int count)
    {
      int i = offset;
      int end = offset + count - 4;

      while (i < end)
      {
        // Look for NAL start code (0x00 0x00 0x00 0x01)
        if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00 && buffer[i + 3] == 0x01)
        {
          int nalStart = i + 4;
          if (nalStart >= buffer.Length)
            break;

          int nalType = buffer[nalStart] & 0x1F;

          if (nalType == 7) // SPS
          {
            int nextNal = FindNextNalStart(buffer, nalStart, count - (nalStart - offset));
            int length = (nextNal == -1 ? count + offset : nextNal) - nalStart;
            var sps = new byte[length];
            Array.Copy(buffer, nalStart, sps, 0, length);
            return sps;
          }

          i = nalStart;
        }
        else
        {
          i++;
        }
      }

      return null;
    }

    private static int FindNextNalStart(byte[] buffer, int start, int remaining)
    {
      int end = start + remaining - 4;
      for (int i = start; i < end; i++)
      {
        if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00 && buffer[i + 3] == 0x01)
          return i;
      }
      return -1;
    }

    private static int FindNalStart(byte[] buffer, int offset, int count)
    {
      for (int i = offset; i < offset + count - 4; i++)
      {
        if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00 && buffer[i + 3] == 0x01)
          return i + 4;
      }
      return -1;
    }
  }

  public class H264SpsParser
  {
    public int Width { get; set; }
    public int Height { get; set; }

    public static H264SpsParser ParseSps(byte[] sps)
    {
      try
      {
        var reader = new BitReader(sps);
        reader.SkipBits(8); // NAL header

        reader.ReadUE(); // profile_idc
        reader.SkipBits(8); // constraint_set_flags + reserved
        reader.ReadUE(); // level_idc
        reader.ReadUE(); // seq_parameter_set_id

        int chromaFormatIdc = 1; // default 4:2:0
        if (reader.PeekBit()) chromaFormatIdc = reader.ReadUE();

        if (chromaFormatIdc == 3) reader.SkipBits(1); // separate_colour_plane_flag

        reader.ReadUE(); // bit_depth_luma_minus8
        reader.ReadUE(); // bit_depth_chroma_minus8
        reader.SkipBits(1); // qpprime_y_zero_transform_bypass_flag

        if (reader.ReadBit()) // seq_scaling_matrix_present_flag
        {
          for (int i = 0; i < 8; i++)
            if (reader.ReadBit())
              SkipScalingList(reader, i < 6 ? 16 : 64);
        }

        reader.ReadUE(); // log2_max_frame_num_minus4
        reader.ReadUE(); // pic_order_cnt_type
        reader.ReadUE(); // log2_max_pic_order_cnt_lsb_minus4
        reader.SkipBits(1); // num_ref_frames
        reader.SkipBits(1); // gaps_in_frame_num_value_allowed_flag

        int picWidthInMbs = reader.ReadUE() + 1;
        int picHeightInMapUnits = reader.ReadUE() + 1;
        int frameMbsOnly = reader.ReadBit() ? 1 : 0;

        if (frameMbsOnly == 0)
          reader.SkipBits(1); // mb_adaptive_frame_field_flag

        reader.SkipBits(1); // direct_8x8_inference_flag

        int frameCroppingFlag = reader.ReadBit() ? 1 : 0;
        int cropLeft = 0, cropRight = 0, cropTop = 0, cropBottom = 0;

        if (frameCroppingFlag == 1)
        {
          cropLeft = reader.ReadUE();
          cropRight = reader.ReadUE();
          cropTop = reader.ReadUE();
          cropBottom = reader.ReadUE();
        }

        int width = picWidthInMbs * 16 - (cropLeft + cropRight) * 2;
        int height = picHeightInMapUnits * 16 * (2 - frameMbsOnly) - (cropTop + cropBottom) * 2;

        return new H264SpsParser
        {
          Width = width,
          Height = height
        };
      }
      catch
      {
        return null;
      }
    }

    private static void SkipScalingList(BitReader reader, int size)
    {
      int lastScale = 8;
      int nextScale = 8;

      for (int j = 0; j < size; j++)
      {
        if (nextScale != 0)
        {
          int deltaScale = reader.ReadSE();
          nextScale = (lastScale + deltaScale + 256) % 256;
        }
        lastScale = nextScale == 0 ? lastScale : nextScale;
      }
    }
  }
  public class BitReader
  {
    private readonly byte[] data;
    private int byteOffset = 0;
    private int bitOffset = 0;

    public BitReader(byte[] data)
    {
      this.data = data;
    }

    public bool ReadBit()
    {
      bool bit = (data[byteOffset] & (1 << (7 - bitOffset))) != 0;
      if (++bitOffset == 8)
      {
        bitOffset = 0;
        byteOffset++;
      }
      return bit;
    }

    public bool PeekBit()
    {
      return (data[byteOffset] & (1 << (7 - bitOffset))) != 0;
    }

    public void SkipBits(int count)
    {
      for (int i = 0; i < count; i++) ReadBit();
    }

    public int ReadBits(int count)
    {
      int result = 0;
      for (int i = 0; i < count; i++)
        result = (result << 1) | (ReadBit() ? 1 : 0);
      return result;
    }

    public int ReadUE()
    {
      int zeros = 0;
      while (!ReadBit() && zeros < 32) zeros++;
      int value = (1 << zeros) - 1 + ReadBits(zeros);
      return value;
    }

    public int ReadSE()
    {
      int value = ReadUE();
      return ((value & 1) == 0) ? -(value / 2) : (value + 1) / 2;
    }
  }

}