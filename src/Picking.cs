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
    

    public static GameObject? PickAtMouse(Ray ray)
    {
        GameObject? bestGameObject = null;
        var bestDist = float.MaxValue;

        PickRecursive(Editor.CurrentScene.GameObjects, ray, ref bestGameObject, ref bestDist);
        return bestGameObject;
    }

    private static void PickRecursive(List<GameObject> gameObjects, Ray ray, ref GameObject? bestGameObject, ref float bestDist)
    {
        foreach (var gameObject in gameObjects)
        {
            var inverse = Quaternion.Inverse(gameObject.WorldRotation);
            var localRay = new Ray(Vector3.Transform(ray.Position - gameObject.WorldPosition, inverse),
                Vector3.Transform(ray.Direction, inverse));
            var half = Vector3.Abs(gameObject.WorldScale) / 2;
            var collision = Raylib.GetRayCollisionBox(localRay, new BoundingBox(-half, half));

            if (collision.Hit && collision.Distance >= 0f && collision.Distance < bestDist)
            {
                bestDist = collision.Distance;
                bestGameObject = gameObject;
            }

            if (gameObject.Child != null) PickRecursive(gameObject.Child, ray, ref bestGameObject, ref bestDist);
        }
    }
}
