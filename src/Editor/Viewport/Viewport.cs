using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine.Editor.Viewport;

public static class Viewport
{
    internal static bool IsFocused { get; private set; }

    public static void Render()
    {
        if (!ImGui.Begin("3D Viewport"))
        {
            IsFocused = false;
            Gizmo.Gizmo.Cancel();
            EditorState.ViewportHovered = false;
            ImGui.End();
            return;
        }

        IsFocused = ImGui.IsWindowFocused();

        ImGui.SetNextItemWidth(180);
        ImGui.Combo("Preview Resolution", ref EditorState.ResScaleIndex, EditorState.ResScaleLabels, EditorState.ResScaleLabels.Length);

        var mode = (int)Gizmo.Gizmo.Mode;
        ImGui.SetNextItemWidth(120);

        if (ImGui.Combo("Gizmo", ref mode, "Move\0Rotate\0Scale\0"))
        {
            Gizmo.Gizmo.Cancel();
            Gizmo.Gizmo.Mode = (Gizmo.Gizmo.Operation)mode;
        }

        ImGui.SameLine();

        if (ImGui.Checkbox("Local", ref Gizmo.Gizmo.Local)) Gizmo.Gizmo.Cancel();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Use local axes instead of world axes. Individual Origins uses each object's local axes; other pivots use the active object's axes.");

        var pivot = (int)Gizmo.Gizmo.Pivot;
        ImGui.SetNextItemWidth(180);

        if (ImGui.Combo("Pivot", ref pivot, "Selection Center\0Individual Origins\0Active Object\0"))
        {
            Gizmo.Gizmo.Cancel();
            Gizmo.Gizmo.Pivot = (Gizmo.Gizmo.PivotPoint)pivot;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Selection Center: average world position. Individual Origins: each object's origin. Active Object: last selected origin.");

        if (!ImGui.IsWindowCollapsed())
        {
            var avail = ImGui.GetContentRegionAvail();

            // Min size: ignore tiny leftovers of a collapsed panel.
            if (avail is { X: >= 16, Y: >= 16 }) EditorState.PendingSize = avail;
        }

        rlImGui.ImageRenderTextureFit(EditorState.ViewTexture, false);

        var hovered = ImGui.IsItemHovered();
        EditorState.ViewportHovered = hovered;

        // The image has no keyboard navigation of its own; clicking it focuses the viewport.
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) ImGui.SetWindowFocus();

        // The image rect must be captured before the gizmo draws its own items on top.
        var imageMin = ImGui.GetItemRectMin();
        var imageMax = ImGui.GetItemRectMax();
        var mouse = ImGui.GetMousePos();

        // Ctrl-click belongs to selection, even when the cursor is over a gizmo handle.
        var gizmoUsed = Gizmo.Gizmo.Draw(ImGui.GetIO().KeyCtrl && !Gizmo.Gizmo.IsDragging ? null : EditorState.Selected,
            EditorState.CurrentCamera, imageMin, imageMax, hovered);

        // A plain click (no gizmo, no camera orbit with Alt) picks the object under the cursor.
        if (hovered && !gizmoUsed && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            var ray = Picking.GetMouseRay(mouse, imageMin, imageMax, EditorState.CurrentCamera);
            EditorState.Select(Picking.PickAtMouse(ray), ImGui.GetIO().KeyCtrl);
        }

        HandleDelete();

        ImGui.End();
    }

    internal static void HandleDelete()
    {
        if (ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()
            && !ImGui.GetIO().WantTextInput && !Gizmo.Gizmo.IsDragging
            && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
            && ImGui.IsKeyPressed(ImGuiKey.Delete, false)
            && EditorState.Selected != null)
        {
            EditorState.DeleteGameObject(EditorState.Selected);
        }
    }

}
