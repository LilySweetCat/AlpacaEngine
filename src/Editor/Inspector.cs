using System.Numerics;
using System.Text.Json;
using AlpacaEngine.GameObjects;
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
    private static GameObject? _renameTarget;
    private static Scene? _renameScene;
    private static string _renameName = "";
    private static bool _openRenamePopup;
    
    public static void Render()
    {
        EditorState.ValidateSelection();
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
        if (_editTarget != EditorState.Selected) FinishEdit();

        // Active widgets keep their own Undo (notably InputText); do not interrupt a drag.
        if (!ImGui.IsAnyItemActive() && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && ImGui.GetIO().KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false)) CommandManager.Undo();
            else if (ImGui.IsKeyPressed(ImGuiKey.Y, false)) CommandManager.Redo();
        }
        EditorState.ValidateSelection();
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
                    var data = JsonSerializer.Serialize(EditorState.CurrentScene);
                    File.WriteAllText($"{Directory.GetCurrentDirectory()}/{EditorState.CurrentScene.Name}.scene", data);
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit")) {
                // Cache availability: Undo()/Redo() change CanUndo/CanRedo mid-frame,
                // so BeginDisabled/EndDisabled must be balanced against a snapshot.
                var canUndo = CommandManager.CanUndo;
                if (!canUndo) ImGui.BeginDisabled();
                if (ImGui.MenuItem("Undo", "Ctrl+Z")) CommandManager.Undo();
                if (!canUndo) ImGui.EndDisabled();

                var canRedo = CommandManager.CanRedo;
                if (!canRedo) ImGui.BeginDisabled();
                if (ImGui.MenuItem("Redo", "Ctrl+Y")) CommandManager.Redo();
                if (!canRedo) ImGui.EndDisabled();

                ImGui.EndMenu();
            }
            ImGui.EndMainMenuBar();
        }
    }

    private static void DrawAssetBrowser()
    {
        AssetBrowser.Render();
    }

    private static void Draw3dViewport()
    {
        if (!ImGui.Begin("3D Viewport"))
        {
            Gizmo.Cancel();
            EditorState.ViewportHovered = false;
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Preview Resolution", ref EditorState.ResScaleIndex, EditorState.ResScaleLabels, EditorState.ResScaleLabels.Length);
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
                EditorState.PendingSize = avail;
        }

        rlImGui.ImageRenderTextureFit(EditorState.ViewTexture, false);
        var hovered = ImGui.IsItemHovered();
        EditorState.ViewportHovered = hovered;
        var mouse = Picking.GetMouseInViewport();
        var gizmoUsed = Gizmo.Draw(EditorState.Selected, EditorState.CurrentCamera,
            ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), hovered);
        
        // if viewport hovered by mouse
        if (hovered && !gizmoUsed && mouse.HasValue
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            var ray = Raylib.GetScreenToWorldRayEx(mouse.Value, EditorState.CurrentCamera,
                EditorState.ViewTexture.Texture.Width, EditorState.ViewTexture.Texture.Height);
            EditorState.Selected = Picking.PickAtMouse(ray);
        }

        if (ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()
            && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
            && ImGui.IsKeyPressed(ImGuiKey.Delete, false)
            && EditorState.Selected != null)
            EditorState.DeleteGameObject(EditorState.Selected);

        ImGui.End();
    }

    private static void DrawInspector()
    {
        if (_editTarget != EditorState.Selected) FinishEdit();
        if (!ImGui.Begin("Inspector"))
        {
            FinishEdit();
            ImGui.End();
            return;
        }

        var target = EditorState.Selected;
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

    internal static void FinishEdit(bool record = true)
    {
        if (record) _finishEdit?.Invoke();
        _finishEdit = null;
        _editTarget = null;
        _editProperty = null;
    }

    private static void DrawHierarchy()
    {
        if (!ImGui.Begin("Hierarchy"))
        {
            ImGui.End();
            return;
        }
        
        if (ImGui.BeginPopupContextWindow("ctx_empty", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("Add SimpleCube"))
            {
                var oldGameObjectsList = EditorState.CurrentScene.GameObjects;
                var newGameObjectsList = new List<GameObject>(oldGameObjectsList) { new SimpleCube() };

                CommandManager.RecordChange(newGameObjects => EditorState.CurrentScene.GameObjects = newGameObjects, oldGameObjectsList, newGameObjectsList);
                EditorState.CurrentScene.GameObjects = newGameObjectsList;
            }
            ImGui.EndPopup();
        }

        GameObject? deleteTarget = null;
        HandleDrawGameObjectTreeRecursively(EditorState.CurrentScene.GameObjects, ref deleteTarget);
        if (ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()
            && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && !_openRenamePopup && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
            && ImGui.IsKeyPressed(ImGuiKey.Delete, false))
            deleteTarget = EditorState.Selected;
        if (deleteTarget != null) EditorState.DeleteGameObject(deleteTarget);
        DrawRenamePopup();

        ImGui.End();
    }

    private static void HandleDrawGameObjectTreeRecursively(List<GameObject> gameObjects, ref GameObject? deleteTarget)
    {
        foreach (var gameObject in gameObjects)
        {
            var hasChild = gameObject.Child is { Count: > 0 };
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                        | (hasChild ? ImGuiTreeNodeFlags.OpenOnArrow : ImGuiTreeNodeFlags.Leaf)
                        | (gameObject == EditorState.Selected ? ImGuiTreeNodeFlags.Selected : 0);

            // ## hides rest of string from user
            var opened = ImGui.TreeNodeEx($"   {gameObject.Name}###{gameObject.Id}", flags);
            
            if (!ImGui.IsItemToggledOpen() && ImGui.IsItemClicked(ImGuiMouseButton.Left))
                EditorState.Selected = gameObject;
            
            var iconMin = ImGui.GetItemRectMin() + new Vector2(
                ImGui.GetTreeNodeToLabelSpacing(), (ImGui.GetItemRectSize().Y - ImGui.GetFontSize()) * 0.5f);
            
            ImGui.GetWindowDrawList().AddImage((nint)GameObjectIcon.Id, iconMin,
                iconMin + new Vector2(ImGui.GetFontSize()));
            
            if (ImGui.BeginPopupContextItem($"ctx_{gameObject.Id}"))
            {
                if (ImGui.MenuItem("Rename"))
                {
                    FinishEdit();
                    EditorState.Selected = gameObject;
                    _renameTarget = gameObject;
                    _renameScene = EditorState.CurrentScene;
                    _renameName = gameObject.Name;
                    _openRenamePopup = true;
                }

                if (ImGui.MenuItem("Delete", "Del"))
                {
                    // Apply after the entire tree has been drawn, never during enumeration.
                    deleteTarget = gameObject;
                }
                ImGui.EndPopup();
            }
            
            if (opened)
            {
                if (hasChild) HandleDrawGameObjectTreeRecursively(gameObject.Child!, ref deleteTarget);
                ImGui.TreePop();
            }
        }
    }

    private static void DrawRenamePopup()
    {
        var focusName = _openRenamePopup;
        if (_openRenamePopup)
        {
            ImGui.OpenPopup("Rename Game Object");
            _openRenamePopup = false;
        }

        if (!ImGui.BeginPopupModal("Rename Game Object", ImGuiWindowFlags.AlwaysAutoResize)) return;

        if (_renameTarget == null || _renameScene != EditorState.CurrentScene
            || !EditorState.Contains(EditorState.CurrentScene.GameObjects, _renameTarget))
        {
            _renameTarget = null;
            _renameScene = null;
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        if (focusName) ImGui.SetKeyboardFocusHere();
        var submitted = ImGui.InputText("Name", ref _renameName, 256,
            ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
        var valid = !string.IsNullOrWhiteSpace(_renameName);
        ImGui.BeginDisabled(!valid);
        var accept = ImGui.Button("Rename") || submitted;
        ImGui.EndDisabled();
        ImGui.SameLine();
        var cancel = ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape, false);
        if ((accept && valid) || cancel)
        {
            if (!cancel) EditorState.RenameGameObject(_renameTarget, _renameName);
            _renameTarget = null;
            _renameScene = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private static void HandleNewProject()
    {
        if (_isOpenNewProjectModal)
            ImGui.OpenPopup("New Project modal");

        if (ImGui.BeginPopupModal("New Project modal", ImGuiWindowFlags.Modal))
        {
            EditorState.CurrentProject = new Project();
            ImGui.InputText("Project name", ref EditorState.CurrentProject.Name, 64);
            ImGui.InputText("Where project will be", ref EditorState.WorkingDirectory, 256);
            ImGui.Separator();
            
            if (ImGui.Button("Accept"))
            {
                var data = JsonSerializer.Serialize(EditorState.CurrentProject);
                File.WriteAllText($"{EditorState.WorkingDirectory}/{EditorState.CurrentProject.Name}.proj", data);
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
