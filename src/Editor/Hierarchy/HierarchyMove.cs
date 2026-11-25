using System.Numerics;
using AlpacaEngine.Commands;
using AlpacaEngine.Extensions;
using AlpacaEngine.GameObjects;

namespace AlpacaEngine.Editor.Hierarchy;

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
        var source = oldParent == null ? scene.GameObjects : oldParent.Children!;
        var destination = parent == null ? scene.GameObjects : parent.Children;
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

            if (!TransformMath.MatricesMatch(local, rebuilt) || !TransformMath.MatricesMatch(world, rebuilt * parentWorld))
            {
                error = "This move would require a skew that Position / Rotation / Scale cannot preserve.";
                return false;
            }
        }

        command = new MoveObjectCommand(scene, target, parent, newIndex, (position, rotation, scale));
        return true;
    }

}
