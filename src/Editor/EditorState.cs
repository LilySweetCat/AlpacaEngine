using System.Numerics;
using AlpacaEngine.Managers;
using Raylib_cs;

namespace AlpacaEngine.Editor;

public static class Editor
{
    public static Project? CurrentProject;
    
    private static Scene _currentScene = new();
    public static Scene CurrentScene
    {
        get => _currentScene;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            
            if (ReferenceEquals(_currentScene, value)) return;
            
            Gizmo.Cancel(true);
            Inspector.FinishEdit();
            CommandManager.Clear();
            Selected = null;
            _currentScene = value;
        }
    }
    public static string? WorkingDirectory = Directory.GetCurrentDirectory();
    
    public static GameObject? Selected;

    public static int ResScaleIndex = 2;
    public static readonly string[] ResScaleLabels = ["0.5x (fast)", "0.75x", "1x", "DPI scale (native)"];

    public static RenderTexture2D ViewTexture;
    public static Vector2 PendingSize = new(800, 600);
    
    public static Camera3D CurrentCamera;
    public static bool ViewportHovered;
}
