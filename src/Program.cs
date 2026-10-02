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
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1280, 720, "AlpacaEngine smoke test");
        Raylib.MaximizeWindow();
        Raylib.SetTargetFPS(60);
        
        rlImGui.Setup(true, enableDocking: true);

        Camera3D camera = new()
        {
            Position = new Vector3(5, 3, 5),
            Target = Vector3.Zero,
            Up = Vector3.UnitY,
            FovY = 60f,
            Projection = CameraProjection.Perspective,
        };
        
        // 4. Create a Render Texture for the 3D viewport panel
        // We use a fixed or dynamic internal resolution for the 3D scene
        var viewRenderTexture = Raylib.LoadRenderTexture(800, 600);

        // 5. Stateful variables for ImGui UI adjustment
        var gameObjects = new List<GameObject>() {new SimpleCube()};
        var selectedGameObjectIndex = 0;

        while (!Raylib.WindowShouldClose())
        {
            // ============================================================
            // STAGE 1: Render the 3D Scene into the RenderTexture
            // ============================================================
            Raylib.BeginTextureMode(viewRenderTexture);
            Raylib.ClearBackground(Color.DarkGray);

            Raylib.BeginMode3D(camera);
                
            // Draw grid under the cube
            Raylib.DrawGrid(10, 1.0f);

            // render 3D
            foreach (var gameObject in gameObjects)
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
            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport());
            
            // TOP MENU
            if (ImGui.BeginMainMenuBar()) {
                if (ImGui.BeginMenu("File")) {
                    if (ImGui.MenuItem("New")) { 
                    }
                    if (ImGui.MenuItem("Open", "Ctrl+O")) { 
                    }
                    if (ImGui.MenuItem("Save", "Ctrl+S")) {
                    }
                    if (ImGui.MenuItem("Save as..")) { 
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            // PANEL A: Hierarchy
            ImGui.Begin("Hierarchy");
            ImGui.TreeNode("Scene"); // NEED FIX: throws error if open 
            ImGui.End();
            
            // PANEL B: Inspector
            ImGui.Begin("Inspector");

            ImGui.DragFloat3("Position", ref gameObjects[selectedGameObjectIndex].Position);
            ImGui.DragFloat3("Scale", ref gameObjects[selectedGameObjectIndex].Scale);
            
            ImGui.End();

            // PANEL C: 3D Viewport Panel (Hosts the RenderTexture)
            ImGui.Begin("3D Viewport");
                
            // Read the available window size allocated by ImGui layout engine
            Vector2 regionAvail = ImGui.GetContentRegionAvail();

            // Render the Texture inside the ImGui frame window bounds
            // Note: rlImGui handles texture flipping automatically so the Y axis aligns correctly.
            rlImGui.ImageRenderTextureFit(viewRenderTexture, false); 
                
            ImGui.End();
            
            // PANEL D: Console logs
            ImGui.Begin("Logs");
            ImGui.LogText("there will be logs"); // FIX IT: not displayed
            ImGui.End();

            // End ImGui Processing Frame and draw metadata
            rlImGui.End();

            Raylib.EndDrawing();
        }

        // Cleanup resources
        Raylib.UnloadRenderTexture(viewRenderTexture);
        rlImGui.Shutdown();
        Raylib.CloseWindow();
    }
}
