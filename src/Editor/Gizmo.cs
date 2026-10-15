using System.Numerics;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

// Transform gizmo (Move / Rotate / Scale handles) drawn over the 3D viewport.
//
// How it works, once per frame (Draw is called from Inspector.Draw3dViewport):
//  1. Mouse position inside the viewport image is converted to a world-space ray.
//  2. Handles are placed on the selected object's world position and drawn in screen space.
//     Their length is fixed at ~85 px on screen, so they keep the same size when zooming.
//  3. Hover: the distance from the mouse to every handle is measured in pixels
//     (quads for move planes, segments for axes, polylines for rotation rings).
//     The closest handle within 9 px is hovered.
//  4. Click on a hovered handle: the start state is saved (position, scale, rotation)
//     together with a drag plane. Axis handles use a plane that contains the axis and
//     faces the camera; rotation rings use the plane perpendicular to the axis.
//  5. Drag: every frame the mouse ray is intersected with that fixed plane and the change
//     is measured from the start hit point. Release keeps the result, Escape restores the
//     saved start state.
//  6. While dragging, Draw returns true so the viewport does not pick another object.
public static class Gizmo
{
    public enum Operation { Move, Rotate, Scale }
    
    public static Operation Mode;
    public static bool Local;
    
    public static bool IsDragging => _target != null;
    private static GameObject? _target;
    private static int _axis;
    private static Vector3 _direction, _planeNormal, _startHit, _position, _localPosition, _scale, _rotation;
    private static Matrix4x4 _parentWorldInv;
    private static float _length, _lastAngle, _angle;
    private static readonly uint[] Colors = [0xff5b48e5, 0xff38cd83, 0xfff28a45];

    // Quaternion -> Euler angles (pitch X, yaw Y, roll Z) in degrees. Inverse of GameObject.Orientation.
    // At gimbal lock (pitch near +-90 degrees) yaw and roll become the same rotation, so roll is set to 0.
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

    // Ray-plane intersection. False if the ray is parallel to the plane or the hit is behind the ray origin.
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

