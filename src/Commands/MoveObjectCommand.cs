using System.Numerics;
using AlpacaEngine.GameObjects;

namespace AlpacaEngine.Commands;

// HierarchyMove validates the move before creating this command.
internal sealed class MoveObjectCommand : ICommand
{
    private readonly Scene _scene;
    private readonly GameObject _target;
    private readonly GameObject? _oldParent;
    private readonly GameObject? _newParent;
    private readonly List<GameObject> _sourceBefore;
    private readonly List<GameObject> _sourceAfter;
    private readonly List<GameObject>? _destinationBefore;
    private readonly List<GameObject> _destinationAfter;
    private readonly (Vector3 Position, Vector3 Rotation, Vector3 Scale) _before;
    private readonly (Vector3 Position, Vector3 Rotation, Vector3 Scale) _after;

    // index is the insertion position after removing the object from its source list.
    internal MoveObjectCommand(Scene scene, GameObject target, GameObject? parent, int index,
        (Vector3 Position, Vector3 Rotation, Vector3 Scale) transform)
    {
        _scene = scene;
        _target = target;
        _oldParent = target.Parent;
        _newParent = parent;
        _before = (target.LocalPosition, target.LocalRotation, target.LocalScale);
        _after = transform;

        _sourceBefore = _oldParent == null ? scene.GameObjects : _oldParent.Children!;
        _destinationBefore = parent == null ? scene.GameObjects : parent.Children;

        // Replace lists rather than mutating snapshots retained by earlier Undo commands.
        _sourceAfter = new List<GameObject>(_sourceBefore);
        _sourceAfter.Remove(target);
        _destinationAfter = _oldParent == _newParent
            ? _sourceAfter : new List<GameObject>(_destinationBefore ?? []);
        _destinationAfter.Insert(index, target);
    }

    public void Do()
    {
        SetObjects(_oldParent, _sourceAfter);

        if (_oldParent != _newParent) SetObjects(_newParent, _destinationAfter);

        _target.Parent = _newParent;
        (_target.LocalPosition, _target.LocalRotation, _target.LocalScale) = _after;
    }

    public void Undo()
    {
        SetObjects(_oldParent, _sourceBefore);

        if (_oldParent != _newParent) SetObjects(_newParent, _destinationBefore);

        _target.Parent = _oldParent;
        (_target.LocalPosition, _target.LocalRotation, _target.LocalScale) = _before;
    }

    private void SetObjects(GameObject? owner, List<GameObject>? objects)
    {
        if (owner == null) _scene.GameObjects = objects!;

        else owner.Children = objects;
    }
}
