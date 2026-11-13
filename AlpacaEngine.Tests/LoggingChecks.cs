using System.Numerics;
using AlpacaEngine.Editor;
using AlpacaEngine.Logging;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

internal static class LoggingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "AlpacaLogging-" + Guid.NewGuid());
        var output = Console.Out;
        using var console = new StringWriter();
        Console.SetOut(console);

        try
        {
            Logger.ClearHistory();
            Logger.Initialize(folder);
            var file = Logger.FilePath!;
            var missingModel = Path.Combine(folder, "missing.glb");

            Check(!ModelManager.TryGet(missingModel, out _), "Missing model is rejected");

            for (var i = 0; i < 60; i++) ModelManager.TryGet(missingModel, out _);

            Check(Logger.GetEntries().Length == 1, "Frame requests do not repeat the error");
            ModelManager.Retry(missingModel);
            ModelManager.TryGet(missingModel, out _);
            var entries = Logger.GetEntries();
            Check(entries.Select(entry => entry.Level).SequenceEqual(
                new[] { LogLevel.Error, LogLevel.Info, LogLevel.Error }), "Error, retry, error ordering");
            Check(entries.All(entry => entry.Message.Contains(missingModel)), "All retry events identify the file");
            Check(entries[0].Source == "ModelManager" && entries[0].Member == "TryGet"
                && entries[0].Line > 0 && entries[0].File.EndsWith("ModelManager.cs"),
                "Caller metadata identifies the original call site");
            Check(entries[0].ExceptionDetails == null, "Ordinary errors do not generate stack traces");
            var saved = File.ReadAllText(file);
            Check(saved.IndexOf("Model file not found", StringComparison.Ordinal)
                < saved.IndexOf("User requested", StringComparison.Ordinal), "File preserves event order");
            Check(console.ToString().Contains(entries[0].Summary), "Console receives the same event");

            try { throw new IOException("Exception fixture"); }
            catch (IOException exception) { Logger.Write(LogLevel.Error, "File operation failed", exception); }

            Check(Logger.GetEntries()[^1].ExceptionDetails!.Contains("LoggingChecks.Run"),
                "Caught exceptions retain their stack trace");
            Logger.ClearHistory();
            Check(Logger.GetEntries().Length == 0 && File.ReadAllText(file).Contains("Exception fixture"),
                "Clearing the panel preserves file history");

            Parallel.For(0, Logger.HistoryLimit + 50,
                i => Logger.Write(LogLevel.Debug, $"Concurrent event {i}"));
            entries = Logger.GetEntries();
            Check(entries.Length == Logger.HistoryLimit
                && entries.Zip(entries.Skip(1)).All(pair => pair.First.Id < pair.Second.Id),
                "Concurrent logging keeps bounded, ordered history");
            Check(File.ReadLines(file).Count(line => line.Contains("Concurrent event")) == Logger.HistoryLimit + 50,
                "File retains events evicted from the panel");

            Logger.Shutdown();
            Logger.Initialize(folder);
            Check(Logger.FilePath != file && File.Exists(file), "Each session gets its own file");
            Logger.Shutdown();
            Logger.ClearHistory();
            Logger.Initialize(file);
            Check(Logger.FilePath == null && Logger.GetEntries().Single().Level == LogLevel.Error,
                "Unavailable log directory reports failure without crashing");
            Logger.Write(LogLevel.Info, "Still logging to the panel");
            Check(Logger.GetEntries()[^1].Level == LogLevel.Info, "Other outputs survive file failure");

            Logger.Initialize(folder);
            RaylibLogging.Initialize();
            var imagePath = Path.Combine(folder, "missing-image.png");
            Raylib.LoadImage(imagePath);
            Check(Logger.GetEntries().Any(entry => entry.Source == "Raylib"
                && entry.Level == LogLevel.Warning && entry.Message.Contains(imagePath)),
                "Native Raylib messages include formatted file paths");
            CheckPanel();
        }
        finally
        {
            // Restore the native default logger before closing our sinks.
            unsafe { Raylib.SetTraceLogCallback(null); }
            Logger.Shutdown();
            Logger.ClearHistory();
            LogWindow.IsOpen = false;
            ModelManager.UnloadAll();
            Console.SetOut(output);

            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        Console.WriteLine("Logging checks passed: retry ordering, file/console output, caller metadata, exceptions, bounded history, file failure, Raylib callback and panel rendering.");
    }

    private static void CheckPanel()
    {
        var context = ImGui.CreateContext();

        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(1024, 768);
            io.DeltaTime = 1f / 60;
            unsafe { io.NativePtr->IniFilename = null; }
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height);
            LogWindow.IsOpen = true;
            Logger.Write(LogLevel.Warning, "A message containing ## and % characters");

            for (var i = 0; i < 3; i++)
            {
                ImGui.NewFrame();
                LogWindow.Render();
                ImGui.Render();
            }

            Check(ImGui.GetDrawData().TotalVtxCount > 0, "Panel produces visible content");
            Logger.ClearHistory();
            ImGui.NewFrame();
            LogWindow.Render();
            ImGui.Render();
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
