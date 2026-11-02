using System.Text;
using Microsoft.Extensions.Logging;

namespace DCT_SD.Helpers;

// Mirrors everything the app already logs (HTTP client request/response lines for every call to
// the external RD Fetch API, EF Core commands, and every existing _logger.LogError/LogWarning
// call) into one timestamped file per run, in addition to the console - no code elsewhere needs
// to change, since this plugs into the same ILogger pipeline every other provider already uses.
// One line per log entry: "yyyy-MM-dd HH:mm:ss.fff [Level] Category: Message", with the
// exception (if any) on the following indented line(s).
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly Timer _flushTimer;
    private bool _disposed;

    public string LogFilePath { get; }

    public FileLoggerProvider(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        LogFilePath = Path.Combine(logDirectory, $"dctsd-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var stream = new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        // AutoFlush is deliberately OFF: with it on, every single log line (including every HTTP
        // client request/response line, every EF Core command, every LogError/LogWarning call
        // across the whole app) did a real, synchronous, blocking disk flush while holding _lock -
        // every request thread that needed to log anything serialized on that one lock, so a single
        // slow disk write could stall the entire app at once. Writes are now buffered in memory
        // (fast, lock held only briefly) and flushed periodically on a background timer instead, so
        // the log file still stays current within ~2 seconds without that per-line disk-flush cost
        // sitting on the request path.
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
        _flushTimer = new Timer(_ => TryFlush(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void TryFlush()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Flush();
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void WriteLine(string categoryName, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            _writer.Write(" [");
            _writer.Write(LevelLabel(level));
            _writer.Write("] ");
            _writer.Write(categoryName);
            _writer.Write(": ");
            _writer.WriteLine(message);

            if (exception is not null)
            {
                foreach (var line in exception.ToString().Split('\n'))
                {
                    _writer.Write("    ");
                    _writer.WriteLine(line.TrimEnd('\r'));
                }
            }
        }
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => level.ToString(),
    };

    public void Dispose()
    {
        _flushTimer.Dispose();
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Flush();
            _writer.Dispose();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string categoryName, FileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.WriteLine(_categoryName, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}
