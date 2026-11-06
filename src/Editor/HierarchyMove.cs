using System.Numerics;
using AlpacaEngine.Extensions;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;

namespace AlpacaEngine.Editor;

internal static class HierarchyMove
{
    // index refers to the destination list before the source is removed.
    internal static bool TryCreate(Scene scene, GameObject target, GameObject? parent, int index,
        out ICommand? command, out string? error)
    {
        command = null;
        error = null;

        if (!EditorState.Contains(scene.GameObjects, target)
            || (parent != null && !EditorState.Contains(scene.GameObjects, parent)))
        {
            error = "The object is no longer in this scene.";
            return false;
        }

        for (var ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor != target) continue;

            error = "An object cannot be placed inside itself or its descendants.";
            return false;
        }

        var oldParent = target.Parent;
        var source = oldParent == null ? scene.GameObjects : oldParent.Child!;
        var destination = parent == null ? scene.GameObjects : parent.Child;
        var oldIndex = source.IndexOf(target);

        if (oldIndex < 0 || index < 0 || index > (destination?.Count ?? 0))
        {
            error = "Invalid insertion position.";
            return false;
        }

        var sameParent = parent == oldParent;
        var newIndex = index - (sameParent && oldIndex < index ? 1 : 0);

        if (sameParent && newIndex == oldIndex) return true;

        var oldPosition = target.LocalPosition;
        var oldRotation = target.LocalRotation;
        var oldScale = target.LocalScale;
        var position = oldPosition;
        var rotation = oldRotation;
        var scale = oldScale;

        if (!sameParent)
        {
            var parentWorld = parent?.WorldMatrix ?? Matrix4x4.Identity;

            if (!Matrix4x4.Invert(parentWorld, out var inverse))
            {
                error = "The parent has zero scale. Its transform cannot be inverted.";
                return false;
            }

            var world = target.WorldMatrix;
            var local = world * inverse;

            if (!Matrix4x4.Decompose(local, out scale, out var orientation, out position))
            {
                error = "This move would require a skew that Position / Rotation / Scale cannot preserve.";
                return false;
            }

            rotation = orientation.ToEulerAngles();
            var rebuilt = Matrix4x4.CreateScale(scale)
                          * Matrix4x4.CreateFromQuaternion(GameObject.Orientation(rotation))
                          * Matrix4x4.CreateTranslation(position);

            if (!MatricesMatch(local, rebuilt) || !MatricesMatch(world, rebuilt * parentWorld))
            {
                error = "This move would require a skew that Position / Rotation / Scale cannot preserve.";
                return false;
            }
        }

        var remaining = new List<GameObject>(source);
        remaining.RemoveAt(oldIndex);
        var inserted = sameParent ? remaining : new List<GameObject>(destination ?? []);
        inserted.Insert(newIndex, target);

        // Replace lists rather than mutating snapshots retained by earlier Undo commands.
        command = new ChangeValueCommand<bool>(moved =>
        {
            SetObjects(oldParent, moved ? remaining : source);

            if (!sameParent) SetObjects(parent, moved ? inserted : destination);

            target.Parent = moved ? parent : oldParent;
            target.LocalPosition = moved ? position : oldPosition;
            target.LocalRotation = moved ? rotation : oldRotation;
            target.LocalScale = moved ? scale : oldScale;
        }, false, true);
        return true;

        void SetObjects(GameObject? owner, List<GameObject>? objects)
        {
            if (owner == null) scene.GameObjects = objects!;

            else owner.Child = objects;
        }
    }

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
