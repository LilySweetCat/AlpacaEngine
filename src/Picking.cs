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
        rtPixel.Y    = rtSize.Y - rtPixel.Y;
        return rtPixel;
    }
    
    public static Ray ScreenPixelToRay(Vector2 rtPixel, Camera3D cam, int rtW, int rtH)
    {
        // NDC: [-1..1]
        var ndc = new Vector2(rtPixel.X / rtW * 2f - 1f, rtPixel.Y / rtH * 2f - 1f);

        var view = Raylib.GetCameraMatrix(cam);                                  // view-matrix
        var proj = Raylib.GetCameraProjectionMatrix(ref cam, (float)rtW / rtH);
        Matrix4x4.Invert(Matrix4x4.Transpose(proj * view), out var invViewProj); 

        var n = Vector4.Transform(new Vector4(ndc.X, ndc.Y, -1f, 1f), invViewProj); n /= n.W; // near
        var f = Vector4.Transform(new Vector4(ndc.X, ndc.Y,  1f, 1f), invViewProj); f /= f.W; // far

        var p0 = new Vector3(n.X, n.Y, n.Z);
        var p1 = new Vector3(f.X, f.Y, f.Z);
        return new Ray(p0, Vector3.Normalize(p1 - p0));
    }
    
    public static GameObject? PickAtMouse(Ray ray)
    {
        GameObject? bestGameObject = null;
        var bestDist = float.MaxValue;

        foreach (var gameObject in Editor.CurrentScene.GameObjects)
        {
            var collision = Raylib.GetRayCollisionBox(ray, gameObject.GetBoundingBox());
            
            if (!collision.Hit || collision.Distance < 0f) continue;
            if (!(collision.Distance < bestDist)) continue;
            
            bestDist = collision.Distance;
            bestGameObject = gameObject;
        }
        return bestGameObject;
    }
}