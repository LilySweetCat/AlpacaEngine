using System.Numerics;
using System.Text.Json;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine;

public static class Inspector
{
    internal static Texture2D GameObjectIcon;
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
            var ray = Raylib.GetScreenToWorldRayEx(mouse.Value, Editor.CurrentCamera,
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

            if(ImGui.DragFloat3("Local Position", ref Editor.Selected.LocalPosition)) CommandManager.Do(new ChangeValueCommand<Vector3>(() => ));
            ImGui.DragFloat3("Local Rotation (degrees)", ref Editor.Selected.LocalRotation);
            ImGui.DragFloat3("Local Scale", ref Editor.Selected.LocalScale);
        }

        ImGui.End();
    }

    private static void DrawHierarchy()
    {
        ImGui.Begin("Hierarchy");
        
        if (ImGui.BeginPopupContextWindow("ctx_empty"))
        {
            if (ImGui.MenuItem("Create Empty")) { /* add object */ }
            ImGui.EndPopup();
        }

        HandleDrawGameObjectTreeRecursively(Editor.CurrentScene.GameObjects);

        ImGui.End();
    }

    private static void HandleDrawGameObjectTreeRecursively(List<GameObject> gameObjects)
    {
        foreach (var gameObject in gameObjects)
        {
            var hasChild = gameObject.Child is { Count: > 0 };
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                        | (hasChild ? ImGuiTreeNodeFlags.OpenOnArrow : ImGuiTreeNodeFlags.Leaf)
                        | (gameObject == Editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);

            // ## hides rest of string from user
            var opened = ImGui.TreeNodeEx($"     {gameObject.Name}##{gameObject.Id}", flags);
            var iconMin = ImGui.GetItemRectMin() + new System.Numerics.Vector2(
                ImGui.GetTreeNodeToLabelSpacing(), ImGui.GetStyle().FramePadding.Y);
            
            ImGui.GetWindowDrawList().AddImage((nint)GameObjectIcon.Id, iconMin,
                iconMin + new System.Numerics.Vector2(ImGui.GetFontSize()));
            
            if (ImGui.BeginPopupContextItem($"ctx_{gameObject.Id}"))
            {
                if (ImGui.MenuItem("Rename")) {}
                if (ImGui.MenuItem("Delete")) {}
                ImGui.EndPopup();
            }
            
            // Must be checked right after this node's header: after recursing into children,
            // "last item" is a child, so the parent would steal the selection.
            if (!ImGui.IsItemToggledOpen() && ImGui.IsItemClicked(ImGuiMouseButton.Left))
                Editor.Selected = gameObject;

            if (opened)
            {
                if (hasChild) HandleDrawGameObjectTreeRecursively(gameObject.Child!);
                ImGui.TreePop();
            }
        }
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
