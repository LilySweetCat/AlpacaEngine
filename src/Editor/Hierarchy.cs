using System.Numerics;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

public static class Hierarchy
{
    internal static Texture2D GameObjectIcon;
    private static GameObject? _renameTarget;
    private static Scene? _renameScene;
    private static string _renameName = "";
    private static bool _openRenamePopup;

    private const string DragPayloadType = "SCENE_GAME_OBJECT";
    private static GameObject? _dragSource;
    private static Scene? _dragScene;
    private static ICommand? _pendingMove;
    private static bool _dragCancelled;

    public static void Render()
    {
        if (!ImGui.Begin("Hierarchy"))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginPopupContextWindow("ctx_empty", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("Add GameObject")) EditorState.AddGameObject();

            if (ImGui.MenuItem("Add Sun Light", "", false, !EditorState.HasSunLight)) EditorState.AddSunLight();

            ImGui.EndPopup();
        }

        GameObject? deleteTarget = null;
        BeginDragDrop();
        var itemSpacing = ImGui.GetStyle().ItemSpacing;
        // Contiguous rows make their top/bottom insertion zones reachable without gaps.
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(itemSpacing.X, 0));
        DrawGameObjectTree(EditorState.CurrentScene.GameObjects, ref deleteTarget);
        DrawRootDropTarget();
        ImGui.PopStyleVar();
        FinishDragDrop();

        if (ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()
            && !ImGui.GetIO().WantTextInput && !Gizmo.IsDragging
            && !_openRenamePopup && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId)
            && ImGui.IsKeyPressed(ImGuiKey.Delete, false))
            deleteTarget = EditorState.Selected;

        if (deleteTarget != null) EditorState.DeleteGameObject(deleteTarget);

        DrawRenamePopup();

        ImGui.End();
    }

    private static void DrawGameObjectTree(List<GameObject> gameObjects, ref GameObject? deleteTarget)
    {
        foreach (var gameObject in gameObjects)
        {
            var hasChild = gameObject.Child is { Count: > 0 };
            var flags = ImGuiTreeNodeFlags.SpanAvailWidth
                        | (hasChild ? ImGuiTreeNodeFlags.OpenOnArrow : ImGuiTreeNodeFlags.Leaf)
                        | (gameObject == EditorState.Selected ? ImGuiTreeNodeFlags.Selected : 0);

            // ## hides rest of string from user
            var opened = ImGui.TreeNodeEx($"   {gameObject.Name}###{gameObject.Id}", flags);

            if (!ImGui.IsItemToggledOpen() && ImGui.IsItemClicked(ImGuiMouseButton.Left))
                EditorState.Selected = gameObject;

            var iconMin = ImGui.GetItemRectMin() + new Vector2(
                ImGui.GetTreeNodeToLabelSpacing(), (ImGui.GetItemRectSize().Y - ImGui.GetFontSize()) * 0.5f);

            ImGui.GetWindowDrawList().AddImage((nint)GameObjectIcon.Id, iconMin,
                iconMin + new Vector2(ImGui.GetFontSize()));

            DrawRowDragDrop(gameObject, opened);

            if (ImGui.BeginPopupContextItem($"ctx_{gameObject.Id}"))
            {
                if (ImGui.MenuItem("Rename"))
                {
                    Inspector.FinishEdit();
                    EditorState.Selected = gameObject;
                    _renameTarget = gameObject;
                    _renameScene = EditorState.CurrentScene;
                    _renameName = gameObject.Name;
                    _openRenamePopup = true;
                }

                if (ImGui.MenuItem("Delete", "Del"))
                {
                    // Apply after the entire tree has been drawn, never during enumeration.
                    deleteTarget = gameObject;
                }

                ImGui.EndPopup();
            }

            if (opened)
            {
                if (hasChild) DrawGameObjectTree(gameObject.Child!, ref deleteTarget);

                ImGui.TreePop();
            }
        }
    }

    private static void DrawRenamePopup()
    {
        var focusName = _openRenamePopup;

        if (_openRenamePopup)
        {
            ImGui.OpenPopup("Rename Game Object");
            _openRenamePopup = false;
        }

        if (!ImGui.BeginPopupModal("Rename Game Object", ImGuiWindowFlags.AlwaysAutoResize)) return;

        if (_renameTarget == null || _renameScene != EditorState.CurrentScene
            || !EditorState.Contains(EditorState.CurrentScene.GameObjects, _renameTarget))
        {
            _renameTarget = null;
            _renameScene = null;
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        if (focusName) ImGui.SetKeyboardFocusHere();

        var submitted = ImGui.InputText("Name", ref _renameName, 256,
            ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
        var valid = !string.IsNullOrWhiteSpace(_renameName);
        ImGui.BeginDisabled(!valid);
        var accept = ImGui.Button("Rename") || submitted;
        ImGui.EndDisabled();
        ImGui.SameLine();
        var cancel = ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape, false);

        if ((accept && valid) || cancel)
        {
            if (!cancel) EditorState.RenameGameObject(_renameTarget, _renameName);

            _renameTarget = null;
            _renameScene = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static void BeginDragDrop()
    {
        _pendingMove = null;

        if (ImGui.IsKeyPressed(ImGuiKey.Escape, false)) _dragCancelled = true;
    }

    private static unsafe void DrawRowDragDrop(GameObject target, bool expanded)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoPreviewTooltip))
        {
            _dragSource = target;
            _dragScene = EditorState.CurrentScene;
            byte marker = 1;
            ImGui.SetDragDropPayload(DragPayloadType, (nint)(&marker), 1);
            ImGui.EndDragDropSource();
        }

        if (_dragSource == target && !_dragCancelled)
            ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(ImGuiCol.DragDropTarget));

        if (!ImGui.BeginDragDropTarget()) return;

        var fraction = (ImGui.GetMousePos().Y - min.Y) / (max.Y - min.Y);
        var inside = fraction >= 0.25f && fraction <= 0.75f;
        GameObject? parent;
        int index;
        var lineY = min.Y;
        var lineX = min.X;

        if (inside)
        {
            parent = target;
            index = target.Child?.Count ?? 0;
        }
        else if (fraction > 0.75f && expanded && target.Child is { Count: > 0 })
        {
            // The next visible row is the first child, so this line uses its indentation.
            parent = target;
            index = 0;
            lineY = max.Y;
            lineX += ImGui.GetStyle().IndentSpacing;
        }
        else
        {
            parent = target.Parent;
            var siblings = parent == null ? EditorState.CurrentScene.GameObjects : parent.Child!;
            index = siblings.IndexOf(target) + (fraction > 0.75f ? 1 : 0);
            lineY = fraction > 0.75f ? max.Y : min.Y;
        }

        DrawDropTarget(parent, index, inside, min, max, lineX, lineY);
        ImGui.EndDragDropTarget();
    }

    private static void DrawRootDropTarget()
    {
        var available = ImGui.GetContentRegionAvail();
        // Dummy supplies a drop rectangle without taking clicks from the empty-area context menu.
        ImGui.Dummy(new Vector2(MathF.Max(1, available.X), MathF.Max(24, available.Y)));

        if (!ImGui.BeginDragDropTarget()) return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        DrawDropTarget(null, EditorState.CurrentScene.GameObjects.Count, false, min, max, min.X, min.Y);
        ImGui.EndDragDropTarget();
    }

    private static unsafe void DrawDropTarget(GameObject? parent, int index, bool inside,
        Vector2 min, Vector2 max, float lineX, float lineY)
    {
        var payload = ImGui.AcceptDragDropPayload(DragPayloadType,
            ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);

        if (payload.NativePtr == null || _dragSource == null || _dragScene != EditorState.CurrentScene || _dragCancelled) return;

        var valid = HierarchyMove.TryCreate(_dragScene, _dragSource, parent, index, out var command, out var error);
        var color = valid ? ImGui.GetColorU32(ImGuiCol.DragDropTarget)
            : ImGui.GetColorU32(new Vector4(1, 0.35f, 0.3f, 1));
        var drawList = ImGui.GetWindowDrawList();

        if (inside) drawList.AddRect(min, max, color, 0, ImDrawFlags.None, 2);

        else drawList.AddLine(new Vector2(lineX, lineY), new Vector2(max.X, lineY), color, 2);

        var sourceIndex = _dragSource.Parent == null
            ? _dragScene.GameObjects.IndexOf(_dragSource) : _dragSource.Parent.Child!.IndexOf(_dragSource);
        var insertionIndex = index - (_dragSource.Parent == parent && sourceIndex < index ? 1 : 0);
        ImGui.SetTooltip(valid
            ? $"{_dragSource.Name} -> {parent?.Name ?? "Scene root"} (position {insertionIndex + 1})"
            : error!);

        if (payload.IsDelivery() && valid) _pendingMove = command;
    }

    private static unsafe void FinishDragDrop()
    {
        // Apply only after drawing the whole tree, never while enumerating its lists.
        if (_pendingMove != null)
        {
            Gizmo.Cancel(true);
            Inspector.FinishEdit();
            CommandManager.Do(_pendingMove);
        }

        _pendingMove = null;

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.GetDragDropPayload().NativePtr == null)
        {
            _dragSource = null;
            _dragScene = null;
            _dragCancelled = false;
        }
    }
}
