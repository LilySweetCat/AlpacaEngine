using System.Numerics;
using System.Text.Json;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using AlpacaEngine.Managers;
using Raylib_cs;
using AlpacaEngine.Rendering;

namespace AlpacaEngine.Editor;

public static class EditorState
{
    // Windows paths are case-insensitive; macOS and Linux stay case-sensitive.
    // A missed match only costs a cache entry, a wrong match could hit the wrong file.
    internal static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static Scene CurrentScene
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(field, value)) return;

            Gizmo.Cancel(true);
            Inspector.FinishEdit();
            CommandManager.Clear();
            Selected = null;
            CurrentScenePath = null;
            field = value;
        }
    } = new();

    public static string? CurrentScenePath { get; private set; }

    private static readonly JsonSerializerOptions SceneJsonOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    public static string CreateScene(string folder, string name)
    {
        name = name.Trim();

        if (name.EndsWith(".scene", StringComparison.OrdinalIgnoreCase)) name = name[..^6].Trim();

        if (string.IsNullOrWhiteSpace(name) || name is "." or ".."
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException("Enter a valid scene name.");

        var path = Path.GetFullPath(Path.Combine(folder, name + ".scene"));
        var scene = new Scene { Name = name, GameObjects = [] };

        // Never overwrite an existing asset, even if it appeared after the dialog opened.
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, scene, SceneJsonOptions);
        return path;
    }

    public static void OpenScene(string path)
    {
        path = Path.GetFullPath(path);
        using var stream = File.OpenRead(path);
        var scene = JsonSerializer.Deserialize<Scene>(stream, SceneJsonOptions)
                    ?? throw new JsonException("The scene file is empty.");

        if (scene.GameObjects == null || !ValidObjects(scene.GameObjects))
            throw new JsonException("The scene contains an invalid object list.");

        if (SceneLighting.GetSuns(scene.GameObjects).Take(2).Count() > 1)
            throw new JsonException("The scene can contain only one sun light.");

        // Only replace the active scene after the entire file has been loaded successfully.
        CurrentScene = scene;
        CurrentScenePath = path;
    }

    private static bool ValidObjects(List<GameObject> objects) => objects.All(gameObject =>
        gameObject != null && gameObject.Components != null
        && gameObject.Components.All(component => component != null)
        && (gameObject.Child == null || ValidObjects(gameObject.Child)));

    public static void SaveScene()
    {
        var path = CurrentScenePath ?? Path.GetFullPath(Path.Combine(
            WorkingDirectory ?? Directory.GetCurrentDirectory(), CurrentScene.Name + ".scene"));
        File.WriteAllText(path, JsonSerializer.Serialize(CurrentScene, SceneJsonOptions));
        CurrentScenePath = path;
    }

    public static string? WorkingDirectory = Directory.GetCurrentDirectory();

    public static string CreateAssetFolder(string parent, string name)
    {
        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name) || name is "." or ".."
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains('/') || name.Contains('\\') || name.EndsWith('.'))
            throw new ArgumentException("Enter a valid folder name without a trailing dot.");

        parent = ValidateAssetFolder(parent, allowRoot: true);
        var path = Path.Combine(parent, name);

        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("An item with this name already exists.");

        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteAssetFolder(string path)
    {
        path = ValidateAssetFolder(path, allowRoot: false);
        var deletingScene = CurrentScenePath != null && IsPathInside(path, CurrentScenePath);

        try
        {
            // Recursive deletion removes contents too, but does not follow directory junctions.
            Directory.Delete(path, recursive: true);
        }
        finally
        {
            // A failed recursive deletion can still have removed some of its files.
            if (deletingScene && !File.Exists(CurrentScenePath)) CurrentScene = new Scene();
        }
    }

    private static string ValidateAssetFolder(string path, bool allowRoot)
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
            throw new InvalidOperationException("Working directory is not set.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(WorkingDirectory));
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        if (!(allowRoot && PathComparer.Equals(root, path)) && !IsPathInside(root, path))
            throw new ArgumentException("Choose a folder inside the project. The project root cannot be deleted.");

        // Reject links anywhere along the path, including the project root.
        var current = path;

        while (true)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Folder operations through symbolic links are not supported.");

            if (PathComparer.Equals(current, root)) break;

            current = Path.GetDirectoryName(current)!;
        }

        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("The folder no longer exists.");

        return path;
    }

    private static bool IsPathInside(string parent, string path)
    {
        var relative = Path.GetRelativePath(parent, path);
        return !Path.IsPathRooted(relative) && relative != "." && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar);
    }

    public static void DeleteAsset(string path)
    {
        path = Path.GetFullPath(path);

        var deletingScene = PathComparer.Equals(path, CurrentScenePath);

        // Reset editing state only after the deletion succeeds.
        File.Delete(path);

        if (deletingScene) CurrentScene = new Scene();
    }

    public static GameObject? Selected;

    public static void ValidateSelection()
    {
        if (Selected == null || Contains(CurrentScene.GameObjects, Selected)) return;

        Gizmo.Cancel(true);
        Inspector.FinishEdit(record: false);
        Selected = null;
    }

    internal static bool Contains(List<GameObject> objects, GameObject target)
    {
        foreach (var gameObject in objects)
        {
            if (ReferenceEquals(gameObject, target)
                || (gameObject.Child != null && Contains(gameObject.Child, target))) return true;
        }

        return false;
    }

    public static void RenameGameObject(GameObject target, string name)
    {
        name = name.Trim();

        if (name.Length == 0 || !Contains(CurrentScene.GameObjects, target)) return;

        Inspector.FinishEdit();

        if (target.Name == name) return;

        CommandManager.Do(new ChangeValueCommand<string>(value => target.Name = value, target.Name, name));
    }

    public static void AddGameObject()
    {
        AddGameObject(new GameObject());
    }

    public static bool HasSunLight => SceneLighting.GetSuns(CurrentScene.GameObjects).Any();

    public static void AddSunLight()
    {
        if (HasSunLight) return;

        AddGameObject(new GameObject
        {
            Name = "Sun",
            LocalPosition = new Vector3(0, 3, 0),
            LocalRotation = new Vector3(45, -30, 0),
            Components = [new SunLightSourceComponent()]
        });
    }

    private static void AddGameObject(GameObject gameObject)
    {
        Inspector.FinishEdit();

        var scene = CurrentScene;
        CommandManager.Do(new ChangeValueCommand<List<GameObject>>(
            objects => scene.GameObjects = objects, scene.GameObjects,
            new List<GameObject>(scene.GameObjects) { gameObject }));
        Selected = gameObject;
    }

    public static void AddComponent(GameObject target, IComponent component)
    {
        if (!Contains(CurrentScene.GameObjects, target)) return;

        if (component is SunLightSourceComponent && HasSunLight) return;

        Inspector.FinishEdit();
        CommandManager.Do(new ChangeValueCommand<List<IComponent>>(
            components => target.Components = components, target.Components,
            new List<IComponent>(target.Components) { component }));
    }

    public static void DeleteGameObject(GameObject target)
    {
        var scene = CurrentScene;
        RemoveFrom(scene.GameObjects, null);

        bool RemoveFrom(List<GameObject> objects, GameObject? parent)
        {
            if (objects.Contains(target))
            {
                Gizmo.Cancel(true);
                Inspector.FinishEdit();

                var remaining = new List<GameObject>(objects);
                remaining.Remove(target);
                CommandManager.Do(new ChangeValueCommand<List<GameObject>>(value =>
                {
                    if (parent == null) scene.GameObjects = value;
                    else parent.Child = value;
                    target.Parent = value.Contains(target) ? parent : null;
                    ValidateSelection();
                }, objects, remaining));
                return true;
            }

            foreach (var gameObject in objects)
                if (gameObject.Child != null && RemoveFrom(gameObject.Child, gameObject)) return true;

            return false;
        }
    }

    public static int ResScaleIndex = 2;
    public static readonly string[] ResScaleLabels = ["0.5x (fast)", "0.75x", "1x", "DPI scale (native)"];

    public static RenderTexture2D ViewTexture;
    public static Vector2 PendingSize = new(800, 600);

    public static Camera3D CurrentCamera;
    public static bool ViewportHovered;
}
