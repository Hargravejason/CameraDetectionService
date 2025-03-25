using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CameraDetectionService.Service.Services;

public class FileLoggerProvider : ILoggerProvider
{
  private readonly string _filePath;
  private readonly IConfiguration _options;

  public FileLoggerProvider(string filePath, IConfiguration options)
  {
    _filePath = filePath;
    _options = options;
  }

  public ILogger CreateLogger(string categoryName)
  {
    return new FileLogger(_filePath, _options);
  }

  public void Dispose()
  {
    // Nothing to dispose in this basic example
  }
}

public class FileLogger : ILogger
{
  private readonly IConfiguration _options;
  private readonly string _filePath;
  private static readonly object _lock = new object();

  public FileLogger(string filePath, IConfiguration options)
  {
    _filePath = filePath;
    _options = options;
  }

  public IDisposable BeginScope<TState>(TState state) => null;

  public bool IsEnabled(LogLevel logLevel)
  {
    var configSetting = _options.GetSection("Logging").GetValue<LogLevel>("LogLevel:Default");
    return logLevel >= configSetting && configSetting < LogLevel.None;
  }

  public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception exception,
      Func<TState, Exception, string> formatter)
  {
    if (!IsEnabled(logLevel))
      return;

    // Safely build the message string using the formatter
    string message = formatter(state, exception);

    // Optionally, you can include exception details separately:
    if (exception != null)
    {
      message += Environment.NewLine + exception.ToString();
    }

    // Write out to the file
    lock (_lock)
    {
      File.AppendAllText(Path.Combine(_filePath,$"log_{DateTime.Now:yyyy_MM_dd}.txt"),
          $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {message}{Environment.NewLine}");
    }
  }
}
