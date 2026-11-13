using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace AlpacaEngine.Logging;

internal static class RaylibLogging
{
    public static unsafe void Initialize()
    {
        Raylib.SetTraceLogCallback(&Receive);
        Raylib.SetTraceLogLevel(TraceLogLevel.Debug);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Receive(int level, sbyte* text, sbyte* arguments)
    {
        // Managed exceptions must never escape a native callback.
        try
        {
            var logLevel = (TraceLogLevel)level switch
            {
                TraceLogLevel.Trace or TraceLogLevel.Debug => LogLevel.Debug,
                TraceLogLevel.Warning => LogLevel.Warning,
                TraceLogLevel.Error or TraceLogLevel.Fatal => LogLevel.Error,
                _ => LogLevel.Info
            };
            Logger.WriteFrom(logLevel, Raylib_cs.Logging.GetLogMessage((nint)text, (nint)arguments), "Raylib");
        }
        catch (Exception exception)
        {
            try { Console.Error.WriteLine($"Cannot capture Raylib log: {exception.Message}"); }
            catch (Exception) { }
        }
    }
}
