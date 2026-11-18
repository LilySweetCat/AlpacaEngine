using System.Collections.Frozen;
using Raylib_cs;
using AlpacaEngine.Logging;

namespace AlpacaEngine.Managers;

// Resources stay cached across Undo/Redo and scene changes, and are released
// before the graphics context closes. Components serialize paths, not GPU pointers.
public static class ModelManager
{
    public static IReadOnlySet<string> SupportedExtensions { get; } = new[]
    {
        ".obj", ".gltf", ".glb", ".iqm", ".vox", ".m3d"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Model> Models = new();
    private static readonly Dictionary<string, string> Errors = new();

    public static string? GetError(string path) => Errors.GetValueOrDefault(path);

    public static void Retry(string path)
    {
        Logger.Write(LogLevel.Info, $"User requested model loading retry: {path}");
        Errors.Remove(path);
    }

    public static bool TryGet(string path, out Model model)
    {
        if (Models.TryGetValue(path, out model)) return true;

        if (Errors.ContainsKey(path)) return false;

        if (!File.Exists(path))
        {
            Errors[path] = "Model file not found: " + path;
            Logger.Write(LogLevel.Error, Errors[path]);
            return false;
        }

        if (!Raylib.IsWindowReady()) return false;

        Logger.Write(LogLevel.Debug, $"Loading model: {path}");
        model = Raylib.LoadModel(path);

        if (!Raylib.IsModelValid(model))
        {
            Raylib.UnloadModel(model);
            model = default;
            Errors[path] = "Could not load model: " + path;
            Logger.Write(LogLevel.Error, Errors[path]);
            return false;
        }

        Models.Add(path, model);
        Logger.Write(LogLevel.Info, $"Model loaded: {path}");
        return true;
    }

    public static void UnloadAll()
    {
        foreach (var model in Models.Values) Raylib.UnloadModel(model);

        Models.Clear();
        Errors.Clear();
    }
}
