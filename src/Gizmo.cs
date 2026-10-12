using System.Numerics;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine;

public static class Gizmo
{
    public enum Operation { Move, Rotate, Scale }
    public static Operation Mode;
    public static bool Local;
    public static bool IsDragging => _target != null;
    private static GameObject? _target;
    private static int _axis;
    private static Vector3 _direction, _planeNormal, _startHit, _position, _scale, _rotation;
    private static float _length, _lastAngle, _angle;
    private static Operation _operation;
    private static readonly uint[] Colors = [0xff5b48e5, 0xff38cd83, 0xfff28a45];

    public static Quaternion Orientation(Vector3 degrees) => Quaternion.CreateFromYawPitchRoll(
        degrees.Y * MathF.PI / 180, degrees.X * MathF.PI / 180, degrees.Z * MathF.PI / 180);

    public static Vector3 EulerDegrees(Quaternion quaternion)
    {
        var m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(quaternion));
        var pitch = MathF.Asin(Math.Clamp(-m.M32, -1, 1));
        var yaw = MathF.Atan2(m.M31, m.M33);
        var roll = MathF.Atan2(m.M12, m.M22);
        if (MathF.Abs(MathF.Cos(pitch)) < 0.0001f)
        {
            yaw = MathF.Atan2(-m.M13, m.M11);
            roll = 0;
        }
        return new Vector3(pitch, yaw, roll) * (180 / MathF.PI);
    }

    public static bool IntersectPlane(Ray ray, Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        var denominator = Vector3.Dot(ray.Direction, normal);
        if (MathF.Abs(denominator) < 0.0001f) return false;
        var distance = Vector3.Dot(point - ray.Position, normal) / denominator;
        if (distance < 0 || !float.IsFinite(distance)) return false;
        hit = ray.Position + ray.Direction * distance;
        return true;
    }

    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));

    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var t = edge.LengthSquared() < 0.0001f ? 0 :
            Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + edge * t);
    }

    public static void Cancel(bool restore = false)
    {
        if (restore && _target != null)
        {
            _target.Position = _position;
            _target.Scale = _scale;
            _target.Rotation = _rotation;
        }
        _target = null;
    }

    // Returns true while the gizmo owns this mouse interaction (including release).
    public static bool Draw(GameObject? selected, Camera3D camera, Vector2 min, Vector2 max, bool hovered)
    {
        var size = max - min;
        if (selected != _target && IsDragging) Cancel();
        if (selected == null || size.X < 1 || size.Y < 1) { Cancel(); return false; }
        var mouse = ImGui.GetMousePos();
        var texture = Editor.ViewTexture.Texture;
        var ray = Picking.ScreenPixelToRay((mouse - min) / size * new Vector2(texture.Width, texture.Height),
            camera, texture.Width, texture.Height);
        var consumed = IsDragging;
        if (IsDragging)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) Cancel(true);
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) Cancel();
            else if (IntersectPlane(ray, _position, _planeNormal, out var hit))
            {
                if (_operation == Operation.Move)
                    selected.Position = _position + (_axis >= 3 ? hit - _startHit :
                        _direction * Vector3.Dot(hit - _startHit, _direction));
                else if (_operation == Operation.Scale)
                {
                    var factor = MathF.Max(0.01f, 1 + Vector3.Dot(hit - _startHit, _direction) / _length);
                    var scale = _scale;
                    scale[_axis] = MathF.Max(0.001f, _scale[_axis] * factor);
                    selected.Scale = scale;
                }
                else if ((hit - _position).LengthSquared() > 0.000001f)
                {
                    var angle = SignedAngle(_startHit - _position, hit - _position, _direction);
                    _angle += MathF.IEEERemainder(angle - _lastAngle, 2 * MathF.PI);
                    _lastAngle = angle;
                    selected.Rotation = EulerDegrees(Quaternion.CreateFromAxisAngle(_direction, _angle)
                        * Orientation(_rotation));
                }
            }
        }

        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var depth = Vector3.Dot(selected.Position - camera.Position, forward);
        if (depth <= 0.01f && camera.Projection == CameraProjection.Perspective) return consumed;
        var worldHeight = camera.Projection == CameraProjection.Perspective
            ? 2 * depth * MathF.Tan(camera.FovY * MathF.PI / 360) : camera.FovY;
        var length = worldHeight * 85 / size.Y;
        var rotation = Orientation(selected.Rotation);
        var local = Local || Mode == Operation.Scale;
        Vector3 Axis(int i) => local ? Vector3.Transform(UnitAxis(i), rotation) : UnitAxis(i);
        Vector2 Project(Vector3 p) => min + Raylib.GetWorldToScreenEx(p, camera, texture.Width, texture.Height)
            / new Vector2(texture.Width, texture.Height) * size;
        var origin = Project(selected.Position);
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(min, max, true);
        var best = 9f;
        var hoverAxis = -1;
        if (Mode == Operation.Move)
        {
            for (var i = 0; i < 3; i++)
            {
                var u = Axis((i + 1) % 3);
                var v = Axis((i + 2) % 3);
                var a = Project(selected.Position + length * (u + v) * 0.25f);
                var b = Project(selected.Position + length * (u * 0.45f + v * 0.25f));
                var c = Project(selected.Position + length * (u + v) * 0.45f);
                var d = Project(selected.Position + length * (u * 0.25f + v * 0.45f));
                if (InsideQuad(mouse, a, b, c, d)) { hoverAxis = i + 3; best = 0; }
                var color = (IsDragging ? _axis == i + 3 : hovered && hoverAxis == i + 3)
                    ? 0x9955ffffu : (Colors[i] & 0x00ffffff) | 0x55000000;
                draw.AddQuadFilled(a, b, c, d, color);
            }
        }
        // Hit testing uses screen pixels, independent of scene preview resolution.
        for (var i = 0; i < 3; i++)
        {
            var axis = Axis(i);
            if (Mode == Operation.Rotate)
            {
                var u = Axis((i + 1) % 3);
                var v = Axis((i + 2) % 3);
                for (var j = 0; j < 64; j++)
                {
                    var a = Project(selected.Position + length * (u * MathF.Cos(j * MathF.Tau / 64) + v * MathF.Sin(j * MathF.Tau / 64)));
                    var b = Project(selected.Position + length * (u * MathF.Cos((j + 1) * MathF.Tau / 64) + v * MathF.Sin((j + 1) * MathF.Tau / 64)));
                    var distance = SegmentDistance(mouse, a, b);
                    if (distance < best) { best = distance; hoverAxis = i; }
                }
            }
            else
            {
                var end = Project(selected.Position + axis * length);
                if (Vector2.Distance(origin, end) < 12) continue;
                var distance = SegmentDistance(mouse, Vector2.Lerp(origin, end, 0.18f), end);
                if (distance < best) { best = distance; hoverAxis = i; }
            }
        }
        if (!hovered || ImGui.GetIO().KeyAlt) hoverAxis = -1;
        for (var i = 0; i < 3; i++)
        {
            var color = (IsDragging ? _axis == i : hoverAxis == i) ? 0xff55ffffu : Colors[i];
            if (Mode == Operation.Rotate)
            {
                var u = Axis((i + 1) % 3);
                var v = Axis((i + 2) % 3);
                for (var j = 0; j < 64; j++)
                {
                    var a = Project(selected.Position + length * (u * MathF.Cos(j * MathF.Tau / 64) + v * MathF.Sin(j * MathF.Tau / 64)));
                    var b = Project(selected.Position + length * (u * MathF.Cos((j + 1) * MathF.Tau / 64) + v * MathF.Sin((j + 1) * MathF.Tau / 64)));
                    draw.AddLine(a, b, color, 2);
                }
            }
            else
            {
                var end = Project(selected.Position + Axis(i) * length);
                if (Vector2.Distance(origin, end) < 12) continue;
                draw.AddLine(origin, end, color, 3);
                if (Mode == Operation.Scale) draw.AddRectFilled(end - new Vector2(5), end + new Vector2(5), color);
                else
                {
                    var direction = Vector2.Normalize(end - origin);
                    var side = new Vector2(-direction.Y, direction.X) * 5;
                    draw.AddTriangleFilled(end, end - direction * 12 + side, end - direction * 12 - side, color);
                }
            }
        }
        draw.AddCircleFilled(origin, 3, 0xffffffff);
        draw.PopClipRect();
        if (!consumed && hoverAxis >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var direction = Axis(hoverAxis % 3);
            // Fixed drag plane contains the selected axis and faces the camera as much as possible.
            var normal = Mode == Operation.Rotate || hoverAxis >= 3 ? direction :
                forward - direction * Vector3.Dot(forward, direction);
            if (normal.LengthSquared() > 0.0001f &&
                IntersectPlane(ray, selected.Position, Vector3.Normalize(normal), out var hit) &&
                (Mode != Operation.Rotate || (hit - selected.Position).LengthSquared() > 0.000001f))
            {
                _target = selected;
                _axis = hoverAxis;
                _direction = direction;
                _planeNormal = Vector3.Normalize(normal);
                _startHit = hit;
                _position = selected.Position;
                _scale = selected.Scale;
                _rotation = selected.Rotation;
                _length = length;
                _operation = Mode;
                _angle = _lastAngle = 0;
            }
            consumed = true;
        }
        return consumed;
    }

    private static Vector3 UnitAxis(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };

    private static bool InsideQuad(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static float Side(Vector2 p, Vector2 x, Vector2 y) =>
            (y.X - x.X) * (p.Y - x.Y) - (y.Y - x.Y) * (p.X - x.X);
        if (MathF.Abs(Side(c, a, b)) < 1) return false;
        var s1 = Side(point, a, b);
        var s2 = Side(point, b, c);
        var s3 = Side(point, c, d);
        var s4 = Side(point, d, a);
        return (s1 >= 0 && s2 >= 0 && s3 >= 0 && s4 >= 0) ||
               (s1 <= 0 && s2 <= 0 && s3 <= 0 && s4 <= 0);
    }
}
