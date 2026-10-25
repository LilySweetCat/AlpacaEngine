using System.Numerics;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using Raylib_cs;

namespace AlpacaEngine.Editor;

// Mouse-to-object selection in the 3D viewport.
public static class Picking
{
    // The camera ray through the mouse position. The mouse point is first converted
    // from UI coordinates into render-texture pixels, because the texture can be
    // smaller or larger than the viewport image on screen.
    public static Ray GetMouseRay(Vector2 mouse, Vector2 min, Vector2 max, Camera3D camera)
    {
        var texture = EditorState.ViewTexture.Texture;
        var pixel = (mouse - min) / (max - min) * new Vector2(texture.Width, texture.Height);

        return Raylib.GetScreenToWorldRayEx(pixel, camera, texture.Width, texture.Height);
    }

    public static GameObject? PickAtMouse(Ray ray)
    {
        GameObject? bestGameObject = null;
        var bestDist = float.MaxValue;

        PickRecursive(EditorState.CurrentScene.GameObjects, ray, ref bestGameObject, ref bestDist);
        return bestGameObject;
    }

    private static void PickRecursive(List<GameObject> gameObjects, Ray ray, ref GameObject? bestGameObject, ref float bestDist)
    {
        foreach (var gameObject in gameObjects)
        {
            var world = gameObject.WorldMatrix;
            var nearestDistance = float.MaxValue;

            if (Matrix4x4.Invert(world, out var inverse))
            {
                var direction = Vector3.TransformNormal(ray.Direction, inverse);
                var localRay = new Ray(Vector3.Transform(ray.Position, inverse), Vector3.Normalize(direction));

                if (gameObject is SimpleCube) TestBounds(new BoundingBox(new Vector3(-0.5f), new Vector3(0.5f)));

                foreach (var component in gameObject.Components)
                    if (component is ModelRenderComponent renderer && renderer.TryGetModel(out var model))
                        TestBounds(Raylib.GetModelBoundingBox(model));

                void TestBounds(BoundingBox bounds)
                {
                    var collision = Raylib.GetRayCollisionBox(localRay, bounds);

                    if (!collision.Hit || collision.Distance < 0f) return;

                    var point = Vector3.Transform(localRay.Position + localRay.Direction * collision.Distance, world);
                    var distance = Vector3.Distance(ray.Position, point);

                    if (distance < nearestDistance) nearestDistance = distance;
                }
            }

            if (nearestDistance < bestDist)
            {
                bestDist = nearestDistance;
                bestGameObject = gameObject;
            }

            if (gameObject.Child != null) PickRecursive(gameObject.Child, ray, ref bestGameObject, ref bestDist);
        }
    }
}
