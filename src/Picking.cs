using System.Numerics;
using ImGuiNET;
using Raylib_cs;

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
        return rtPixel;
    }
    
    public static Ray ScreenPixelToRay(Vector2 rtPixel, Camera3D cam, int rtW, int rtH)
    {
        return Raylib.GetScreenToWorldRayEx(rtPixel, cam, rtW, rtH);
    }
    
    public static GameObject? PickAtMouse(Ray ray)
    {
        GameObject? bestGameObject = null;
        var bestDist = float.MaxValue;

        foreach (var gameObject in Editor.CurrentScene.GameObjects)
        {
            var inverse = Quaternion.Inverse(Gizmo.Orientation(gameObject.Rotation));
            var localRay = new Ray(Vector3.Transform(ray.Position - gameObject.Position, inverse),
                Vector3.Transform(ray.Direction, inverse));
            var half = Vector3.Abs(gameObject.Scale) / 2;
            var collision = Raylib.GetRayCollisionBox(localRay, new BoundingBox(-half, half));
            
            if (!collision.Hit || collision.Distance < 0f) continue;
            if (!(collision.Distance < bestDist)) continue;
            
            bestDist = collision.Distance;
            bestGameObject = gameObject;
        }
        return bestGameObject;
    }
}
