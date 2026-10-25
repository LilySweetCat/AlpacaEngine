using System.Numerics;

namespace AlpacaEngine.Extensions;

public static class QuaternionExtension
{
    extension(Quaternion quaternion)
    {
        // Convert the quaternion to Euler angles in degrees: X pitch, Y yaw, Z roll,
        // the same order GameObject.Orientation uses to build a quaternion.
        public Vector3 ToEulerAngles()
        {
            var m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(quaternion));

            var pitch = MathF.Asin(Math.Clamp(-m.M32, -1, 1));
            var yaw = MathF.Atan2(m.M31, m.M33);
            var roll = MathF.Atan2(m.M12, m.M22);

            if (!(MathF.Abs(MathF.Cos(pitch)) < 0.0001f)) return new Vector3(pitch, yaw, roll) * (180 / MathF.PI);

            yaw = MathF.Atan2(-m.M13, m.M11);
            roll = 0;
            return new Vector3(pitch, yaw, roll) * (180 / MathF.PI);
        }
    }
}
