using System.Numerics;
using System.Text.Json.Serialization;

namespace AlpacaEngine;

[JsonPolymorphic]
[JsonDerivedType(typeof(SimpleCube), "simpleCube")]
public abstract class GameObject
{
    public Guid Id = Guid.NewGuid();
    public string Name = "Game Object";

    public Vector3 Position = Vector3.Zero;
    public Vector3 Scale = Vector3.One;
    public Vector3 Rotation = Vector3.Zero;

    public Guid? ParentId = null;

    public virtual void Render()
    {

    }
}
