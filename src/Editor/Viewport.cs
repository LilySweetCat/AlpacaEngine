using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace AlpacaEngine.Editor;

public static class Viewport
{
    public static void Render()
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
        var gizmoUsed = Gizmo.Draw(EditorState.Selected, EditorState.CurrentCamera, imageMin, imageMax, hovered);

        // A plain click (no gizmo, no camera orbit with Alt) picks the object under the cursor.
        if (hovered && !gizmoUsed && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && !Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            var ray = Picking.GetMouseRay(mouse, imageMin, imageMax, EditorState.CurrentCamera);
            EditorState.Selected = Picking.PickAtMouse(ray);
        }

        HandleDelete();

        ImGui.End();
    }

    internal static void HandleDelete()
    {
        if (ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()
            && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
            && ImGui.IsKeyPressed(ImGuiKey.Delete, false)
            && EditorState.Selected != null)
        {
            EditorState.DeleteGameObject(EditorState.Selected);
        }
    }

}
