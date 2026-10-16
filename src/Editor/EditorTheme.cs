using System.Numerics;
using ImGuiNET;

namespace AlpacaEngine.Editor;

internal static class EditorTheme
{
    public static void Apply(float dpiScale)
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();

        style.WindowPadding = new Vector2(10, 10);
        style.FramePadding = new Vector2(8, 5);
        style.ItemSpacing = new Vector2(8, 6);
        style.ItemInnerSpacing = new Vector2(6, 4);
        style.WindowRounding = 8;
        style.ChildRounding = 6;
        style.FrameRounding = 5;
        style.PopupRounding = 6;
        style.ScrollbarRounding = 8;
        style.GrabRounding = 4;
        style.TabRounding = 5;
        style.WindowBorderSize = 1;
        style.ChildBorderSize = 1;
        style.PopupBorderSize = 1;
        style.FrameBorderSize = 0;
        style.TabBorderSize = 0;
        style.ScrollbarSize = 13;
        style.GrabMinSize = 10;

        var colors = style.Colors;
        var accent = new Vector4(0.78f, 0.57f, 0.32f, 1);
        var accentHovered = new Vector4(0.90f, 0.68f, 0.41f, 1);
        var selection = new Vector4(0.34f, 0.29f, 0.22f, 1);

        colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.90f, 0.90f, 1);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.49f, 0.49f, 0.49f, 1);
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.12f, 1);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.10f, 0.10f, 1);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.15f, 0.15f, 0.15f, 0.98f);
        colors[(int)ImGuiCol.Border] = new Vector4(0.25f, 0.25f, 0.25f, 1);
        colors[(int)ImGuiCol.BorderShadow] = Vector4.Zero;
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.18f, 0.18f, 0.18f, 1);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.25f, 0.25f, 0.25f, 1);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.30f, 0.30f, 0.30f, 1);
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.09f, 0.09f, 0.09f, 1);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.16f, 0.16f, 0.16f, 1);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.09f, 0.09f, 0.09f, 1);
        colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.14f, 0.14f, 0.14f, 1);
        colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.10f, 0.10f, 0.10f, 1);
        colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.29f, 0.29f, 0.29f, 1);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.38f, 0.38f, 0.38f, 1);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.46f, 0.46f, 0.46f, 1);
        colors[(int)ImGuiCol.CheckMark] = accent;
        colors[(int)ImGuiCol.SliderGrab] = accent;
        colors[(int)ImGuiCol.SliderGrabActive] = accentHovered;
        colors[(int)ImGuiCol.Button] = new Vector4(0.23f, 0.23f, 0.23f, 1);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.31f, 0.31f, 0.31f, 1);
        colors[(int)ImGuiCol.ButtonActive] = selection;
        colors[(int)ImGuiCol.Header] = new Vector4(0.23f, 0.23f, 0.23f, 1);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.30f, 0.30f, 0.30f, 1);
        colors[(int)ImGuiCol.HeaderActive] = selection;
        colors[(int)ImGuiCol.Separator] = new Vector4(0.25f, 0.25f, 0.25f, 1);
        colors[(int)ImGuiCol.SeparatorHovered] = accent;
        colors[(int)ImGuiCol.SeparatorActive] = accentHovered;
        colors[(int)ImGuiCol.ResizeGrip] = new Vector4(0.50f, 0.50f, 0.50f, 0.20f);
        colors[(int)ImGuiCol.ResizeGripHovered] = accent;
        colors[(int)ImGuiCol.ResizeGripActive] = accentHovered;
        colors[(int)ImGuiCol.Tab] = new Vector4(0.13f, 0.13f, 0.13f, 1);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.29f, 0.29f, 0.29f, 1);
        colors[(int)ImGuiCol.TabSelected] = new Vector4(0.23f, 0.23f, 0.23f, 1);
        colors[(int)ImGuiCol.TabSelectedOverline] = accent;
        colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.11f, 0.11f, 0.11f, 1);
        colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.18f, 0.18f, 0.18f, 1);
        colors[(int)ImGuiCol.TabDimmedSelectedOverline] = new Vector4(0.40f, 0.40f, 0.40f, 1);
        colors[(int)ImGuiCol.DockingPreview] = new Vector4(accent.X, accent.Y, accent.Z, 0.35f);
        colors[(int)ImGuiCol.DockingEmptyBg] = new Vector4(0.08f, 0.08f, 0.08f, 1);
        colors[(int)ImGuiCol.PlotLines] = new Vector4(0.65f, 0.65f, 0.65f, 1);
        colors[(int)ImGuiCol.PlotLinesHovered] = accentHovered;
        colors[(int)ImGuiCol.PlotHistogram] = accent;
        colors[(int)ImGuiCol.PlotHistogramHovered] = accentHovered;
        colors[(int)ImGuiCol.TableHeaderBg] = new Vector4(0.20f, 0.20f, 0.20f, 1);
        colors[(int)ImGuiCol.TableBorderStrong] = new Vector4(0.28f, 0.28f, 0.28f, 1);
        colors[(int)ImGuiCol.TableBorderLight] = new Vector4(0.22f, 0.22f, 0.22f, 1);
        colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(accent.X, accent.Y, accent.Z, 0.30f);
        colors[(int)ImGuiCol.DragDropTarget] = accentHovered;
        colors[(int)ImGuiCol.NavCursor] = accent;

        style.ScaleAllSizes(dpiScale);
    }
}
