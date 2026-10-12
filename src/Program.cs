using System.CommandLine;
using System.CommandLine.Parsing;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine;

class Program
{
    [System.STAThread]
    static int Main(string[] args)
    {
        Option<string> projDirectoryOption = new("--proj")
        {
            Description = "Where project is located"
        };
        Option<string> gameModeOption = new("--game")
        {
            Description = "Launch as game?"
        };

        RootCommand rootCommand = new("AlpacaEngine CLI");
        
        rootCommand.Options.Add(projDirectoryOption);
        rootCommand.Options.Add(gameModeOption);

        rootCommand.SetAction(parseResult =>
        {
            var parsedProjDirectory = parseResult.GetValue(projDirectoryOption);
            var parsedGameMode = parseResult.GetValue(gameModeOption);

            if (parsedProjDirectory is not null)
            {
                Editor.workingDirectory = parsedProjDirectory;
            }

            if (parsedGameMode is not null)
            {
                StartGame();
                return 0;
            }

            StartEditor();
            return 0;
        });

        var parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    private static void StartGame()
    {
        
    }

    private static void StartEditor()
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

        Editor.CurrentCamera = new()
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
            if (Editor.ViewportHovered && !Gizmo.IsDragging && Raylib.IsKeyDown(KeyboardKey.LeftAlt))
            {
                Raylib.UpdateCamera(ref Editor.CurrentCamera, CameraMode.Free);
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

            Raylib.BeginMode3D(Editor.CurrentCamera);
                
            // Draw grid under the cube
            Raylib.DrawGrid(10, 1.0f);

            // render 3D
            foreach (var gameObject in Editor.CurrentScene.GameObjects)
            {
                gameObject.Render();
                
                if (Editor.Selected != gameObject) continue;
                
                gameObject.RenderWireframe();
                Raylib.DrawBoundingBox(gameObject.GetBoundingBox(), Color.Violet);
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
