using System.Text.Json.Serialization;

namespace AlpacaEngine.GameObjects.Components;

[JsonPolymorphic]
[JsonDerivedType(typeof(ModelRenderComponent), "modelRenderer")]
public interface IComponent
{
    public void DrawOnInspector();
    public void Process(GameObject gameObject);
    public void ProcessOnSelected(GameObject gameObject);
}