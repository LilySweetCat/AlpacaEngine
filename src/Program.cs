using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine;

class Program
{
    [System.STAThread]
    static void Main(string[] args)
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(1280, 720, $"AlpacaEngine: {Editor.CurrentScene.Name}");
        Raylib.MaximizeWindow();
        Raylib.SetTargetFPS(60);
        
        var windowScale = Raylib.GetWindowScaleDPI();
        var dpiScale = windowScale.X; 
        
        rlImGui.Setup(true, enableDocking: true);
        
        // Масштабируем элементы интерфейса (кнопки, ползунки, отступы)
        var style = ImGui.GetStyle();
        style.ScaleAllSizes(dpiScale);

        Camera3D camera = new()
        {
            Position = new Vector3(5, 3, 5),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = 60f,
            Projection = CameraProjection.Perspective,
        };
        
        // ---- render texture for 3D Viewport (resolution is default here) ----
        Editor.ViewTexture = Raylib.LoadRenderTexture(800, 600);
        var rtWidth = 800;
        var rtHeight = 600;

        while (!Raylib.WindowShouldClose())
        {
            // ============================================================
            // STAGE 0: Update camera in editor
            // ============================================================
            if (Raylib.IsKeyDown(KeyboardKey.LeftAlt))
            {
                Raylib.UpdateCamera(ref camera, CameraMode.Free);
            }
            
            // ============================================================
            // STAGE 0.5: Recreate viewport RenderTexture if panel size changed
            // ============================================================
            var resScale = Editor.ResScaleIndex switch
            {
                0 => 0.5f,
                1 => 0.76f,
                2 => 1f,
                _ => Raylib.GetWindowScaleDPI().X, // HiDPI native
            };

            var targetW = Math.Clamp((int)(Editor.PendingSize.X * resScale), 64, 4096);
            var targetH = Math.Clamp((int)(Editor.PendingSize.Y * resScale), 64, 4096);

            // if difference << 8px = ignore(debounce)
            if (Math.Abs(targetW - rtWidth) >= 8 || Math.Abs(targetH - rtHeight) >= 8)
            {
                Raylib.UnloadRenderTexture(Editor.ViewTexture);
                Editor.ViewTexture = Raylib.LoadRenderTexture(targetW, targetH);
                rtWidth = targetW;
                rtHeight = targetH;
            }

            // ============================================================
            // STAGE 1: Render the 3D Scene into the RenderTexture
            // ============================================================
            Raylib.BeginTextureMode(Editor.ViewTexture);
            Raylib.ClearBackground(Color.Black);

            Raylib.BeginMode3D(camera);
                
            // Draw grid under the cube
            Raylib.DrawGrid(10, 1.0f);

            // render 3D
            foreach (var gameObject in Editor.CurrentScene.GameObjects)
            {
                gameObject.Render();
            }

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
            
            // PANEL E: Console logs
            // ImGui.BeginTabBar("Commands");
            // ImGui.TabItemButton("")
            // ImGui.LogText("there will be logs"); // FIX IT: not displayed
            // ImGui.EndTabBar();

            // ============================================================
            // STAGE 3: Cleanup: End ImGui Processing Frame and draw metadata
            // ============================================================
            rlImGui.End();

            Raylib.EndDrawing();
        }

        // Cleanup resources
        Raylib.UnloadRenderTexture(Editor.ViewTexture);
        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }
}
