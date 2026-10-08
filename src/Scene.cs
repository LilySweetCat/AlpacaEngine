using System.Text.Json.Serialization;

namespace AlpacaEngine;

[Serializable]
public class Scene
{
    public string Name { get; set; } = "New Scene";
    
    public Guid Id { get; set; } = Guid.NewGuid();

    public List<GameObject> GameObjects { get; set; } = [new SimpleCube()]; // default scene
}