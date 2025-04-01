using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;


namespace CameraDetectionService;

/// <summary>
/// Interaction logic for OfflineWindow.xaml
/// </summary>
public partial class OfflineWindow : Window, INotifyPropertyChanged
{
  private List<string> _camerasOffline = new List<string>();
  private string _textToShow = "Camera is offline!";
  public string TextToShow
  {
    get => _textToShow;
    set
    {
      _textToShow = value;
      OnPropertyChanged();
    }
  }
  public OfflineWindow()
  {
    InitializeComponent();
    DataContext = this;
  }

  public void AddCameratoOffline(string camera)
  {
    _camerasOffline.Add(camera);

    if (_camerasOffline.Count > 0)
    {
      var offline = string.Join(", ", _camerasOffline);
      if(offline.Length > 80)
        offline = offline.Substring(0, 80) + " etc...";
      TextToShow = $"{offline} ";

      this.Show();
    }
    
  }

  public void RemoveCameraFromOffline(string camera)
  {
    _camerasOffline.Remove(camera);
    if (_camerasOffline.Count == 0 && this.IsVisible)
    {
      this.Hide();
    }
    else
    {
      var offline = string.Join(", ", _camerasOffline);
      if (offline.Length > 80)
        offline = offline.Substring(0, 80) + " etc...";
      TextToShow = $"{offline} ";
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }

  private void Button_Click(object sender, RoutedEventArgs e)
  {
    // Hide all OfflineWindow instances
    foreach (var window in Application.Current.Windows.OfType<OfflineWindow>())
    {
      window.Hide();
    }
  }
}
