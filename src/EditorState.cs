using System.Numerics;
using Raylib_cs;

namespace AlpacaEngine;

public static class Editor
{
    public static Project? CurrentProject;
    public static Scene CurrentScene;
    public static string workingDirectory = Directory.GetCurrentDirectory();
    
    public static GameObject? Selected;

    public static int ResScaleIndex = 2;
    public static readonly string[] ResScaleLabels = ["0.5x (fast)", "0.75x", "1x", "DPI scale (native)"];

    public static RenderTexture2D ViewTexture;
    public static Vector2 PendingSize = new(800, 600);
}
