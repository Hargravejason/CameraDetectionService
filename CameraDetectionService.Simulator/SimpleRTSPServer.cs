using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class SimpleRtspServer
{
  private readonly string ip;
  private readonly int port;
  private TcpListener rtspListener;
  private CancellationTokenSource cts;
  private CancellationTokenSource streamingCts;
  private UdpClient udpSender;
  private IPEndPoint clientEndpoint;
  private int clientRtpPort;

  public SimpleRtspServer(string ipAddress, int port)
  {
    ip = ipAddress;
    this.port = port;
  }

  public void Start()
  {
    cts = new CancellationTokenSource();
    rtspListener = new TcpListener(IPAddress.Parse(ip), port);
    rtspListener.Start();
    Debug.WriteLine($"🟢 RTSP server started at rtsp://{ip}:{port}");

    Task.Run(async () =>
    {
      while (!cts.IsCancellationRequested)
      {
        var client = await rtspListener.AcceptTcpClientAsync();
        _ = Task.Run(() => HandleRtspClient(client, cts.Token));
      }
    });
  }

  public void Stop()
  {
    cts?.Cancel();
    rtspListener?.Stop();
    udpSender?.Close();
    udpSender = null;
    clientEndpoint = null;
    streamingCts = null;
  }

  private async Task HandleRtspClient(TcpClient client, CancellationToken token)
  {
    using var stream = client.GetStream();
    var reader = new StreamReader(stream, Encoding.ASCII);
    var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

    string sessionId = new Random().Next(10000, 99999).ToString();
    bool streamingStarted = false;

    while (!token.IsCancellationRequested)
    {
      string requestLine = await reader.ReadLineAsync();
      if (string.IsNullOrWhiteSpace(requestLine)) break;

      string cseq = null;
      string transport = null;
      string line;
      while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync()))
      {
        if (line.StartsWith("CSeq:"))
          cseq = line.Substring(5).Trim();
        else if (line.StartsWith("Transport:"))
          transport = line.Substring(10).Trim();
      }

      string response = $"RTSP/1.0 200 OK\r\nCSeq: {cseq}\r\n";

      if (requestLine.StartsWith("OPTIONS"))
      {
        response += "Public: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN\r\n\r\n";
      }
      else if (requestLine.StartsWith("DESCRIBE"))
      {
        string sdp =
            "v=0\r\n" +
            $"o=- 0 0 IN IP4 {ip}\r\n" +
            $"s=FakeCamera\r\n" +
            $"c=IN IP4 {ip}\r\n" +
            $"t=0 0\r\n" +
            $"m=video 5000 RTP/AVP 96\r\n" +
            "a=rtpmap:96 H264/90000\r\n" +
            "a=fmtp:96 packetization-mode=1; sprop-parameter-sets=Z0IAKeKQCg==,aMljiA==\r\n" +
            "a=control:track1\r\n";

        response += "Content-Type: application/sdp\r\n" +
                    $"Content-Length: {sdp.Length}\r\n\r\n" +
                    sdp;
      }
      else if (requestLine.StartsWith("SETUP"))
      {
        var parts = transport.Split(';');
        foreach (var part in parts)
        {
          if (part.StartsWith("client_port"))
          {
            var portPair = part.Split('=')[1].Split('-');
            clientRtpPort = int.Parse(portPair[0]);
            break;
          }
        }

        clientEndpoint = new IPEndPoint(((IPEndPoint)client.Client.RemoteEndPoint).Address, clientRtpPort);
        udpSender = new UdpClient(5000);
        udpSender.Connect(clientEndpoint);

        response += $"Transport: RTP/AVP;unicast;client_port={clientRtpPort}-{clientRtpPort + 1};server_port=5000-5001\r\n";
        response += $"Session: {sessionId}\r\n\r\n";
      }
      else if (requestLine.StartsWith("PLAY"))
      {
        response += $"Session: {sessionId}\r\n\r\n";
        if (!streamingStarted)
        {
          streamingStarted = true;
          streamingCts = new CancellationTokenSource();
          _ = Task.Run(() => SendDummyH264Stream(streamingCts.Token));
        }
      }
      else if (requestLine.StartsWith("TEARDOWN"))
      {
        response += $"Session: {sessionId}\r\n\r\n";
        streamingCts?.Cancel();
        udpSender?.Close();
        udpSender = null;
        clientEndpoint = null;
        streamingCts = null;
        break;
      }

      await writer.WriteAsync(response);
    }

    udpSender?.Close();
    udpSender = null;
  }

  private async Task SendDummyH264Stream(CancellationToken token)
  {
    ushort seq = 0;
    uint timestamp = 0;

    while (!token.IsCancellationRequested)
    {
      byte[] dummyFrame = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x88, 0x84 }; // fake IDR NAL unit

      byte[] packet = new byte[12 + dummyFrame.Length];
      packet[0] = 0x80;
      packet[1] = 96 | 0x80;
      packet[2] = (byte)(seq >> 8);
      packet[3] = (byte)(seq);
      packet[4] = (byte)(timestamp >> 24);
      packet[5] = (byte)(timestamp >> 16);
      packet[6] = (byte)(timestamp >> 8);
      packet[7] = (byte)(timestamp);
      packet[8] = 0x12;
      packet[9] = 0x34;
      packet[10] = 0x56;
      packet[11] = 0x78;

      Buffer.BlockCopy(dummyFrame, 0, packet, 12, dummyFrame.Length);

      try
      {
        if (udpSender == null || clientEndpoint == null)
        {
          Debug.WriteLine("UDP sender or client endpoint is null.");
          break;
        }
        await udpSender.SendAsync(packet, packet.Length);
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"UDP send failed: {ex.Message}");
        break;
      }

      seq++;
      timestamp += 9000;
      await Task.Delay(100);
    }
  }
}
