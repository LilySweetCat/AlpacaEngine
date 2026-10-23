using System.Numerics;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

// Move / Rotate / Scale: экранные ручки поверх viewport, не 3D-меши.
// См. docs/gizmo_rotate_scale_visual.html: выбор → клик → перетаскивание → применение.
// Курсор задаёт луч в 3D; точку на луче определяет плоскость, зафиксированная при клике.
public static class Gizmo
{
    public enum Operation { Move, Rotate, Scale }

    public static Operation Mode;
    public static bool Local;

    public static bool IsDragging => _target != null;
    private static GameObject? _target;
    // 0–2: оси X/Y/Z или кольца; 3–5: плоскости Move с нормалью X/Y/Z.
    private static int _axis;
    private static Vector3 _direction, _planeNormal, _startHit, _position, _localPosition, _scale, _rotation;
    private static Matrix4x4 _parentWorldInv;
    private static float _length, _lastAngle, _angle;
    private static readonly uint[] Colors = [0xff5b48e5, 0xff38cd83, 0xfff28a45];

    // Rotate хранит угол как кватернион, а объект — как градусы Эйлера.
    // При pitch ≈ ±90° yaw и roll совпадают: фиксируем roll = 0.
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

    // Луч от камеры + плоскость жеста → точка мыши в 3D; параллельный луч или точка позади камеры не подходят.
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

