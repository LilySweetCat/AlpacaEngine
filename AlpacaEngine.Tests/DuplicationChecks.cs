#nullable enable

using System.Numerics;
using AlpacaEngine;
using AlpacaEngine.Extensions;
using AlpacaEngine.Editor;
using AlpacaEngine.Editor.Hierarchy;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using AlpacaEngine.Managers;
using ImGuiNET;

internal static class DuplicationChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        var child = new GameObject { Name = "Door", LocalPosition = Vector3.UnitX,
            Components = [new ModelRenderComponent { ModelPath = "door.glb" }] };
        var crate = new GameObject { Name = "Crate", Children = [child], LocalPosition = new Vector3(2, 3, 4),
            LocalRotation = new Vector3(10, 20, 30), LocalScale = new Vector3(2, 3, 4),
            Components = [new ModelRenderComponent { ModelPath = "crate.glb" }] };
        var sibling = new GameObject { Name = "Crate_Copy_0" };
        var parent = new GameObject { Name = "Parent", Children = [crate, sibling], LocalRotation = new Vector3(0, 40, 0) };
        var other = new GameObject { Name = "Other" };
        var scene = new Scene { GameObjects = [parent, other] };
        EditorState.CurrentScene = scene;
        EditorState.Select(crate);
        EditorState.Select(other, true);
        EditorState.Select(child, true);

        var oldRootList = scene.GameObjects;
        var oldChildList = parent.Children;
        EditorState.DuplicateSelection();

        var copies = EditorState.SelectedObjects.ToArray();
        var copy = copies[0];
        var childCopy = copies[2];
        Check(copies.Length == 3 && EditorState.Selected == childCopy, "Copy selection preserves active object order");
        Check(scene.GameObjects.SequenceEqual(new[] { parent, other, copies[1] })
            && parent.Children!.SequenceEqual(new[] { crate, copy, sibling }), "Copies appear beside originals under each parent");
        Check(copy.Children!.Single() == childCopy && childCopy.Parent == copy && copy.Parent == parent,
            "Selected descendant is copied only once, with correct parent links");
        Check(copy.Name == "Crate_Copy_1" && childCopy.Name == "Door_Copy_0", "Copy names avoid existing scene names");
        Check(new[] { crate.Id, child.Id, other.Id, copy.Id, childCopy.Id, copies[1].Id }.Distinct().Count() == 6,
            "Every duplicate has a fresh ID");
        Check(copy.LocalPosition == crate.LocalPosition && copy.LocalRotation == crate.LocalRotation
            && copy.LocalScale == crate.LocalScale && TransformMath.MatricesMatch(child.WorldMatrix, childCopy.WorldMatrix),
            "Exact local and world transforms survive recursive duplication");
        Check(!ReferenceEquals(copy.Components, crate.Components)
            && !ReferenceEquals(copy.Components[0], crate.Components[0])
            && !ReferenceEquals(childCopy.Components[0], child.Components[0]), "Component lists and instances are independent");
        ((ModelRenderComponent)copy.Components[0]).ModelPath = "changed.glb";
        childCopy.LocalPosition = Vector3.Zero;
        Check(((ModelRenderComponent)crate.Components[0]).ModelPath == "crate.glb" && child.LocalPosition == Vector3.UnitX,
            "Changing duplicate settings and transforms leaves originals alone");
        Check(oldRootList.SequenceEqual(new[] { parent, other }) && oldChildList!.SequenceEqual(new[] { crate, sibling }),
            "Duplication does not mutate lists retained by earlier history");
        CommandManager.Undo();
        Check(scene.GameObjects == oldRootList && parent.Children == oldChildList && copy.Parent == null
            && childCopy.Parent == copy && EditorState.SelectedObjects.SequenceEqual(new[] { crate, other, child })
            && !CommandManager.CanUndo, "One Undo removes all copies and restores original selection");
        CommandManager.Redo();
        Check(parent.Children!.Contains(copy) && copy.Parent == parent && EditorState.Selected == childCopy
            && ((ModelRenderComponent)copy.Components[0]).ModelPath == "changed.glb", "Redo reuses the same objects and IDs");

        EditorState.Select(copy);
        EditorState.DuplicateSelection();
        Check(EditorState.Selected!.Name == "Crate_Copy_2", "Repeated duplication advances the suffix");
        CommandManager.Undo();
        EditorState.Select(null);
        EditorState.DuplicateSelection();
        Check(CommandManager.CanRedo, "Empty duplication does not destroy Redo");

        var folder = Path.Combine(Path.GetTempPath(), "alpaca-duplication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var previousDirectory = EditorState.WorkingDirectory;

        try
        {
            EditorState.WorkingDirectory = folder;
            EditorState.SaveScene();
            EditorState.OpenScene(EditorState.CurrentScenePath!);

            var loaded = EditorState.CurrentScene.GameObjects[0].Children!;
            Check(((ModelRenderComponent)loaded[0].Components[0]).ModelPath == "crate.glb"
                && ((ModelRenderComponent)loaded[1].Components[0]).ModelPath == "changed.glb"
                && loaded[1].Children![0].Parent == loaded[1], "Saving and reopening preserves copies, settings and hierarchy");
        }
        finally
        {
            EditorState.WorkingDirectory = previousDirectory;
            Directory.Delete(folder, true);
        }

        var sun = new GameObject { Components = [new SunLightSourceComponent()] };
        parent = new GameObject { Children = [sun] };
        EditorState.CurrentScene = new Scene { GameObjects = [parent, other] };
        EditorState.Select(other);
        EditorState.Select(parent, true);
        EditorState.DuplicateSelection();
        Check(!CommandManager.CanUndo && EditorState.CurrentScene.GameObjects.Count == 2,
            "A group containing a nested sun is rejected atomically");

        var light = new SunLightSourceComponent { Color = Vector3.UnitX, Intensity = 2, Ambient = 0.4f };
        var lightCopy = (SunLightSourceComponent)light.Copy();
        lightCopy.Intensity = 3;
        Check(light.Intensity == 2 && lightCopy.Color == light.Color && lightCopy.Ambient == light.Ambient,
            "Light copy preserves settings independently");

        EditorState.Select(other);
        EditorState.Select(parent, true);
        EditorState.Select(parent, true);
        Check(EditorState.Selected == other && EditorState.SelectedObjects.Count == 1, "Ctrl-toggle restores the previous active object");
        EditorState.Select(null, true);
        Check(EditorState.Selected == other, "Ctrl-click on empty space preserves selection");
        EditorState.DeleteGameObject(other);
        Check(EditorState.SelectedObjects.Count == 0, "Deleting a selected object removes stale selection");
        EditorState.CurrentScene = new Scene();
        EditorState.AddGameObject();

        var added = EditorState.Selected!;
        EditorState.DuplicateSelection();
        CommandManager.Undo();
        CommandManager.Undo();
        Check(EditorState.CurrentScene.GameObjects.Count == 0, "Duplicate and earlier add undo without corrupting history");
        CommandManager.Redo();
        CommandManager.Redo();
        Check(EditorState.CurrentScene.GameObjects.Count == 2 && EditorState.CurrentScene.GameObjects[0] == added,
            "Add and duplicate redo in order");

        RunInputChecks();
        Console.WriteLine("Group selection, duplication, persistence and Undo/Redo checks passed.");
    }

    private static unsafe void RunInputChecks()
    {
        var context = ImGui.CreateContext();

        try
        {
            var io = ImGui.GetIO();
            io.ConfigMacOSXBehaviors = false;
            io.DisplaySize = new Vector2(640, 480);
            io.DeltaTime = 1f / 60;
            io.NativePtr->IniFilename = null;
            io.Fonts.GetTexDataAsRGBA32(out nint pixels, out int width, out int height);

            var a = new GameObject { Name = "A" };
            var b = new GameObject { Name = "B" };
            EditorState.CurrentScene = new Scene { GameObjects = [a, b] };

            void Frame(Vector2 mouse, bool down)
            {
                io.AddMousePosEvent(mouse.X, mouse.Y);
                io.AddMouseButtonEvent(0, down);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(io.DisplaySize);
                Hierarchy.Render();
                EditorUI.HandleShortcuts();
                ImGui.Render();
            }

            Frame(Vector2.Zero, false);

            var first = new Vector2(80, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y + ImGui.GetTextLineHeight() / 2);
            var second = first + new Vector2(0, ImGui.GetTextLineHeight());
            Frame(first, false);
            Frame(first, true);
            Frame(first, false);
            io.AddKeyEvent(ImGuiKey.LeftCtrl, true);
            io.AddKeyEvent(ImGuiKey.ModCtrl, true);
            Frame(second, false);
            Frame(second, true);
            Frame(second, false);
            Check(EditorState.SelectedObjects.SequenceEqual(new[] { a, b }), "ImGui Ctrl-click adds a second object");
            io.AddKeyEvent(ImGuiKey.D, true);
            Frame(second, false);
            Check(EditorState.CurrentScene.GameObjects.Count == 2 && !CommandManager.CanUndo,
                "Ctrl+D no longer duplicates objects");
            io.AddKeyEvent(ImGuiKey.D, false);
            io.AddKeyEvent(ImGuiKey.LeftCtrl, false);
            io.AddKeyEvent(ImGuiKey.ModCtrl, false);
            Frame(second, false);
            io.AddKeyEvent(ImGuiKey.LeftShift, true);
            io.AddKeyEvent(ImGuiKey.ModShift, true);
            Frame(second, false);
            io.AddKeyEvent(ImGuiKey.D, true);
            Frame(second, false);
            Check(EditorState.CurrentScene.GameObjects.Count == 4 && EditorState.SelectedObjects.Count == 2,
                "Shift+D duplicates the selected group through the editor shortcut");
            Frame(second, false);
            Check(EditorState.CurrentScene.GameObjects.Count == 4, "Holding Shift+D does not repeat duplication");
            io.AddKeyEvent(ImGuiKey.D, false);
            io.AddKeyEvent(ImGuiKey.LeftShift, false);
            io.AddKeyEvent(ImGuiKey.ModShift, false);
            Frame(second, false);
            io.AddKeyEvent(ImGuiKey.LeftCtrl, true);
            io.AddKeyEvent(ImGuiKey.ModCtrl, true);
            Frame(second, false);
            io.AddKeyEvent(ImGuiKey.Z, true);
            Frame(second, false);
            Check(EditorState.CurrentScene.GameObjects.SequenceEqual(new[] { a, b })
                && EditorState.SelectedObjects.SequenceEqual(new[] { a, b }), "Ctrl+Z restores originals and group selection");
            io.AddKeyEvent(ImGuiKey.Z, false);
            Frame(second, false);
            Frame(second, true);
            Frame(second, false);
            Check(EditorState.Selected == a && EditorState.SelectedObjects.Count == 1, "ImGui Ctrl-click removes a selected object");
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
