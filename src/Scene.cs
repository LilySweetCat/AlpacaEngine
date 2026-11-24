using System.Numerics;
using AlpacaEngine.GameObjects;

namespace AlpacaEngine;

public class Scene
{
    public string Name { get; set; } = "New Scene";

    public Vector3 BackgroundColor = Vector3.Zero;

    public List<GameObject> GameObjects { get; set; } = []; // default scene
}
