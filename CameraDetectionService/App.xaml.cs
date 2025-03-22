using System.Windows;

public partial class App : Application
{
  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);
    // Perform any app-level initialization or logging
  }

  protected override void OnExit(ExitEventArgs e)
  {
    // Cleanup or finalize resources here
    base.OnExit(e);
  }
}
