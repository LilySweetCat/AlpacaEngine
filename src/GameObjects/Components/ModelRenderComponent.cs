using System.Numerics;
using AlpacaEngine.Editor;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.GameObjects.Components;

public class ModelRenderComponent : IComponent
{
    public string ModelPath = "";
    private readonly AssetBrowser _modelPicker = new(ModelManager.SupportedExtensions);

    public void DrawOnInspector()
    {
        ImGui.TextUnformatted("Model");
        ImGui.SameLine();
        
        if (ImGui.Button("Select")) _modelPicker.OpenPicker(ModelPath);
        
        if (!string.IsNullOrWhiteSpace(ModelPath))
        {
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                CommandManager.Do(new ChangeValueCommand<string>(value => ModelPath = value, ModelPath, ""));
            ImGui.TextWrapped(ModelPath);
        }
        
        if (_modelPicker.DrawPicker() is { } path && path != ModelPath)
            CommandManager.Do(new ChangeValueCommand<string>(value => ModelPath = value,
                ModelPath, path));
        
        if (string.IsNullOrWhiteSpace(ModelPath)) ImGui.TextDisabled("No model selected");
        else if (GetResolvedPath() is not { } resolvedPath) ImGui.TextWrapped("Invalid model path.");
        else if (ModelManager.GetError(resolvedPath) is { } error)
        {
            ImGui.TextWrapped(error);
            if (ImGui.Button("Retry loading")) ModelManager.Retry(resolvedPath);
        }
    }

    public void Process(GameObject gameObject)
    {
        // GameObject's complete WorldMatrix is already active in the renderer.
        if (!TryGetModel(out var model)) return;
        
        Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
    }

    public void ProcessOnSelected(GameObject gameObject)
    {
        if (!TryGetModel(out var model)) return;
        
        //Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        Raylib.DrawModelWires(model, Vector3.Zero, 1f, Color.Yellow);
    }

    private string? GetResolvedPath()
    {
        try
        {
            return Path.GetFullPath(ModelPath,
                Path.GetFullPath(EditorState.WorkingDirectory ?? Directory.GetCurrentDirectory()));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public bool TryGetModel(out Model model)
    {
        model = default;
        return !string.IsNullOrWhiteSpace(ModelPath) && GetResolvedPath() is { } path
                                                     && ModelManager.TryGet(path, out model);
    }
}