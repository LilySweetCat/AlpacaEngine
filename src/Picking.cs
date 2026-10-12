using System.Numerics;
using ImGuiNET;

namespace AlpacaEngine;

public static class Picking
{
    public static Vector2? GetMouseInViewport()
    {
        var imageMax = ImGui.GetItemRectMax();
        var imageMin = ImGui.GetItemRectMin();
        
        var mousePos = ImGui.GetMousePos();
        var localPos = mousePos - imageMin;
        var size = imageMax - imageMin;
        
        if (localPos.X < 0 || localPos.Y < 0 || localPos.X > size.X || localPos.Y > size.Y) return null;
        
        var rtSize   = new Vector2(Editor.ViewTexture.Texture.Width, Editor.ViewTexture.Texture.Height);
        var uv       = localPos / size;
        var rtPixel  = uv * rtSize;
        rtPixel.Y    = rtSize.Y - rtPixel.Y;      // Y-флип: origin RT — снизу
        return rtPixel;
    }
}