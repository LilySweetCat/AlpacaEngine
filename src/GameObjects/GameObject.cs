using System.Numerics;
using System.Text.Json.Serialization;
using Raylib_cs;

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

    public virtual void RenderWireframe()
    {
        
    }
    
    public virtual BoundingBox GetBoundingBox()
    {
        var half = Vector3.Abs(Scale) / 2f;
        var rotation = Matrix4x4.CreateFromQuaternion(Gizmo.Orientation(Rotation));
        var extent = new Vector3(
            MathF.Abs(rotation.M11) * half.X + MathF.Abs(rotation.M21) * half.Y + MathF.Abs(rotation.M31) * half.Z,
            MathF.Abs(rotation.M12) * half.X + MathF.Abs(rotation.M22) * half.Y + MathF.Abs(rotation.M32) * half.Z,
            MathF.Abs(rotation.M13) * half.X + MathF.Abs(rotation.M23) * half.Y + MathF.Abs(rotation.M33) * half.Z);
        
        return new BoundingBox(Position - extent, Position + extent);
    }
    
}
