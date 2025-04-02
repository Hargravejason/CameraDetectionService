using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using System.Windows.Media.Imaging;


namespace CameraDetectionService.Helpers;

  /// <summary>
  /// Interaction logic for CustomBalloon.xaml
  /// </summary>
  public partial class CustomBalloon : UserControl
  {
  public CustomBalloon()
  {
    InitializeComponent();
  }

  public string Title
  {
    get => TitleTextBlock.Text;
    set => TitleTextBlock.Text = value;
  }

  public string Message
  {
    get => MessageTextBlock.Text;
    set => MessageTextBlock.Text = value;
  }
  public BalloonIcon Icon
  {
    set => IconImage.Source = GetIconImage(value);
  }

  private BitmapImage GetIconImage(BalloonIcon icon)
  {
    string iconUri = icon switch
    {
      BalloonIcon.Info => "pack://application:,,,/Resources/info_icon.png",
      BalloonIcon.Warning => "pack://application:,,,/Resources/warning_icon.png",
      BalloonIcon.Error => "pack://application:,,,/Resources/error_icon.png",
      _ => "pack://application:,,,/Resources/info_icon.ico"
    };
    return new BitmapImage(new Uri(iconUri));
  }
}
