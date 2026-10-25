using System.Numerics;
using System.Text.Json.Serialization;
using AlpacaEngine.GameObjects.Components;
using Raylib_cs;

namespace AlpacaEngine.GameObjects;

[JsonPolymorphic]
[JsonDerivedType(typeof(SimpleCube), "simpleCube")]
public class GameObject
{
    public Guid Id = Guid.NewGuid();
    public string Name = "Game Object";

    public Vector3 LocalPosition = Vector3.Zero;
    public Vector3 LocalScale = Vector3.One;
    public Vector3 LocalRotation = Vector3.Zero;

    public List<IComponent> Components = [];

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

    public Quaternion WorldRotation => (Parent?.WorldRotation ?? Quaternion.Identity) * Orientation(LocalRotation);

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

    public virtual unsafe void PushTransform()
    {
        Rlgl.PushMatrix();
        var m = WorldMatrix;
        // System.Numerics uses row vectors. These rows are the columns of the
        // equivalent OpenGL matrix; pass them directly, without Raymath conversion.
        float* values = stackalloc float[16]
        {
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44
        };
        Rlgl.MultMatrixf(values);
    }

    public virtual void PopTransform() => Rlgl.PopMatrix();

    public virtual void Render()
    {
        foreach (var component in Components) component.Process(this);
    }

    public virtual void RenderOnSelected()
    {
        foreach (var component in Components) component.ProcessOnSelected(this);
    }
}
