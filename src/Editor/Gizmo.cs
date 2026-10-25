using System.Numerics;
using AlpacaEngine.Extensions;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

// Move / Rotate / Scale: screen-space handles drawn over the viewport, not 3D meshes.
// See docs/gizmo_rotate_scale_visual.html: pick handle -> click -> drag -> apply.
// The mouse defines a ray in 3D; the point on that ray comes from a plane fixed at click time.
public static class Gizmo
{
    public enum Operation { Move, Rotate, Scale }

    public static Operation Mode;
    public static bool Local;

    public static bool IsDragging => _target != null;

    private static GameObject? _target;

    // 0-2: the X/Y/Z axes (arrows, boxes, rings); 3-5: Move planes with X/Y/Z normals.
    private static int _axis;
    private static Vector3 _direction, _planeNormal, _startHit, _position, _localPosition, _scale, _rotation;
    private static Matrix4x4 _parentWorldInv;
    private static float _length, _lastAngle, _angle;
    private static readonly uint[] Colors = [0xff5b48e5, 0xff38cd83, 0xfff28a45];

    // Everything the handle drawing and hit testing needs for one frame.
    private sealed record HandleFrame(
        Camera3D Camera, Vector2 Min, Vector2 Size, GameObject Selected,
        Vector3 World, Quaternion Rotation, bool LocalAxes, float Length, Vector2 Origin);

    // A ray from the camera plus the plane of the gesture -> the mouse point in 3D;
    // a parallel ray or a point behind the camera do not make a valid drag.
    public static bool IntersectPlane(Ray ray, Vector3 point, Vector3 normal, out Vector3 hit)
    {
        hit = default;
        var rayFacing = Vector3.Dot(ray.Direction, normal);

        if (MathF.Abs(rayFacing) < 0.0001f) return false;

        var distance = Vector3.Dot(point - ray.Position, normal) / rayFacing;

        if (distance < 0 || !float.IsFinite(distance)) return false;

        hit = ray.Position + ray.Direction * distance;
        return true;
    }

