using AlpacaEngine.Managers;
using ImGuiNET;

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

        // Active widgets keep their own Undo (notably InputText); do not interrupt a drag.
        if (!ImGui.IsAnyItemActive() && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
            && ImGui.GetIO().KeyCtrl)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false)) CommandManager.Undo();

            else if (ImGui.IsKeyPressed(ImGuiKey.Y, false)) CommandManager.Redo();
        }
    }

    private static void DrawTopMenu()
    {
        if (!ImGui.BeginMainMenuBar()) return;

        if (ImGui.BeginMenu("Scene"))
        {
            if (ImGui.MenuItem("Save Scene", "Ctrl+S")) EditorState.SaveScene();

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

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

}