    // Signed angle from 'from' to 'to' around 'axis' (right-hand rule). Used for rotation ring drag.
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));

    // Pixel distance from a point to a line segment. Used for hover tests.
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var t = edge.LengthSquared() < 0.0001f ? 0 :
            Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + edge * t);
    }

    // Ends the current drag. restore = true puts back the transform saved at drag start (Escape).
    public static void Cancel(bool restore = false)
    {
        var target = _target;
        if (target != null)
        {
            if (restore)
            {
                target.LocalPosition = _localPosition;
                target.LocalScale = _scale;
                target.LocalRotation = _rotation;
            }
            else
            {
                CommandManager.RecordChange(
                    value => (target.LocalPosition, target.LocalScale, target.LocalRotation) = value,
                    (_localPosition, _scale, _rotation),
                    (target.LocalPosition, target.LocalScale, target.LocalRotation));
            }
        }
        _target = null;
    }

    // Returns true while the gizmo owns this mouse interaction (including release).
    public static bool Draw(GameObject? selected, Camera3D camera, Vector2 min, Vector2 max, bool hovered)
    {
        // Hotkeys: W = Move, E = Rotate, R = Scale (Unity-style). Ignored while dragging or typing in a text field.
        if (!IsDragging && !ImGui.GetIO().WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = Operation.Move;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = Operation.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = Operation.Scale;
        }
        
        var size = max - min;
        if (selected != _target && IsDragging) Cancel();
        if (selected == null || size.X < 1 || size.Y < 1) { Cancel(); return false; }
        var mouse = ImGui.GetMousePos();
        var texture = AlpacaEngine.Editor.Editor.ViewTexture.Texture;
        // Mouse in viewport-image pixels mapped to render-texture pixels, then to a world ray.
        var ray = Raylib.GetScreenToWorldRayEx((mouse - min) / size * new Vector2(texture.Width, texture.Height),
            camera, texture.Width, texture.Height);
        var consumed = IsDragging;
        
        // Drag update: runs every frame while the mouse button is held.
        if (IsDragging)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) Cancel(true);
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) Cancel();
            else if (IntersectPlane(ray, _position, _planeNormal, out var hit))
            {
                // Move: project the hit delta onto the handle axis (or use the full delta for a plane handle).
                // Parent-space objects need the world result converted back to local space.
                if (Mode == Operation.Move)
                {
                    var worldPosition = _position + (_axis >= 3 ? hit - _startHit :
                        _direction * Vector3.Dot(hit - _startHit, _direction));
                    selected.LocalPosition = selected.Parent == null
                        ? worldPosition : Vector3.Transform(worldPosition, _parentWorldInv);
                }
                // Scale: 1 + (distance moved along the axis / handle length), applied only to the dragged component.
                else if (Mode == Operation.Scale)
                {
                    var factor = MathF.Max(0.01f, 1 + Vector3.Dot(hit - _startHit, _direction) / _length);
                    var scale = _scale;
                    scale[_axis] = MathF.Max(0.001f, _scale[_axis] * factor);
                    selected.LocalScale = scale;
                }
                // Rotate: angle of the hit point around the ring center. Deltas are accumulated
                // (IEEERemainder wraps each step to -180..180) so the object can turn past 180 degrees.
                else if ((hit - _position).LengthSquared() > 0.000001f)
                {
                    var angle = SignedAngle(_startHit - _position, hit - _position, _direction);
                    _angle += MathF.IEEERemainder(angle - _lastAngle, 2 * MathF.PI);
                    _lastAngle = angle;
                    selected.LocalRotation = EulerDegrees(Quaternion.CreateFromAxisAngle(_direction, _angle)
                        * GameObject.Orientation(_rotation));
                }
            }
        }

        // Handle geometry. Points behind the camera are not drawn (perspective only).
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var world = selected.WorldPosition;
        var depth = Vector3.Dot(world - camera.Position, forward);
        if (depth <= 0.01f && camera.Projection == CameraProjection.Perspective) return consumed;
        // World height of the screen at the object's depth. Orthographic: FovY is already the world height.
        var worldHeight = camera.Projection == CameraProjection.Perspective
            ? 2 * depth * MathF.Tan(camera.FovY * MathF.PI / 360) : camera.FovY;
        // Handle length in world units that is ~85 screen pixels tall.
        var length = worldHeight * 85 / size.Y;
        var rotation = selected.WorldRotation;
        // Scale handles are always local. Move/Rotate use world axes unless Local is checked.
        var local = Local || Mode == Operation.Scale;
        Vector3 Axis(int i) => local ? Vector3.Transform(UnitAxis(i), rotation) : UnitAxis(i);
        // World point -> pixel position inside the viewport image.
        Vector2 Project(Vector3 p) => min + Raylib.GetWorldToScreenEx(p, camera, texture.Width, texture.Height)
            / new Vector2(texture.Width, texture.Height) * size;
        // Rotation ring for axis i: 64 segments, drawn in the plane perpendicular to that axis.
        // Index 64 repeats index 0 so the loop closes.
        Vector2[] Ring(int axisIndex)
        {
            var u = Axis((axisIndex + 1) % 3);
            var v = Axis((axisIndex + 2) % 3);
            var points = new Vector2[65];
            for (var j = 0; j <= 64; j++)
                points[j] = Project(world + length * (u * MathF.Cos(j * MathF.Tau / 64) + v * MathF.Sin(j * MathF.Tau / 64)));
            return points;
        }
        var origin = Project(world);
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(min, max, true);
        // best = closest hover distance so far (9 px threshold). hoverAxis: 0-2 axes/rings, 3-5 move planes.
        var best = 9f;
        var hoverAxis = -1;
        // Move plane handles: small squares between two axes. They are checked first and win if the mouse is inside.
        if (Mode == Operation.Move)
        {
            for (var i = 0; i < 3; i++)
            {
                var u = Axis((i + 1) % 3);
                var v = Axis((i + 2) % 3);
                var a = Project(world + length * (u + v) * 0.25f);
                var b = Project(world + length * (u * 0.45f + v * 0.25f));
                var c = Project(world + length * (u + v) * 0.45f);
                var d = Project(world + length * (u * 0.25f + v * 0.45f));
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
                var ring = Ring(i);
                for (var j = 0; j < 64; j++)
                {
                    var distance = SegmentDistance(mouse, ring[j], ring[j + 1]);
                    if (distance < best) { best = distance; hoverAxis = i; }
                }
            }
            else
            {
                var end = Project(world + axis * length);
                // Axis pointing almost straight at the camera is too short on screen to grab.
                if (Vector2.Distance(origin, end) < 12) continue;
                // The inner 18% of the axis (near the origin) is not hit-testable, so clicks there still reach the object.
                var distance = SegmentDistance(mouse, Vector2.Lerp(origin, end, 0.18f), end);
                if (distance < best) { best = distance; hoverAxis = i; }
            }
        }
        // No hover when the viewport is not hovered or Alt is held (Alt is used for camera orbit).
        if (!hovered || ImGui.GetIO().KeyAlt) hoverAxis = -1;
        // Draw the handles. The dragged or hovered handle is highlighted in a brighter color.
        for (var i = 0; i < 3; i++)
        {
            var color = (IsDragging ? _axis == i : hoverAxis == i) ? 0xff55ffffu : Colors[i];
            if (Mode == Operation.Rotate)
            {
                var ring = Ring(i);
                for (var j = 0; j < 64; j++) draw.AddLine(ring[j], ring[j + 1], color, 2);
            }
            else
            {
                var end = Project(world + Axis(i) * length);
                if (Vector2.Distance(origin, end) < 12) continue;
                draw.AddLine(origin, end, color, 3);
                // Scale handle: square cap. Move handle: arrow head.
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
        // Drag start: on a fresh click over a handle, save the start state and build the drag plane.
        if (!consumed && hoverAxis >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var direction = Axis(hoverAxis % 3);
            // Drag plane: rings and move planes use the axis normal as plane normal. Axis handles use
            // the camera-facing plane that contains the axis (camera forward with the axis component removed).
            var normal = Mode == Operation.Rotate || hoverAxis >= 3 ? direction :
                forward - direction * Vector3.Dot(forward, direction);
            // Reject the click if the ray misses the plane or a ring click lands exactly on the center (no angle).
            if (normal.LengthSquared() > 0.0001f &&
                IntersectPlane(ray, world, Vector3.Normalize(normal), out var hit) &&
                (Mode != Operation.Rotate || (hit - world).LengthSquared() > 0.000001f))
            {
                _target = selected;
                _axis = hoverAxis;
                _direction = direction;
                _planeNormal = Vector3.Normalize(normal);
                _startHit = hit;
                _position = world;
                _localPosition = selected.LocalPosition;
                _parentWorldInv = selected.Parent == null ? Matrix4x4.Identity
                    : selected.Parent.WorldMatrixInverted;
                _scale = selected.LocalScale;
                _rotation = selected.LocalRotation;
                _length = length;
                _angle = _lastAngle = 0;
            }
            consumed = true;
        }
        return consumed;
    }

    private static Vector3 UnitAxis(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };

    // Point-in-quad test for a convex quad given in order a-b-c-d. Accepts either winding direction.
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
