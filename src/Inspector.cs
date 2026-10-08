using System.Text.Json;
using ImGuiNET;
using rlImGui_cs;

namespace AlpacaEngine;

public static class Inspector
{
    private static bool _isOpenNewProjectModal;
    private static bool _isWelcomeModal = true;
    public static void Render()
    {
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport());

        if (_isWelcomeModal)
            ImGui.OpenPopup("Welcome");

        if (ImGui.BeginPopupModal("Welcome", ImGuiWindowFlags.Modal))
        {
            ImGui.InputText("Where project located", ref Editor.workingDirectory, 256);
            ImGui.Separator();
            
            if (ImGui.Button("Open"))
            {
                var data = File.ReadAllText($"{Editor.workingDirectory}/{Editor.workingDirectory}.proj");
                Editor.CurrentProject =  JsonSerializer.Deserialize<Project>(data);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();

            if (ImGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
            _isWelcomeModal = false;
        }
            
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
        ImGui.Begin("3D Viewport");

        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Preview Resolution", ref Editor.ResScaleIndex, Editor.ResScaleLabels, Editor.ResScaleLabels.Length);

        if (!ImGui.IsWindowCollapsed())
        {
            var avail = ImGui.GetContentRegionAvail();
            if (avail is { X: >= 16, Y: >= 16 }) // min size
                Editor.PendingSize = avail;
        }

        rlImGui.ImageRenderTextureFit(Editor.ViewTexture, false);

        ImGui.End();
    }

    private static void DrawInspector()
    {
        ImGui.Begin("Inspector");

        if (Editor.Selected != null)
        {
            ImGui.InputText("Name", ref Editor.Selected.Name, 64);
            ImGui.DragFloat3("Position", ref Editor.Selected.Position);
            ImGui.DragFloat3("Scale", ref Editor.Selected.Scale);
        }

        ImGui.End();
    }

    private static void DrawHierarchy()
    {
        ImGui.Begin("Hierarchy");

        if (ImGui.TreeNodeEx("Scene", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem) &&
                ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup("Scene context");
            }
            if (ImGui.BeginPopup("Scene context"))
            {
                ImGui.Button("Add new...");
                ImGui.EndPopup();
            }
                
            foreach (var gameObject in Editor.CurrentScene.GameObjects)
            {
                if (ImGui.TreeNodeEx($"{gameObject.Name}: {gameObject.Id}")) // FIX IT: remove when name will be unique
                {
                    Editor.Selected = gameObject;
                    ImGui.TreePop();
                }
            }
            ImGui.TreePop();
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