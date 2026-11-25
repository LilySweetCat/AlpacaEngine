using System.Numerics;
using AlpacaEngine.Commands;
using AlpacaEngine.Extensions;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;

namespace AlpacaEngine.Editor.Gizmo;

// All previews are calculated from the click-time snapshot, never from the previous frame.
internal sealed class GizmoGesture
{
    private readonly record struct Transform(Vector3 Position, Vector3 Rotation, Vector3 Scale)
    {
        internal static Transform Capture(GameObject target) =>
            new(target.LocalPosition, target.LocalRotation, target.LocalScale);

        internal Matrix4x4 Matrix => TransformMath.Compose(Position, Rotation, Scale);

        internal void Apply(GameObject target)
        {
            target.LocalPosition = Position;
            target.LocalRotation = Rotation;
            target.LocalScale = Scale;
        }
    }

    private sealed record Target(GameObject Object, Transform Before, Matrix4x4 World, Quaternion Rotation);

    private readonly Target[] _targets;
    private readonly Dictionary<GameObject, Matrix4x4> _ancestorLocals = [];
    private readonly Editor.Gizmo.Gizmo.PivotPoint _pivotMode;
    private readonly bool _localAxes;

    internal Vector3 Pivot { get; }
    internal Quaternion Axes { get; }
    internal string? Error { get; private set; }

    internal static Vector3 GetPivot(IReadOnlyList<GameObject> selection, GameObject active, Editor.Gizmo.Gizmo.PivotPoint mode)
    {
        if (mode != Editor.Gizmo.Gizmo.PivotPoint.SelectionCenter) return active.WorldPosition;

        var sum = Vector3.Zero;

        foreach (var target in selection) sum += target.WorldPosition;

        return sum / selection.Count;
    }

    internal GizmoGesture(IReadOnlyList<GameObject> selection, GameObject active, Editor.Gizmo.Gizmo.PivotPoint pivotMode, bool localAxes)
    {
        _pivotMode = pivotMode;
        _localAxes = localAxes;
        Pivot = GetPivot(selection, active, pivotMode);
        Axes = localAxes ? active.WorldRotation : Quaternion.Identity;
        _targets = selection.Select(target => new Target(target, Transform.Capture(target),
            target.WorldMatrix, target.WorldRotation)).ToArray();

        foreach (var target in selection)
        {
            for (var parent = target.Parent; parent != null; parent = parent.Parent)
                _ancestorLocals.TryAdd(parent, Transform.Capture(parent).Matrix);
        }
    }

    internal bool Move(Vector3 offset) => TryApply(target => target.World * Matrix4x4.CreateTranslation(offset));

    internal bool Rotate(int axis, float angle) => TryApply(target =>
        AroundPivot(target, Matrix4x4.CreateFromAxisAngle(Direction(target, axis), angle)));

    internal bool Scale(int axis, float factor) => TryApply(target =>
    {
        var direction = Direction(target, axis);

        // Stretch along a unit world axis: I + (factor - 1) * axis * transpose(axis).
        var amount = factor - 1;
        var stretch = Matrix4x4.Identity;
        stretch.M11 += amount * direction.X * direction.X;
        stretch.M12 += amount * direction.X * direction.Y;
        stretch.M13 += amount * direction.X * direction.Z;
        stretch.M21 += amount * direction.Y * direction.X;
        stretch.M22 += amount * direction.Y * direction.Y;
        stretch.M23 += amount * direction.Y * direction.Z;
        stretch.M31 += amount * direction.Z * direction.X;
        stretch.M32 += amount * direction.Z * direction.Y;
        stretch.M33 += amount * direction.Z * direction.Z;
        return AroundPivot(target, stretch);
    });

    private Vector3 Direction(Target target, int axis)
    {
        var unit = axis switch { 0 => Vector3.UnitX, 1 => Vector3.UnitY, _ => Vector3.UnitZ };
        var rotation = _pivotMode == Editor.Gizmo.Gizmo.PivotPoint.IndividualOrigins && _localAxes ? target.Rotation : Axes;
        return Vector3.Normalize(Vector3.Transform(unit, rotation));
    }

    private Matrix4x4 AroundPivot(Target target, Matrix4x4 change)
    {
        var pivot = _pivotMode == Editor.Gizmo.Gizmo.PivotPoint.IndividualOrigins ? target.World.Translation : Pivot;
        return target.World * Matrix4x4.CreateTranslation(-pivot) * change * Matrix4x4.CreateTranslation(pivot);
    }

    private bool TryApply(Func<Target, Matrix4x4> calculateWorld)
    {
        var worlds = _targets.ToDictionary(target => target.Object, calculateWorld);
        var transforms = new Transform[_targets.Length];
        Error = null;

        for (var i = 0; i < _targets.Length; i++)
        {
            var target = _targets[i];
            var parentWorld = GetParentWorld(target.Object.Parent);

            if (!Matrix4x4.Invert(parentWorld, out var inverse))
            {
                Error = "The parent has zero scale. Its transform cannot be inverted.";
                return false;
            }

            var world = worlds[target.Object];
            var local = world * inverse;

            // Preserve exact Euler values and scales when only the position changed.
            var positionOnly = target.Before with { Position = local.Translation };

            if (TransformMath.MatricesMatch(target.Before.Matrix, local)
                && TransformMath.MatricesMatch(target.Before.Matrix * parentWorld, world))
                transforms[i] = target.Before;

            else if (TransformMath.MatricesMatch(positionOnly.Matrix, local)
                && TransformMath.MatricesMatch(positionOnly.Matrix * parentWorld, world))
                transforms[i] = positionOnly;

            else
            {
                if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var position))
                    return RejectSkew();

                transforms[i] = new Transform(position, rotation.ToEulerAngles(), scale);

                if (!TransformMath.MatricesMatch(transforms[i].Matrix, local)
                    || !TransformMath.MatricesMatch(transforms[i].Matrix * parentWorld, world)) return RejectSkew();
            }
        }

        // Validate every object before changing any of them, including selected descendants.
        Apply(transforms);
        return true;

        Matrix4x4 GetParentWorld(GameObject? parent)
        {
            if (parent == null) return Matrix4x4.Identity;

            if (worlds.TryGetValue(parent, out var desired)) return desired;

            return _ancestorLocals[parent] * GetParentWorld(parent.Parent);
        }
    }

    private bool RejectSkew()
    {
        Error = "This transform would require a skew that Position / Rotation / Scale cannot preserve.";
        return false;
    }

    private void Apply(Transform[] transforms)
    {
        for (var i = 0; i < _targets.Length; i++) transforms[i].Apply(_targets[i].Object);
    }

    internal void Finish(bool restore)
    {
        var before = _targets.Select(target => target.Before).ToArray();

        if (restore)
        {
            Apply(before);
            return;
        }

        var after = _targets.Select(target => Transform.Capture(target.Object)).ToArray();

        if (!before.SequenceEqual(after)) CommandManager.Record(new ChangeValueCommand<Transform[]>(Apply, before, after));
    }
}
