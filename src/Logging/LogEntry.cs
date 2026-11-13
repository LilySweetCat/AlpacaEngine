namespace AlpacaEngine.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public sealed record LogEntry(long Id, DateTimeOffset Time, LogLevel Level, string Message,
    string Source, string Member, string File, int Line, string? ExceptionDetails)
{
    public string Summary => $"{Time:HH:mm:ss.fff}  {Level.ToString().ToUpperInvariant(),-7} [{Source}] {Message}";

    public string Details => $"{Time:yyyy-MM-dd HH:mm:ss.fff zzz}\n{Summary}\n"
        + (File.Length == 0 ? "" : $"{File}:{Line} ({Member})\n")
        + ExceptionDetails;
}
