using AlpacaEngine.Editor;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;

namespace AlpacaEngine.Commands;

internal sealed class DuplicateObjectsCommand : ICommand
{
    private readonly Scene _scene;
    private readonly GameObject[] _originalSelection;
    private readonly GameObject[] _copiedSelection;
    private readonly List<(GameObject? Parent, List<GameObject> Before, List<GameObject> After)> _changes = [];
    private readonly Dictionary<GameObject, GameObject> _copies = [];
    private readonly HashSet<string> _names;

    public DuplicateObjectsCommand(Scene scene, IEnumerable<GameObject> selection)
    {
        _scene = scene;
        _originalSelection = selection.Distinct().ToArray();
        _names = AllObjects(scene.GameObjects).Select(target => target.Name).ToHashSet(StringComparer.Ordinal);

        var selected = _originalSelection.ToHashSet();

        // A selected child is already copied as part of its selected ancestor's tree.
        var roots = _originalSelection.Where(target => !HasSelectedAncestor(target, selected)).ToArray();

        foreach (var group in roots.GroupBy(target => target.Parent))
        {
            var before = group.Key == null ? scene.GameObjects : group.Key.Children!;
            var after = new List<GameObject>();
            var targets = group.ToHashSet();

            foreach (var target in before)
            {
                after.Add(target);

                if (!targets.Contains(target)) continue;

                var copy = CopyTree(target);
                copy.Parent = group.Key;
                after.Add(copy);
            }

            // Keep earlier commands' list snapshots intact.
            _changes.Add((group.Key, before, after));
        }

        _copiedSelection = _originalSelection.Select(target => _copies[target]).ToArray();
    }

    public void Do()
    {
        Apply(true);
        EditorState.SetSelection(_copiedSelection);
    }

    public void Undo()
    {
        Apply(false);
        EditorState.SetSelection(_originalSelection);
    }

    private void Apply(bool duplicate)
    {
        foreach (var (parent, before, after) in _changes)
        {
            var objects = duplicate ? after : before;

            if (parent == null) _scene.GameObjects = objects;
            else parent.Children = objects;

            foreach (var copy in after.Except(before)) copy.Parent = duplicate ? parent : null;
        }
    }

    private GameObject CopyTree(GameObject original)
    {
        var copy = new GameObject
        {
            Name = CopyName(original.Name),
            LocalPosition = original.LocalPosition,
            LocalRotation = original.LocalRotation,
            LocalScale = original.LocalScale,
            Components = original.Components.Select(component => component.Copy()).ToList()
        };
        _copies.Add(original, copy);

        if (original.Children != null) copy.Children = original.Children.Select(CopyTree).ToList();

        return copy;
    }

    private string CopyName(string name)
    {
        var suffix = name.LastIndexOf("_Copy_", StringComparison.Ordinal);

        if (suffix >= 0 && int.TryParse(name[(suffix + 6)..], out _)) name = name[..suffix];

        var index = 0;
        string candidate;

        do { candidate = $"{name}_Copy_{index++}"; }
        while (!_names.Add(candidate));

        return candidate;
    }

    private static bool HasSelectedAncestor(GameObject target, HashSet<GameObject> selected)
    {
        for (var parent = target.Parent; parent != null; parent = parent.Parent)
        {
            if (selected.Contains(parent)) return true;
        }

        return false;
    }

    private static IEnumerable<GameObject> AllObjects(IEnumerable<GameObject> objects)
    {
        foreach (var target in objects)
        {
            yield return target;

            if (target.Children == null) continue;

            foreach (var child in AllObjects(target.Children)) yield return child;
        }
    }
}
