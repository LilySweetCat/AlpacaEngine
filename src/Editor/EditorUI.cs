using AlpacaEngine.Managers;
using ImGuiNET;
using AlpacaEngine.Logging;

namespace AlpacaEngine.Editor;

public static class EditorUI
{
    public static void Render()
    {
        EditorState.ValidateSelection();
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport());

        // TOP MENU
        DrawTopMenu();

        // PANEL A: Hierarchy
        Hierarchy.Render();

        // PANEL B: Inspector
        Inspector.Render();

        // PANEL C: 3D Viewport
        Viewport.Render();

        // PANEL D: Assets browser
        AssetBrowser.Render();

        LogWindow.Render();

        HandleShortcuts();
    }

    internal static void HandleShortcuts()
    {
        // Active widgets keep their own Undo (notably InputText); do not interrupt a drag.
        if (ImGui.IsAnyItemActive() || ImGui.GetIO().WantTextInput || Gizmo.IsDragging
            || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)) return;
        
        if (ImGui.GetIO().KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false)) CommandManager.Undo();

            else if (ImGui.IsKeyPressed(ImGuiKey.Y, false)) CommandManager.Redo();
        }
        else if (ImGui.GetIO().KeyShift)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.D, false) && (Hierarchy.IsFocused || Viewport.IsFocused))
                EditorState.DuplicateSelection();
        }
    }

    private static void DrawTopMenu()
    {
        if (!ImGui.BeginMainMenuBar()) return;

        if (ImGui.BeginMenu("Scene"))
        {
            if (ImGui.MenuItem("Save Scene", "Ctrl+S")) SaveScene();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
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

            var canDuplicate = EditorState.SelectedObjects.Count > 0 && !Gizmo.IsDragging;
            ImGui.BeginDisabled(!canDuplicate);

            if (ImGui.MenuItem("Duplicate", "Shift+D")) EditorState.DuplicateSelection();

            ImGui.EndDisabled();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Editor"))
        {
            ImGui.MenuItem("Logs", "", ref LogWindow.IsOpen);
            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    private static void SaveScene()
    {
        try
        {
            EditorState.SaveScene();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or System.Text.Json.JsonException
            or System.Security.SecurityException)
        {
            Logger.Write(LogLevel.Error, "Cannot save scene.", exception);
            LogWindow.IsOpen = true;
        }
    }

}
