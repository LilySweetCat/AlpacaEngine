using System.Numerics;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace AlpacaEngine.GameObjects;

[JsonPolymorphic]
[JsonDerivedType(typeof(SimpleCube), "simpleCube")]
public abstract class GameObject
{
    public Guid Id = Guid.NewGuid();
    public string Name = "Game Object";

    public Vector3 LocalPosition = Vector3.Zero;
    public Vector3 LocalScale = Vector3.One;
    public Vector3 LocalRotation = Vector3.Zero;

    [JsonIgnore] public GameObject? Parent;

    public List<GameObject>? Child
    {
        get;
        set
        {
            field = value;
            if (value == null) return;
            foreach (var child in value) child.Parent = this;
        }
    }

    public Vector3 WorldPosition => Parent == null ? LocalPosition : Vector3.Transform(LocalPosition, Parent.WorldMatrix);

    // local first, then parent
    public Quaternion WorldRotation => Orientation(LocalRotation) * (Parent?.WorldRotation ?? Quaternion.Identity);

    // ponytail: componentwise; wrong for rotated non-uniform parent scale, fix with full matrix extraction if needed
    public Vector3 WorldScale => Parent == null ? LocalScale : LocalScale * Parent.WorldScale;

    public Matrix4x4 WorldMatrix
    {
        get
        {
            var local = Matrix4x4.CreateScale(LocalScale)
                        * Matrix4x4.CreateFromQuaternion(Orientation(LocalRotation))
                        * Matrix4x4.CreateTranslation(LocalPosition);
            return Parent == null ? local : local * Parent.WorldMatrix;
        }
    }

    [JsonIgnore] public Matrix4x4 WorldMatrixInverted
    {
        get
        {
            Matrix4x4.Invert(WorldMatrix, out var inverted);
            return inverted;
        }
    }

    public static Quaternion Orientation(Vector3 degrees) => Quaternion.CreateFromYawPitchRoll(
        degrees.Y * MathF.PI / 180, degrees.X * MathF.PI / 180, degrees.Z * MathF.PI / 180);

    public virtual void PushTransform()
    {
        Rlgl.PushMatrix();
        
        Rlgl.Translatef(LocalPosition.X, LocalPosition.Y, LocalPosition.Z);
        
        Rlgl.Rotatef(LocalRotation.Y, 0, 1, 0);
        Rlgl.Rotatef(LocalRotation.X, 1, 0, 0);
        Rlgl.Rotatef(LocalRotation.Z, 0, 0, 1);
    }

    public virtual void PopTransform() => Rlgl.PopMatrix();

    public virtual void Render()
    {

    }

    public virtual void RenderWireframe()
    {

    }
}
