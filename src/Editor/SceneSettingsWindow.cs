using System.Numerics;
using AlpacaEngine.Managers;
using ImGuiNET;

namespace AlpacaEngine.Editor;

public static class SceneSettingsWindow
{
    public static bool IsOpen = true;

    private static Scene? _editScene;
    private static Vector3 _colorBeforeEdit;

    public static void Render()
    {
        if (!IsOpen)
        {
            FinishEdit();
            return;
        }

        if (!ImGui.Begin("Scene Settings", ref IsOpen))
        {
            FinishEdit();
            ImGui.End();
            return;
        }

        var scene = EditorState.CurrentScene;
        var before = scene.BackgroundColor;
        ImGui.ColorEdit3("Background Color", ref scene.BackgroundColor);

        if (ImGui.IsItemActivated())
        {
            FinishEdit();
            _editScene = scene;
            _colorBeforeEdit = before;
        }

        if (ImGui.IsItemDeactivated()) FinishEdit();

        if (ImGui.Button("Reset Background Color"))
        {
            FinishEdit();
            CommandManager.RecordChange(value => scene.BackgroundColor = value, scene.BackgroundColor, Vector3.Zero);
            scene.BackgroundColor = Vector3.Zero;
        }

        ImGui.End();
    }

    internal static void FinishEdit()
    {
        if (_editScene == null) return;

        var scene = _editScene;
        CommandManager.RecordChange(value => scene.BackgroundColor = value, _colorBeforeEdit, scene.BackgroundColor);
        _editScene = null;
    }
}
