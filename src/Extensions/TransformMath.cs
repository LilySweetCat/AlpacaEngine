using System.Numerics;

namespace AlpacaEngine.Extensions;

internal static class TransformMath
{
    internal static Matrix4x4 Compose(Vector3 position, Vector3 rotation, Vector3 scale) =>
        Matrix4x4.CreateScale(scale)
        * Matrix4x4.CreateFromQuaternion(GameObjects.GameObject.Orientation(rotation))
        * Matrix4x4.CreateTranslation(position);

    internal static bool MatricesMatch(Matrix4x4 a, Matrix4x4 b)
    {
        return Close(a.M11, b.M11) && Close(a.M12, b.M12) && Close(a.M13, b.M13) && Close(a.M14, b.M14)
            && Close(a.M21, b.M21) && Close(a.M22, b.M22) && Close(a.M23, b.M23) && Close(a.M24, b.M24)
            && Close(a.M31, b.M31) && Close(a.M32, b.M32) && Close(a.M33, b.M33) && Close(a.M34, b.M34)
            && Close(a.M41, b.M41) && Close(a.M42, b.M42) && Close(a.M43, b.M43) && Close(a.M44, b.M44);

        static bool Close(float x, float y) => float.IsFinite(x) && float.IsFinite(y)
            && MathF.Abs(x - y) <= 0.0001f * MathF.Max(1, MathF.Max(MathF.Abs(x), MathF.Abs(y)));
    }
}