    // Rotate: the signed angle between two directions from the ring center around the chosen axis.
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));

    // Handle picking: the distance from the cursor to a screen-space segment, in pixels.
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var t = edge.LengthSquared() < 0.0001f ? 0 :
            Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + edge * t);
    }

    // Escape restores the original transform; releasing the mouse records one Undo step.
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

    // true means: the click belongs to the gizmo, the viewport must not select another object
    // (including on the release frame).
    public static bool Draw(GameObject? selected, Camera3D camera, Vector2 min, Vector2 max, bool hovered)
    {
        HandleHotkeys();

        var size = max - min;

        if (selected != _target && IsDragging) Cancel();

        if (selected == null || size.X < 1 || size.Y < 1)
        {
            Cancel();
            return false;
        }

        var mouse = ImGui.GetMousePos();
        var ray = Picking.GetMouseRay(mouse, min, max, camera);
        var consumed = IsDragging;

        // While the button is held, intersect the ray with the same plane and apply the result.
        if (IsDragging)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) Cancel(true);

            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) Cancel();

            else if (IntersectPlane(ray, _position, _planeNormal, out var hit))
            {
                switch (Mode)
                {
                    case Operation.Move: HandleMove(selected, hit); break;
                    case Operation.Scale: HandleScale(selected, hit); break;
                    case Operation.Rotate: HandleRotate(selected, hit); break;
                }
            }
        }

        var frame = CreateFrame(selected, camera, min, size);

        // Handles are anchored at the object's world position; skip them behind the camera.
        if (frame == null) return consumed;

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(min, max, true);

        var hoveredHandle = FindHoveredHandle(frame, mouse, hovered);
        DrawHandles(frame, hoveredHandle);

        drawList.AddCircleFilled(frame.Origin, 3, 0xffffffff);
        drawList.PopClipRect();

        // On click, remember the original transform, the 3D mouse point and the gesture plane.
        if (!consumed && hoveredHandle >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            BeginGesture(frame, ray, hoveredHandle);

            // The click stays with the gizmo even when the gesture did not start.
            consumed = true;
        }

        return consumed;
    }

    private static void HandleHotkeys()
    {
        // Mode switching with W/E/R must not break a running gesture or text input.
        if (IsDragging || ImGui.GetIO().WantTextInput) return;

        if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = Operation.Move;

        if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = Operation.Rotate;

        if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = Operation.Scale;
    }

    // Collect everything the handles need for this frame; null when the object is behind the camera.
    private static HandleFrame? CreateFrame(GameObject selected, Camera3D camera, Vector2 min, Vector2 size)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var world = selected.WorldPosition;
        var depth = Vector3.Dot(world - camera.Position, forward);

        if (depth <= 0.01f && camera.Projection == CameraProjection.Perspective) return null;

        // The visible world height at the object's depth; for the orthographic camera FovY is in world units.
        var worldHeight = camera.Projection == CameraProjection.Perspective
            ? 2 * depth * MathF.Tan(camera.FovY * MathF.PI / 360) : camera.FovY;

        // The handle length in world units that covers roughly 85 screen pixels.
        var length = worldHeight * 85 / size.Y;

        var texture = EditorState.ViewTexture.Texture;
        var origin = min + Raylib.GetWorldToScreenEx(world, camera, texture.Width, texture.Height)
            / new Vector2(texture.Width, texture.Height) * size;

        // Scale always follows local axes; Move/Rotate follow world axes unless Local is on.
        var localAxes = Local || Mode == Operation.Scale;

        return new HandleFrame(camera, min, size, selected, world, selected.WorldRotation, localAxes, length, origin);
    }

    // Find the handle closest to the cursor within 9 pixels; axis and plane indices are listed above.
    private static int FindHoveredHandle(HandleFrame frame, Vector2 mouse, bool hovered)
    {
        var nearestDistance = 9f;
        var hoveredHandle = -1;

        // Move planes are tested before the axes and win when the cursor is inside them.
        if (Mode == Operation.Move) FindMovePlane(frame, mouse, ref hoveredHandle, ref nearestDistance);

        // Rings and axes are hit tested by their distance in UI pixels,
        // independent of the render texture resolution.
        for (var i = 0; i < 3; i++)
        {
            if (Mode == Operation.Rotate)
            {
                var ring = Ring(frame, i);

                for (var j = 0; j < 64; j++)
                {
                    var distance = SegmentDistance(mouse, ring[j], ring[j + 1]);

                    if (distance >= nearestDistance) continue;

                    nearestDistance = distance;
                    hoveredHandle = i;
                }
            }
            else
            {
                var end = Project(frame, frame.World + Axis(frame, i) * frame.Length);

                // An axis pointing at the camera is too short on screen to grab.
                if (Vector2.Distance(frame.Origin, end) < 12) continue;

                // The first 18% of the arrow stays free for clicking the object itself.
                var distance = SegmentDistance(mouse, Vector2.Lerp(frame.Origin, end, 0.18f), end);

                if (distance >= nearestDistance) continue;

                nearestDistance = distance;
                hoveredHandle = i;
            }
        }

        // Alt is needed for camera rotation, so the handles do not capture the mouse then.
        if (!hovered || ImGui.GetIO().KeyAlt) hoveredHandle = -1;

        return hoveredHandle;
    }

    // Hit test the Move planes; a quad wins over the axes when the cursor is inside it.
    private static void FindMovePlane(HandleFrame frame, Vector2 mouse, ref int hoveredHandle, ref float nearestDistance)
    {
        for (var i = 0; i < 3; i++)
        {
            var (a, b, c, d) = MovePlaneCorners(frame, i);

            if (!InsideQuad(mouse, a, b, c, d)) continue;

            hoveredHandle = i + 3;
            nearestDistance = 0;
        }
    }

    // On click, remember the original transform, the 3D mouse point and the plane of the gesture.
    private static void BeginGesture(HandleFrame frame, Ray ray, int hoveredHandle)
    {
        var direction = Axis(frame, hoveredHandle % 3);

        // Rotate rings and Move planes lie in the plane across the axis.
        // Move/Scale arrows use a plane that contains the axis and faces the camera.
        var forward = Vector3.Normalize(frame.Camera.Target - frame.Camera.Position);
        var normal = Mode == Operation.Rotate || hoveredHandle >= 3 ? direction :
            forward - direction * Vector3.Dot(forward, direction);

        // No gesture starts when the ray missed the plane or the Rotate angle is undefined.
        if (normal.LengthSquared() <= 0.0001f) return;

        if (!IntersectPlane(ray, frame.World, Vector3.Normalize(normal), out var hit)) return;

        if (Mode == Operation.Rotate && (hit - frame.World).LengthSquared() <= 0.000001f) return;

        _target = frame.Selected;
        _axis = hoveredHandle;
        _direction = direction;
        _planeNormal = Vector3.Normalize(normal);
        _startHit = hit;
        _position = frame.World;
        _localPosition = frame.Selected.LocalPosition;
        _parentWorldInv = frame.Selected.Parent?.WorldMatrixInverted ?? Matrix4x4.Identity;
        _scale = frame.Selected.LocalScale;
        _rotation = frame.Selected.LocalRotation;
        _length = frame.Length;
        _angle = _lastAngle = 0;
    }

    // Move: the arrow keeps only the offset along its axis; the quad takes the whole plane offset.
    private static void HandleMove(GameObject selected, Vector3 hit)
    {
        var worldPosition = _position + (_axis >= 3 ? hit - _startHit :
            _direction * Vector3.Dot(hit - _startHit, _direction));

        // A parent transform means the world position must be converted back to local.
        selected.LocalPosition = selected.Parent == null
            ? worldPosition : Vector3.Transform(worldPosition, _parentWorldInv);
    }

    // Scale: a drag of half the handle length gives a 1.5x factor; only the chosen component changes.
    private static void HandleScale(GameObject selected, Vector3 hit)
    {
        var factor = MathF.Max(0.01f, 1 + Vector3.Dot(hit - _startHit, _direction) / _length);
        var scale = _scale;

        scale[_axis] = MathF.Max(0.001f, _scale[_axis] * factor);
        selected.LocalScale = scale;
    }

    // Rotate: the angle between the start point and the current point around the ring center.
    private static void HandleRotate(GameObject selected, Vector3 hit)
    {
        if ((hit - _position).LengthSquared() <= 0.000001f) return;

        var angle = SignedAngle(_startHit - _position, hit - _position, _direction);

        // Accumulating small steps allows passing the +/-180 degree border and making a full turn.
        _angle += MathF.IEEERemainder(angle - _lastAngle, 2 * MathF.PI);
        _lastAngle = angle;
        selected.LocalRotation = (Quaternion.CreateFromAxisAngle(_direction, _angle)
            * GameObject.Orientation(_rotation)).ToEulerAngles();
    }

    // Draw the active handles over the viewport image; the hovered or dragged one is highlighted.
    private static void DrawHandles(HandleFrame frame, int hoveredHandle)
    {
        var drawList = ImGui.GetWindowDrawList();

        // The Move planes are drawn first so the axes stay visible on top of them.
        if (Mode == Operation.Move) DrawMovePlanes(frame, hoveredHandle);

        for (var i = 0; i < 3; i++)
        {
            var color = (IsDragging ? _axis == i : hoveredHandle == i) ? 0xff55ffffu : Colors[i];

            switch (Mode)
            {
                case Operation.Move: DrawMove(frame, i, color); break;
                case Operation.Rotate: DrawRotate(frame, i, color); break;
                case Operation.Scale: DrawScale(frame, i, color); break;
            }
        }
    }

    // The small quads between the Move axes; they drag the whole plane, not just one axis.
    private static void DrawMovePlanes(HandleFrame frame, int hoveredHandle)
    {
        var drawList = ImGui.GetWindowDrawList();

        for (var i = 0; i < 3; i++)
        {
            var (a, b, c, d) = MovePlaneCorners(frame, i);
            var color = (IsDragging ? _axis == i + 3 : hoveredHandle == i + 3)
                ? 0x9955ffffu : (Colors[i] & 0x00ffffff) | 0x55000000;
            drawList.AddQuadFilled(a, b, c, d, color);
        }
    }

    // Screen corners of the i-th Move plane quad, shared by its hit test and its drawing.
    private static (Vector2 A, Vector2 B, Vector2 C, Vector2 D) MovePlaneCorners(HandleFrame frame, int i)
    {
        var u = Axis(frame, (i + 1) % 3);
        var v = Axis(frame, (i + 2) % 3);

        return (
            Project(frame, frame.World + frame.Length * (u + v) * 0.25f),
            Project(frame, frame.World + frame.Length * (u * 0.45f + v * 0.25f)),
            Project(frame, frame.World + frame.Length * (u + v) * 0.45f),
            Project(frame, frame.World + frame.Length * (u * 0.25f + v * 0.45f)));
    }

    private static void DrawMove(HandleFrame frame, int i, uint color)
    {
        var end = Project(frame, frame.World + Axis(frame, i) * frame.Length);

        if (Vector2.Distance(frame.Origin, end) < 12) return;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(frame.Origin, end, color, 3);

        var direction = Vector2.Normalize(end - frame.Origin);
        var side = new Vector2(-direction.Y, direction.X) * 5;
        drawList.AddTriangleFilled(end, end - direction * 12 + side, end - direction * 12 - side, color);
    }

    private static void DrawRotate(HandleFrame frame, int i, uint color)
    {
        var ring = Ring(frame, i);
        var drawList = ImGui.GetWindowDrawList();

        for (var j = 0; j < 64; j++) drawList.AddLine(ring[j], ring[j + 1], color, 2);
    }

    private static void DrawScale(HandleFrame frame, int i, uint color)
    {
        var end = Project(frame, frame.World + Axis(frame, i) * frame.Length);

        if (Vector2.Distance(frame.Origin, end) < 12) return;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(frame.Origin, end, color, 3);
        drawList.AddRectFilled(end - new Vector2(5), end + new Vector2(5), color);
    }

    // Project a world point onto the viewport image (accounting for its UI size).
    private static Vector2 Project(HandleFrame frame, Vector3 point)
    {
        var texture = EditorState.ViewTexture.Texture;
        return frame.Min + Raylib.GetWorldToScreenEx(point, frame.Camera, texture.Width, texture.Height)
            / new Vector2(texture.Width, texture.Height) * frame.Size;
    }

    // The Rotate ring: 64 screen segments across the plane of the axis; the last point closes it.
    private static Vector2[] Ring(HandleFrame frame, int axisIndex)
    {
        var u = Axis(frame, (axisIndex + 1) % 3);
        var v = Axis(frame, (axisIndex + 2) % 3);

        var points = new Vector2[65];

        for (var j = 0; j <= 64; j++)
            points[j] = Project(frame, frame.World + frame.Length
                * (u * MathF.Cos(j * MathF.Tau / 64) + v * MathF.Sin(j * MathF.Tau / 64)));

        return points;
    }

    // The i-th axis direction, in object space or world space depending on the mode.
    private static Vector3 Axis(HandleFrame frame, int i) =>
        frame.LocalAxes ? Vector3.Transform(UnitAxis(i), frame.Rotation) : UnitAxis(i);

    private static Vector3 UnitAxis(int axis) => axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };

    // Point-in-quad test that accepts either vertex winding order.
    private static bool InsideQuad(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (MathF.Abs(Side(c, a, b)) < 1) return false;

        var s1 = Side(point, a, b);
        var s2 = Side(point, b, c);
        var s3 = Side(point, c, d);
        var s4 = Side(point, d, a);

        return (s1 >= 0 && s2 >= 0 && s3 >= 0 && s4 >= 0) ||
               (s1 <= 0 && s2 <= 0 && s3 <= 0 && s4 <= 0);

        static float Side(Vector2 p, Vector2 x, Vector2 y) =>
            (y.X - x.X) * (p.Y - x.Y) - (y.Y - x.Y) * (p.X - x.X);
    }
}
