using System.Numerics;
using System.Text.Json;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using AlpacaEngine.Managers;
using Raylib_cs;

namespace AlpacaEngine.Editor;

public static class EditorState
{
    public static Project? CurrentProject;

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

    public static void DeleteAsset(string path)
    {
        path = Path.GetFullPath(path);
        var comparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var projectPath = CurrentProject == null || string.IsNullOrWhiteSpace(WorkingDirectory)
            ? null : Path.GetFullPath(Path.Combine(WorkingDirectory, CurrentProject.Name + ".proj"));
        var deletingScene = comparer.Equals(path, CurrentScenePath);
        var deletingProject = comparer.Equals(path, projectPath);

        // Reset editing state only after deletion succeeds.
        File.Delete(path);
        if (deletingProject) CurrentProject = null;
        if (deletingScene || deletingProject) CurrentScene = new Scene();
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
        Inspector.FinishEdit();
        var scene = CurrentScene;
        var gameObject = new GameObject();
        CommandManager.Do(new ChangeValueCommand<List<GameObject>>(
            objects => scene.GameObjects = objects, scene.GameObjects,
            new List<GameObject>(scene.GameObjects) { gameObject }));
        Selected = gameObject;
    }

    public static void AddComponent(GameObject target, IComponent component)
    {
        if (!Contains(CurrentScene.GameObjects, target)) return;
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
