using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using AlpacaEngine.Managers;
using ImGuiNET;

namespace AlpacaEngine.Editor;

public static class Inspector
{
    private static GameObject? _editTarget;
    private static string? _editProperty;
    private static Action? _finishEdit;

    public static void Render()
    {
        // Another panel changed the selection: close the pending edit of the old object first.
        if (_editTarget != EditorState.Selected) FinishEdit();

        if (!ImGui.Begin("Inspector"))
        {
            FinishEdit();
            ImGui.End();
            return;
        }

        var target = EditorState.Selected;

        if (target != null)
        {
            ImGui.PushID(target.Id.ToString());

            var name = target.Name;
            ImGui.InputText("Name", ref target.Name, 64);
            TrackEdit(target, nameof(target.Name), name, () => target.Name, value => target.Name = value);

            var position = target.LocalPosition;
            ImGui.DragFloat3("Local Position", ref target.LocalPosition);
            TrackEdit(target, nameof(target.LocalPosition), position, () => target.LocalPosition, value => target.LocalPosition = value);

            var rotation = target.LocalRotation;
            ImGui.DragFloat3("Local Rotation (degrees)", ref target.LocalRotation);
            TrackEdit(target, nameof(target.LocalRotation), rotation, () => target.LocalRotation, value => target.LocalRotation = value);

            var scale = target.LocalScale;
            ImGui.DragFloat3("Local Scale", ref target.LocalScale);
            TrackEdit(target, nameof(target.LocalScale), scale, () => target.LocalScale, value => target.LocalScale = value);

            for (var i = 0; i < target.Components.Count; i++)
            {
                ImGui.PushID(i);

                if (ImGui.CollapsingHeader(target.Components[i].GetType().Name, ImGuiTreeNodeFlags.DefaultOpen))
                    target.Components[i].DrawOnInspector();

                ImGui.PopID();
            }

            if (ImGui.Button("Add Component")) ImGui.OpenPopup("AddComponentPopup");

            if (ImGui.BeginPopup("AddComponentPopup"))
            {
                if (ImGui.MenuItem("ModelRenderComponent"))
                    EditorState.AddComponent(target, new ModelRenderComponent());

                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        ImGui.End();
    }

    internal static void TrackEdit<T>(GameObject target, string property, T before, Func<T> getValue, Action<T> setValue)
    {
        if (ImGui.IsItemActivated())
        {
            FinishEdit();
            _editTarget = target;
            _editProperty = property;
            _finishEdit = () => CommandManager.RecordChange(setValue, before, getValue());
        }

        if (ImGui.IsItemDeactivated() && _editProperty == property) FinishEdit();
    }

    internal static void FinishEdit(bool record = true)
    {
        if (record) _finishEdit?.Invoke();

        _finishEdit = null;
        _editTarget = null;
        _editProperty = null;
    }

}
