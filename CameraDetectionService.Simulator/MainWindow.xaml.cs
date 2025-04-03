using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace CameraDetectionService.Simulator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
  private SimpleRtspServer _rtspServer;
  private bool _isRunning;

  public MainWindow()
  {
    InitializeComponent();
    byte[] imageBytes = GetResourceBytes("pack://application:,,,/fakeCamImage.jpg");
    _rtspServer = new SimpleRtspServer("127.0.0.1", 554);
  }

  private void ToggleButton_Click(object sender, RoutedEventArgs e)
  {
    if (_isRunning)
    {
      _rtspServer.Stop();
      ToggleButton.Content = "Start";
    }
    else
    {
      _rtspServer.Start();
      ToggleButton.Content = "Stop";
    }
    _isRunning = !_isRunning;
  }
  private byte[] GetResourceBytes(string resourceUri)
  {
    Uri uri = new Uri(resourceUri, UriKind.RelativeOrAbsolute);
    StreamResourceInfo resourceInfo = Application.GetResourceStream(uri);
    if (resourceInfo != null)
    {
      using (Stream stream = resourceInfo.Stream)
      {
        using (MemoryStream memoryStream = new MemoryStream())
        {
          stream.CopyTo(memoryStream);
          return memoryStream.ToArray();
        }
      }
    }
    return null;
  }
}