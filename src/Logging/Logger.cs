using System.Runtime.CompilerServices;

namespace AlpacaEngine.Logging;

public static class Logger
{
    public const int HistoryLimit = 1000;
    private static readonly Lock Sync = new();
    private static readonly Queue<LogEntry> History = new();
    private static StreamWriter? _writer;
    private static long _nextId;

    public static string? FilePath { get; private set; }

    public static void Initialize(string directory)
    {
        lock (Sync)
        {
            _writer?.Dispose();
            _writer = null;
            FilePath = null;

            try
            {
                Directory.CreateDirectory(directory);

                var path = Path.GetFullPath(Path.Combine(directory,
                    $"alpaca-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log"));
                _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.Read)) { AutoFlush = true };
                FilePath = path;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                Write(LogLevel.Error, $"Cannot create log file in: {directory}", exception);
            }
        }
    }

    public static LogEntry[] GetEntries()
    {
        lock (Sync) return History.ToArray();
    }

    public static void ClearHistory()
    {
        lock (Sync) History.Clear();
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                AddEntry(LogLevel.Error, "Cannot close log file.", exception, "Logger", "", "", 0);
            }
            finally
            {
                _writer = null;
            }
        }
    }

    // Caller attributes are filled in by the compiler; no stack trace is needed.
    public static void Write(LogLevel level, string message, Exception? exception = null,
        [CallerMemberName] string member = "", [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        WriteFrom(level, message, Path.GetFileNameWithoutExtension(file), exception, member, file, line);
    }

    internal static void WriteFrom(LogLevel level, string message, string source,
        Exception? exception = null, string member = "", string file = "", int line = 0)
    {
        lock (Sync)
        {
            var entry = AddEntry(level, message, exception, source, member, file, line);

            try
            {
                _writer?.WriteLine(entry.Details);
            }
            catch (Exception failure) when (IsFileError(failure))
            {
                // Disable a failed sink before reporting its failure; never log recursively.
                var failedWriter = _writer;
                _writer = null;

                try { failedWriter?.Dispose(); }
                catch (Exception cleanupFailure) when (IsFileError(cleanupFailure)) { }

                AddEntry(LogLevel.Error, $"Writing to log file failed: {FilePath}", failure,
                    "Logger", "", "", 0);
            }
        }
    }

    private static LogEntry AddEntry(LogLevel level, string message, Exception? exception,
        string source, string member, string file, int line)
    {
        var entry = new LogEntry(++_nextId, DateTimeOffset.Now, level, message, source,
            member, file, line, exception?.ToString());
        History.Enqueue(entry);

        if (History.Count > HistoryLimit) History.Dequeue();

        try
        {
            Console.WriteLine(entry.Summary);

            if (entry.ExceptionDetails != null) Console.WriteLine(entry.ExceptionDetails);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }

        return entry;
    }

    private static bool IsFileError(Exception exception) => exception is IOException
        or UnauthorizedAccessException or ArgumentException or NotSupportedException
        or System.Security.SecurityException;
}