    // Rotate: угол со знаком между двумя направлениями от центра кольца вокруг выбранной оси.
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));

    // Выбор ручки: расстояние курсора до отрезка на экране, в пикселях.
    public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var t = edge.LengthSquared() < 0.0001f ? 0 :
            Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
        return Vector2.Distance(point, a + edge * t);
    }

    // Escape возвращает исходный transform; отпускание записывает один шаг Undo.
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

    // true означает: клик занят gizmo, viewport не должен выбирать другой объект (включая кадр отпускания).
    public static bool Draw(GameObject? selected, Camera3D camera, Vector2 min, Vector2 max, bool hovered)
    {
        // Переключение режима W/E/R не прерывает жест и ввод текста.
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
        var texture = EditorState.ViewTexture.Texture;
        // Пиксель мыши в изображении viewport → пиксель render texture → луч из камеры.
        var ray = Raylib.GetScreenToWorldRayEx((mouse - min) / size * new Vector2(texture.Width, texture.Height),
            camera, texture.Width, texture.Height);
        var consumed = IsDragging;

        // Шаги 3–4: пока кнопка нажата, пересекаем луч с той же плоскостью и применяем результат.
        if (IsDragging)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) Cancel(true);
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) Cancel();
            else if (IntersectPlane(ray, _position, _planeNormal, out var hit))
            {
                // Move: стрелка оставляет лишь смещение вдоль оси; квадрат между осями берёт всё смещение в плоскости.
                if (Mode == Operation.Move)
                {
                    var worldPosition = _position + (_axis >= 3 ? hit - _startHit :
                        _direction * Vector3.Dot(hit - _startHit, _direction));

                    // Родительский transform требует перевести мировую позицию обратно в локальную.
                    selected.LocalPosition = selected.Parent == null
                        ? worldPosition : Vector3.Transform(worldPosition, _parentWorldInv);
                }
                // Scale: сдвиг на половину длины ручки даёт множитель 1.5; меняем только выбранную компоненту.
                else if (Mode == Operation.Scale)
                {
                    var factor = MathF.Max(0.01f, 1 + Vector3.Dot(hit - _startHit, _direction) / _length);
                    var scale = _scale;

                    scale[_axis] = MathF.Max(0.001f, _scale[_axis] * factor);
                    selected.LocalScale = scale;
                }
                // Rotate: угол между начальной и текущей точками относительно центра кольца.
                else if ((hit - _position).LengthSquared() > 0.000001f)
                {
                    var angle = SignedAngle(_startHit - _position, hit - _position, _direction);

                    // Накопление небольших приращений позволяет пройти границу ±180° и сделать полный оборот.
                    _angle += MathF.IEEERemainder(angle - _lastAngle, 2 * MathF.PI);
                    _lastAngle = angle;
                    selected.LocalRotation = EulerDegrees(Quaternion.CreateFromAxisAngle(_direction, _angle)
                        * GameObject.Orientation(_rotation));
                }
            }
        }

        // Шаг 1: экранные ручки привязаны к мировой позиции объекта; за камерой их не рисуем.
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var world = selected.WorldPosition;
        var depth = Vector3.Dot(world - camera.Position, forward);

        if (depth <= 0.01f && camera.Projection == CameraProjection.Perspective) return consumed;

        // Высота видимой области на глубине объекта; для ортографической камеры FovY уже задан в мировых единицах.
        var worldHeight = camera.Projection == CameraProjection.Perspective
            ? 2 * depth * MathF.Tan(camera.FovY * MathF.PI / 360) : camera.FovY;
        // Длина в мировых единицах, соответствующая примерно 85 пикселям на экране.
        var length = worldHeight * 85 / size.Y;
        var rotation = selected.WorldRotation;
        // Scale всегда вдоль локальных осей; Move/Rotate — вдоль мировых, если Local выключен.
        var local = Local || Mode == Operation.Scale;

        Vector3 Axis(int i) => local ? Vector3.Transform(UnitAxis(i), rotation) : UnitAxis(i);
        // Проекция мировой точки на изображение viewport (с учётом его размера в UI).
        Vector2 Project(Vector3 p) => min + Raylib.GetWorldToScreenEx(p, camera, texture.Width, texture.Height)
            / new Vector2(texture.Width, texture.Height) * size;
        // Кольцо Rotate: 64 экранных отрезка в плоскости поперёк оси; последняя точка замыкает кольцо.
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

        // Ищем ближайшую ручку в пределах 9 пикселей; индексы осей и плоскостей указаны выше.
        var nearestDistance = 9f;
        var hoveredHandle = -1;

        // Квадраты Move между двумя осями: попадание проверяем внутри экранного четырёхугольника.
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

                if (InsideQuad(mouse, a, b, c, d)) { hoveredHandle = i + 3; nearestDistance = 0; }

                var color = (IsDragging ? _axis == i + 3 : hovered && hoveredHandle == i + 3)
                    ? 0x9955ffffu : (Colors[i] & 0x00ffffff) | 0x55000000;
                draw.AddQuadFilled(a, b, c, d, color);
            }
        }
        // Кольца и оси проверяем по расстоянию в пикселях UI, независимо от разрешения render texture.
        for (var i = 0; i < 3; i++)
        {
            var axis = Axis(i);
            if (Mode == Operation.Rotate)
            {
                var ring = Ring(i);
                for (var j = 0; j < 64; j++)
                {
                    var distance = SegmentDistance(mouse, ring[j], ring[j + 1]);

                    if (distance < nearestDistance) { nearestDistance = distance; hoveredHandle = i; }
                }
            }
            else
            {
                var end = Project(world + axis * length);

                // Ось, направленная на камеру, слишком коротка на экране для захвата.
                if (Vector2.Distance(origin, end) < 12) continue;

                // Первые 18% стрелки оставляем для клика по самому объекту.
                var distance = SegmentDistance(mouse, Vector2.Lerp(origin, end, 0.18f), end);

                if (distance < nearestDistance) { nearestDistance = distance; hoveredHandle = i; }
            }
        }
        // Alt нужен для вращения камеры, поэтому ручки в этот момент не захватывают мышь.
        if (!hovered || ImGui.GetIO().KeyAlt) hoveredHandle = -1;

        // Рисуем поверх изображения; активную ручку подсвечиваем.
        for (var i = 0; i < 3; i++)
        {
            var color = (IsDragging ? _axis == i : hoveredHandle == i) ? 0xff55ffffu : Colors[i];

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

                // Квадратный конец у Scale, наконечник стрелки у Move.
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

        // Шаг 2: при клике запоминаем исходный transform, точку мыши в 3D и плоскость жеста.
        if (!consumed && hoveredHandle >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var direction = Axis(hoveredHandle % 3);
            // Кольцо Rotate и квадрат Move лежат в плоскости поперёк оси.
            // Для стрелок Move/Scale плоскость содержит ось и обращена к камере.
            var normal = Mode == Operation.Rotate || hoveredHandle >= 3 ? direction :
                forward - direction * Vector3.Dot(forward, direction);

            // Не начинаем жест, если луч не пересёк плоскость или для Rotate угол не определён.
            if (normal.LengthSquared() > 0.0001f &&
                IntersectPlane(ray, world, Vector3.Normalize(normal), out var hit) &&
                (Mode != Operation.Rotate || (hit - world).LengthSquared() > 0.000001f))
            {
                _target = selected;
                _axis = hoveredHandle;
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

    // Попадание курсора внутрь выпуклого квадрата Move, независимо от порядка обхода вершин.
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
