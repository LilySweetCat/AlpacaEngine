using System.Numerics;
using AlpacaEngine.Logging;
using ImGuiNET;

namespace AlpacaEngine.Editor;

public static class LogWindow
{
    public static bool IsOpen;
    private static bool _showDebug;
    private static bool _showInfo = true;
    private static bool _showWarning = true;
    private static bool _showError = true;
    private static bool _autoScroll = true;
    private static long _lastEntryId;

    public static void Render()
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(800, 300), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Logs", ref IsOpen)) DrawContents();

        ImGui.End();
    }

    private static void DrawContents()
    {
        if (ImGui.Button("Clear")) Logger.ClearHistory();

        ImGui.SameLine();
        ImGui.Checkbox("Debug", ref _showDebug);
        ImGui.SameLine();
        ImGui.Checkbox("Info", ref _showInfo);
        ImGui.SameLine();
        ImGui.Checkbox("Warning", ref _showWarning);
        ImGui.SameLine();
        ImGui.Checkbox("Error", ref _showError);
        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);
        ImGui.TextWrapped($"Log file: {Logger.FilePath ?? "unavailable"}");
        ImGui.Separator();

        var entries = Logger.GetEntries();

        if (ImGui.BeginChild("Log entries", Vector2.Zero, ImGuiChildFlags.None,
                ImGuiWindowFlags.HorizontalScrollbar))
        {
            foreach (var entry in entries)
            {
                if (!IsVisible(entry.Level)) continue;

                ImGui.PushID(entry.Id.ToString());
                ImGui.PushStyleColor(ImGuiCol.Text, GetColor(entry.Level));
                // Keep messages out of widget labels: native messages can contain ImGui's ## markers.
                var expanded = ImGui.TreeNodeEx("##entry", ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.SameLine();
                ImGui.TextUnformatted(entry.Summary);
                ImGui.PopStyleColor();

                if (expanded)
                {
                    ImGui.TextUnformatted(entry.Details);

                    if (ImGui.SmallButton("Copy details")) ImGui.SetClipboardText(entry.Details);

                    ImGui.TreePop();
                }

                ImGui.PopID();
            }

            var latestId = entries.LastOrDefault()?.Id ?? 0;

            if (_autoScroll && latestId != _lastEntryId) ImGui.SetScrollHereY(1f);

            _lastEntryId = latestId;
        }

        ImGui.EndChild();
    }

    private static bool IsVisible(LogLevel level) => level switch
    {
        LogLevel.Debug => _showDebug,
        LogLevel.Info => _showInfo,
        LogLevel.Warning => _showWarning,
        LogLevel.Error => _showError,
        _ => false
    };

    private static Vector4 GetColor(LogLevel level) => level switch
    {
        LogLevel.Debug => new Vector4(0.65f, 0.65f, 0.65f, 1),
        LogLevel.Warning => new Vector4(1, 0.8f, 0.3f, 1),
        LogLevel.Error => new Vector4(1, 0.4f, 0.4f, 1),
        _ => ImGui.GetStyle().Colors[(int)ImGuiCol.Text]
    };
}
