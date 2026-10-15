using System.Text.Json;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine.Editor;

public static class Inspector
{
    internal static Texture2D GameObjectIcon;
    private static bool _isOpenNewProjectModal;
    private static GameObject? _editTarget;
    private static string? _editProperty;
    private static Action? _finishEdit;
    
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
        if (_editTarget != AlpacaEngine.Editor.Editor.Selected) FinishEdit();

        // Active widgets keep their own Undo (notably InputText); do not interrupt a drag.
        if (!ImGui.IsAnyItemActive() && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && ImGui.GetIO().KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false)) CommandManager.Undo();
            else if (ImGui.IsKeyPressed(ImGuiKey.Y, false)) CommandManager.Redo();
        }
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
                    var data = JsonSerializer.Serialize(AlpacaEngine.Editor.Editor.CurrentScene);
                    File.WriteAllText($"{Directory.GetCurrentDirectory()}/{AlpacaEngine.Editor.Editor.CurrentScene.Name}.scene", data);
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
            AlpacaEngine.Editor.Editor.ViewportHovered = false;
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Preview Resolution", ref AlpacaEngine.Editor.Editor.ResScaleIndex, AlpacaEngine.Editor.Editor.ResScaleLabels, AlpacaEngine.Editor.Editor.ResScaleLabels.Length);
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
                AlpacaEngine.Editor.Editor.PendingSize = avail;
        }

        rlImGui.ImageRenderTextureFit(AlpacaEngine.Editor.Editor.ViewTexture, false);
        var hovered = ImGui.IsItemHovered();
        AlpacaEngine.Editor.Editor.ViewportHovered = hovered;
        var mouse = Picking.GetMouseInViewport();
        var gizmoUsed = Gizmo.Draw(AlpacaEngine.Editor.Editor.Selected, AlpacaEngine.Editor.Editor.CurrentCamera,
            ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), hovered);
        
        // if viewport hovered by mouse
        if (hovered && !gizmoUsed && mouse.HasValue
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            var ray = Raylib.GetScreenToWorldRayEx(mouse.Value, AlpacaEngine.Editor.Editor.CurrentCamera,
                AlpacaEngine.Editor.Editor.ViewTexture.Texture.Width, AlpacaEngine.Editor.Editor.ViewTexture.Texture.Height);
            AlpacaEngine.Editor.Editor.Selected = Picking.PickAtMouse(ray);
        }

        ImGui.End();
    }

    private static void DrawInspector()
    {
        if (_editTarget != AlpacaEngine.Editor.Editor.Selected) FinishEdit();
        if (!ImGui.Begin("Inspector"))
        {
            FinishEdit();
            ImGui.End();
            return;
        }

        var target = AlpacaEngine.Editor.Editor.Selected;
        if (target != null)
        {
            ImGui.PushID(target.Id.ToString());
            
            var name = target.Name;
            ImGui.InputText("Name", ref target.Name, 64);
            TrackEdit(target, nameof(target.Name), name, () => target.Name, value => target.Name = value);

            var position = target.LocalPosition;
            ImGui.DragFloat3("Local Position", ref target.LocalPosition);
            TrackEdit(target, nameof(target.LocalPosition), position, () => target.LocalPosition, value => target.LocalPosition = value);
            
            var rotation = target.LocalRotation;
            ImGui.DragFloat3("Local Rotation (degrees)", ref target.LocalRotation);
            TrackEdit(target, nameof(target.LocalRotation), rotation, () => target.LocalRotation, value => target.LocalRotation = value);
            
            var scale = target.LocalScale;
            ImGui.DragFloat3("Local Scale", ref target.LocalScale);
            TrackEdit(target, nameof(target.LocalScale), scale, () => target.LocalScale, value => target.LocalScale = value);
            
            ImGui.PopID();
        }

        ImGui.End();
    }

    private static void TrackEdit<T>(GameObject target, string property, T before, Func<T> getValue, Action<T> setValue)
    {
        if (ImGui.IsItemActivated())
        {
            FinishEdit();
            _editTarget = target;
            _editProperty = property;
            _finishEdit = () => CommandManager.RecordChange(setValue, before, getValue());
        }
        if (ImGui.IsItemDeactivated() && _editProperty == property) FinishEdit();
    }

    internal static void FinishEdit()
    {
        _finishEdit?.Invoke();
        _finishEdit = null;
        _editTarget = null;
        _editProperty = null;
    }

    private static void DrawHierarchy()
    {
        ImGui.Begin("Hierarchy");
        
        if (ImGui.BeginPopupContextWindow("ctx_empty"))
        {
            if (ImGui.MenuItem("Create Empty")) { /* add object */ }
            ImGui.EndPopup();
        }

        HandleDrawGameObjectTreeRecursively(AlpacaEngine.Editor.Editor.CurrentScene.GameObjects);

        ImGui.End();
    }

    private static void HandleDrawGameObjectTreeRecursively(List<GameObject> gameObjects)
    {
        foreach (var gameObject in gameObjects)
        {
            var hasChild = gameObject.Child is { Count: > 0 };
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                        | (hasChild ? ImGuiTreeNodeFlags.OpenOnArrow : ImGuiTreeNodeFlags.Leaf)
                        | (gameObject == AlpacaEngine.Editor.Editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);

            // ## hides rest of string from user
            var opened = ImGui.TreeNodeEx($"     {gameObject.Name}##{gameObject.Id}", flags);
            var iconMin = ImGui.GetItemRectMin() + new System.Numerics.Vector2(
                ImGui.GetTreeNodeToLabelSpacing(), (ImGui.GetItemRectSize().Y - ImGui.GetFontSize()) * 0.5f);
            
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
                AlpacaEngine.Editor.Editor.Selected = gameObject;

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
            AlpacaEngine.Editor.Editor.CurrentProject = new Project();
            ImGui.InputText("Project name", ref AlpacaEngine.Editor.Editor.CurrentProject.Name, 64);
            ImGui.InputText("Where project will be", ref AlpacaEngine.Editor.Editor.WorkingDirectory, 256);
            ImGui.Separator();
            
            if (ImGui.Button("Accept"))
            {
                var data = JsonSerializer.Serialize(AlpacaEngine.Editor.Editor.CurrentProject);
                File.WriteAllText($"{AlpacaEngine.Editor.Editor.WorkingDirectory}/{AlpacaEngine.Editor.Editor.CurrentProject.Name}.proj", data);
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
