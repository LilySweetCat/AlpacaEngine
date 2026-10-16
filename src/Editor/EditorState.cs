using System.Numerics;
using AlpacaEngine.GameObjects;
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
            field = value;
        }
    } = new();

    public static string? WorkingDirectory = Directory.GetCurrentDirectory();
    
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
