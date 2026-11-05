using System.Numerics;
using AlpacaEngine.Editor;
using ImGuiNET;

namespace AlpacaEngine.GameObjects.Components;

public class SunLightSourceComponent : IComponent
{
    public Vector3 Color = Vector3.One;
    public float Intensity = 1f;
    public float Ambient = 0.2f;

    // Local +Z points along the light rays. Translation and scale do not affect them.
    public static Vector3 GetDirection(GameObject owner) =>
        Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, owner.WorldRotation));

    public void DrawOnInspector()
    {
        var target = EditorState.Selected;

        if (target == null) return;

        var color = Color;
        ImGui.ColorEdit3("Color", ref Color);
        Inspector.TrackEdit(target, "Sun.Color", color, () => Color, value => Color = value);

        var intensity = Intensity;
        ImGui.DragFloat("Intensity", ref Intensity, 0.01f, 0f, 10f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
        Inspector.TrackEdit(target, "Sun.Intensity", intensity, () => Intensity, value => Intensity = value);

        var ambient = Ambient;
        ImGui.SliderFloat("Ambient", ref Ambient, 0f, 1f);
        Inspector.TrackEdit(target, "Sun.Ambient", ambient, () => Ambient, value => Ambient = value);
        ImGui.TextWrapped("Rotation controls the light direction (+Z). Position and scale do not change lighting.");
    }

    public void Process(GameObject gameObject) { }

    public void ProcessOnSelected(GameObject gameObject) { }
}
