using System.Text.Json;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine;

public static class Inspector
{
    private static bool _isOpenNewProjectModal;
    public static void Render()
    {
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport());
            
        // TOP MENU
        DrawTopMenu();

        HandleNewProject();

        // PANEL A: Hierarchy
        DrawHierarchy();

        // PANEL B: Inspector
        DrawInspector();

        // PANEL C: 3D Viewport
        Draw3dViewport();

        // PANEL D: Assets browser
        DrawAssetBrowser();
    }

    private static void DrawTopMenu()
    {
        if (ImGui.BeginMainMenuBar()) {
            if (ImGui.BeginMenu("Project")) {
                if (ImGui.MenuItem("New Project")) {
                    _isOpenNewProjectModal = true;
                }
                if (ImGui.MenuItem("Open Project", "Ctrl+O")) {
                }
                if (ImGui.MenuItem("Save Project", "Ctrl+S")) {
                    var data = JsonSerializer.Serialize(Editor.CurrentScene);
                    File.WriteAllText($"{Directory.GetCurrentDirectory()}/{Editor.CurrentScene.Name}.scene", data);
                }
                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }

    private static void DrawAssetBrowser()
    {
        ImGui.Begin("Assets");

        ImGui.End();
    }

    private static void Draw3dViewport()
    {
        if (!ImGui.Begin("3D Viewport"))
        {
            Gizmo.Cancel();
            Editor.ViewportHovered = false;
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Preview Resolution", ref Editor.ResScaleIndex, Editor.ResScaleLabels, Editor.ResScaleLabels.Length);
        var mode = (int)Gizmo.Mode;
        ImGui.SetNextItemWidth(120);
        if (ImGui.Combo("Gizmo", ref mode, "Move\0Rotate\0Scale\0"))
        {
            Gizmo.Cancel();
            Gizmo.Mode = (Gizmo.Operation)mode;
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Local", ref Gizmo.Local)) Gizmo.Cancel();

        if (!ImGui.IsWindowCollapsed())
        {
            var avail = ImGui.GetContentRegionAvail();
            if (avail is { X: >= 16, Y: >= 16 }) // min size
                Editor.PendingSize = avail;
        }

        rlImGui.ImageRenderTextureFit(Editor.ViewTexture, false);
        var hovered = ImGui.IsItemHovered();
        Editor.ViewportHovered = hovered;
        var mouse = Picking.GetMouseInViewport();
        var gizmoUsed = Gizmo.Draw(Editor.Selected, Editor.CurrentCamera,
            ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), hovered);
        
        // if viewport hovered by mouse
        if (hovered && !gizmoUsed && mouse.HasValue
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            var ray = Picking.ScreenPixelToRay(mouse.Value, Editor.CurrentCamera,
                Editor.ViewTexture.Texture.Width, Editor.ViewTexture.Texture.Height);
            Editor.Selected = Picking.PickAtMouse(ray);
        }

        ImGui.End();
    }

    private static void DrawInspector()
    {
        ImGui.Begin("Inspector");

        if (Editor.Selected != null)
        {
            ImGui.InputText("Name", ref Editor.Selected.Name, 64);
            ImGui.DragFloat3("Position", ref Editor.Selected.Position);
            ImGui.DragFloat3("Rotation (degrees)", ref Editor.Selected.Rotation);
            ImGui.DragFloat3("Scale", ref Editor.Selected.Scale);
        }

        ImGui.End();
    }

    private static void DrawHierarchy()
    {
        ImGui.Begin("Hierarchy");

        foreach (var gameObject in Editor.CurrentScene.GameObjects)
        {
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                        | ImGuiTreeNodeFlags.Leaf
                        | (gameObject == Editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);

            if (ImGui.TreeNodeEx($"{gameObject.Name}##{gameObject.Id}", flags)) // ## hides rest of string from user
                ImGui.TreePop();

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                Editor.Selected = gameObject;             // state — только на клике
        }

        ImGui.End();
    }

    private static void HandleNewProject()
    {
        if (_isOpenNewProjectModal)
            ImGui.OpenPopup("New Project modal");

        if (ImGui.BeginPopupModal("New Project modal", ImGuiWindowFlags.Modal))
        {
            Editor.CurrentProject = new Project();
            ImGui.InputText("Project name", ref Editor.CurrentProject.Name, 64);
            ImGui.InputText("Where project will be", ref Editor.workingDirectory, 256);
            ImGui.Separator();
            
            if (ImGui.Button("Accept"))
            {
                var data = JsonSerializer.Serialize(Editor.CurrentProject);
                File.WriteAllText($"{Editor.workingDirectory}/{Editor.CurrentProject.Name}.proj", data);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
            _isOpenNewProjectModal = false;
        }
    }
}
