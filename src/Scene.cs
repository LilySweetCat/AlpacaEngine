using System.Numerics;
using AlpacaEngine.GameObjects;

namespace AlpacaEngine;

public class Scene
{
    public string Name { get; set; } = "New Scene";

    public List<GameObject> GameObjects { get; set; } = [new SimpleCube(){Child = [new SimpleCube(){LocalPosition = Vector3.One}] }]; // default scene
}
