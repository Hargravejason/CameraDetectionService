using System.Windows;


namespace CameraDetectionService;

/// <summary>
/// Interaction logic for OfflineWindow.xaml
/// </summary>
public partial class OfflineWindow : Window
{
  public string TextToShow { get; set; } = "Camera is offline!";
  public OfflineWindow(string textToShow)
  {
    TextToShow = textToShow;
    InitializeComponent();
    DataContext = this;
  }
}
