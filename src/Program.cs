using System.Numerics;
using AlpacaEngine.Editor;
using AlpacaEngine.GameObjects;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine;

class Program
{
    [System.STAThread]
    static int Main(string[] args)
    {
        // --proj <dir>: the working directory for assets and scenes.
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--proj" || i + 1 >= args.Length) continue;

            EditorState.WorkingDirectory = args[++i];
        }

        StartEditor();
        return 0;
    }

    private static void StartEditor()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(1280, 720, $"AlpacaEngine: {EditorState.CurrentScene.Name}");

        // icon for app
        var icon = Raylib.LoadImage(Path.Combine(AppContext.BaseDirectory, "assets", "icons", "alpaca.png"));
        Raylib.SetWindowIcon(icon);
        Raylib.UnloadImage(icon);

        // all icons go here
        InitializeIcons();

        Raylib.MaximizeWindow();
        Raylib.SetTargetFPS(60);

        var windowScale = Raylib.GetWindowScaleDPI();
        var dpiScale = windowScale.X;

        rlImGui.Setup(true, enableDocking: true);

        EditorTheme.Apply(dpiScale);

        EditorState.CurrentCamera = new()
        {
            Position = new Vector3(5, 3, 5),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = 60f,
            Projection = CameraProjection.Perspective,
        };

        // ---- render texture for 3D Viewport (resolution is default here) ----
        EditorState.ViewTexture = Raylib.LoadRenderTexture(800, 600);

        while (!Raylib.WindowShouldClose())
        {
            // ============================================================
            // STAGE 0: Update camera in editor
            // ============================================================
            if (EditorState.ViewportHovered && !Gizmo.IsDragging && Raylib.IsMouseButtonDown(MouseButton.Right))
            {
                Raylib.UpdateCamera(ref EditorState.CurrentCamera, CameraMode.Free);
            }

            // ============================================================
            // STAGE 0.5: Recreate viewport RenderTexture if panel size changed
            // ============================================================
            var resScale = EditorState.ResScaleIndex switch
            {
                0 => 0.5f,
                1 => 0.75f,
                2 => 1f,
                _ => Raylib.GetWindowScaleDPI().X, // HiDPI native
            };

            var targetW = Math.Clamp((int)(EditorState.PendingSize.X * resScale), 64, 4096);
            var targetH = Math.Clamp((int)(EditorState.PendingSize.Y * resScale), 64, 4096);

            // if difference << 8px = ignore(debounce)
            if (Math.Abs(targetW - EditorState.ViewTexture.Texture.Width) >= 8 || Math.Abs(targetH - EditorState.ViewTexture.Texture.Height) >= 8)
            {
                Raylib.UnloadRenderTexture(EditorState.ViewTexture);
                EditorState.ViewTexture = Raylib.LoadRenderTexture(targetW, targetH);
            }

            // ============================================================
            // STAGE 1: Render the 3D Scene into the RenderTexture
            // ============================================================
            Raylib.BeginTextureMode(EditorState.ViewTexture);
            Raylib.ClearBackground(Color.Black);

            Raylib.BeginMode3D(EditorState.CurrentCamera);

            // Draw grid under the cube
            Raylib.DrawGrid(10, 1.0f);

            // render 3D
            RenderGameObjects(EditorState.CurrentScene.GameObjects);

            Raylib.EndMode3D();
            Raylib.EndTextureMode();

            // ============================================================
            // STAGE 2: Render the Main Window UI Layout
            // ============================================================
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            // Start ImGui Frame Processing
            rlImGui.Begin();

            Inspector.Render();

            // ============================================================
            // STAGE 3: Cleanup: End ImGui Processing Frame and draw metadata
            // ============================================================
            rlImGui.End();

            Raylib.EndDrawing();
        }

        // Cleanup resources
        Managers.ModelManager.UnloadAll();
        Raylib.UnloadTexture(Inspector.GameObjectIcon);
        AssetBrowser.UnloadIcons();
        Raylib.UnloadRenderTexture(EditorState.ViewTexture);
        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }

    private static void InitializeIcons()
    {
        AssetBrowser.LoadIcons();
        Inspector.GameObjectIcon = Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory, "assets", "icons", "gameobject.png"));

        Raylib.SetTextureFilter(Inspector.GameObjectIcon, TextureFilter.Bilinear);
    }

    private static void RenderGameObjects(List<GameObject> gameObjects)
    {
        foreach (var gameObject in gameObjects)
        {
            gameObject.PushTransform();

            try
            {
                gameObject.Render();

                if (EditorState.Selected == gameObject)
                {
                    gameObject.RenderOnSelected();
                }
            }
            finally
            {
                gameObject.PopTransform();
            }

            // Each WorldMatrix already includes its parents; do not nest transforms.
            if (gameObject.Child != null) RenderGameObjects(gameObject.Child);
        }
    }
}
