#nullable enable

using System.Numerics;
using AlpacaEngine;
using AlpacaEngine.Editor;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using ImGuiNET;

internal static class HierarchyChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Move(GameObject target, GameObject? parent, int index)
    {
        Check(HierarchyMove.TryCreate(EditorState.CurrentScene, target, parent, index, out var command, out var error),
            error ?? "Move should be valid");

        if (command != null) CommandManager.Do(command);
    }

    public static void Run()
    {
        var a = new GameObject();
        var b = new GameObject();
        var c = new GameObject();
        var scene = new Scene { GameObjects = [a, b, c] };
        EditorState.CurrentScene = scene;
        Move(a, null, 3);
        Check(scene.GameObjects.SequenceEqual(new[] { b, c, a }), "Reorder down adjusts the removal index");
        CommandManager.Undo();
        Check(scene.GameObjects.SequenceEqual(new[] { a, b, c }), "Undo restores sibling order");
        Move(a, null, 1);
        Check(!CommandManager.CanUndo && CommandManager.CanRedo, "No-op preserves Redo");
        CommandManager.Redo();
        Move(a, null, 0);
        Check(scene.GameObjects.SequenceEqual(new[] { a, b, c }), "Reorder upwards");

        var descendant = new GameObject { LocalPosition = new Vector3(2, 1, -3) };
        a.Child = [descendant];
        a.LocalPosition = new Vector3(3, -4, 5);
        a.LocalRotation = new Vector3(15, 30, -45);
        a.LocalScale = new Vector3(2, 3, -4);
        b.LocalPosition = new Vector3(-10, 4, 7);
        b.LocalRotation = new Vector3(-20, 55, 10);
        b.LocalScale = new Vector3(2);
        var world = a.WorldMatrix;
        var childWorld = descendant.WorldMatrix;
        var originalPosition = a.LocalPosition;
        var originalRotation = a.LocalRotation;
        var originalScale = a.LocalScale;
        CommandManager.Clear();
        Move(a, b, 0);
        Check(a.Parent == b && b.Child!.Single() == a && !scene.GameObjects.Contains(a), "Reparent whole subtree");
        Check(HierarchyMove.MatricesMatch(world, a.WorldMatrix)
            && HierarchyMove.MatricesMatch(childWorld, descendant.WorldMatrix), "Preserve world transform including mirrored scale and descendants");
        Check(!HierarchyMove.TryCreate(scene, b, descendant, 0, out _, out _), "Reject descendant cycle");
        Check(!HierarchyMove.TryCreate(scene, a, a, 0, out _, out _), "Reject self-parenting");
        CommandManager.Undo();
        Check(a.Parent == null && b.Child == null && scene.GameObjects[0] == a
            && a.LocalPosition == originalPosition && a.LocalRotation == originalRotation && a.LocalScale == originalScale,
            "One Undo restores parent, null child list, index and exact local values");
        CommandManager.Redo();
        Move(a, null, scene.GameObjects.Count);
        Check(a.Parent == null && scene.GameObjects.Last() == a && HierarchyMove.MatricesMatch(world, a.WorldMatrix),
            "Return to root preserves world transform");
        CommandManager.Undo();
        CommandManager.Undo();
        Check(scene.GameObjects.SequenceEqual(new[] { a, b, c }) && b.Child == null, "Multiple moves undo in order");

        b.LocalScale = new Vector3(1, 2, 3);
        Check(!HierarchyMove.TryCreate(scene, a, b, 0, out _, out var skewError) && skewError!.Contains("skew"),
            "Reject skew without touching the scene");
        b.LocalScale = new Vector3(0, 1, 1);
        Check(!HierarchyMove.TryCreate(scene, a, b, 0, out _, out _), "Reject singular parent");
        Check(!HierarchyMove.TryCreate(scene, new GameObject(), null, 0, out _, out _), "Reject foreign source");
        Check(!HierarchyMove.TryCreate(scene, a, new GameObject(), 0, out _, out _), "Reject foreign parent");
        Check(!HierarchyMove.TryCreate(scene, a, null, 99, out _, out _), "Reject invalid index");

        // Earlier add/delete commands retain list snapshots; moves must not modify those lists.
        EditorState.CurrentScene = new Scene();
        EditorState.AddGameObject();
        a = EditorState.Selected!;
        EditorState.AddGameObject();
        b = EditorState.Selected!;
        Move(a, b, 0);
        CommandManager.Undo();
        CommandManager.Undo();
        Check(EditorState.CurrentScene.GameObjects.Single() == a && a.Parent == null, "Move does not corrupt earlier history");
        CommandManager.Redo();
        CommandManager.Redo();
        Check(b.Child!.Single() == a && a.Parent == b, "Redo after earlier history restores move");

        var random = new Random(123);

        for (var i = 0; i < 100; i++)
        {
            Vector3 RandomVector() => new((float)random.NextDouble() * 100 - 50,
                (float)random.NextDouble() * 100 - 50, (float)random.NextDouble() * 100 - 50);
            a = new GameObject { LocalPosition = RandomVector(), LocalRotation = RandomVector(), LocalScale = new Vector3(1, 2, 3) };
            b = new GameObject { LocalPosition = RandomVector(), LocalRotation = RandomVector(), LocalScale = new Vector3(2) };
            c = new GameObject { LocalPosition = RandomVector(), LocalRotation = RandomVector(), LocalScale = new Vector3(0.5f), Child = [a] };
            EditorState.CurrentScene = new Scene { GameObjects = [b, c] };
            world = a.WorldMatrix;
            Move(a, b, 0);
            Check(HierarchyMove.MatricesMatch(world, a.WorldMatrix), "Random transformed parent round trip");
        }

        RunDragChecks();
        Console.WriteLine("Hierarchy transforms, validation, Undo/Redo and ImGui drag checks passed.");
    }

    private static unsafe void RunDragChecks()
    {
        var context = ImGui.CreateContext();

        try
        {
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(640, 480);
            io.DeltaTime = 1f / 60;
            io.NativePtr->IniFilename = null;
            io.Fonts.GetTexDataAsRGBA32(out nint pixels, out int width, out int height);
            var a = new GameObject { Name = "A" };
            var b = new GameObject { Name = "B" };
            var c = new GameObject { Name = "C" };
            EditorState.CurrentScene = new Scene { GameObjects = [a, b, c] };

            void Frame(Vector2 mouse, bool down)
            {
                io.AddMousePosEvent(mouse.X, mouse.Y);
                io.AddMouseButtonEvent(0, down);
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(io.DisplaySize);
                Hierarchy.Render();
                ImGui.Render();
            }

            Frame(Vector2.Zero, false);
            var rowHeight = ImGui.GetTextLineHeight();
            var top = ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y;
            var first = new Vector2(80, top + rowHeight / 2);
            var second = first + new Vector2(0, rowHeight);
            var third = first + new Vector2(0, 2 * rowHeight);

            void Drag(Vector2 from, Vector2 to, bool release = true)
            {
                Frame(from, false);
                Frame(from, true);
                Frame(to, true);
                Frame(to, true);

                if (release) Frame(to, false);
            }

            Drag(first, second, release: false);
            Check(a.Parent == null && !CommandManager.CanUndo, "Drag preview does not mutate scene or history");
            Frame(second, false);
            Check(a.Parent == b && b.Child!.Single() == a, "Native ImGui row drop reparents");
            CommandManager.Undo();
            Frame(first, false);
            Drag(third, new Vector2(80, top + rowHeight - 1));
            Check(EditorState.CurrentScene.GameObjects.SequenceEqual(new[] { a, c, b }) && c.Parent == null,
                "Native ImGui edge drop draws insertion and reorders");
            CommandManager.Undo();
            Frame(first, false);
            Drag(first, second);
            Frame(first, false);
            // A is hidden inside collapsed B; expose it using the tree arrow.
            var arrow = new Vector2(ImGui.GetStyle().WindowPadding.X + 5, first.Y);
            Frame(arrow, true);
            Frame(arrow, false);
            Frame(second, false);
            Drag(second, new Vector2(80, 300));
            Check(a.Parent == null && EditorState.CurrentScene.GameObjects.Last() == a, "Empty-area drop returns child to root");

            Frame(first, false);
            var beforeOutsideDrop = EditorState.CurrentScene.GameObjects.ToArray();
            Drag(first, new Vector2(700, 300));
            Check(EditorState.CurrentScene.GameObjects.SequenceEqual(beforeOutsideDrop), "Release outside hierarchy leaves scene unchanged");

            Frame(first, false);
            Drag(first, second, release: false);
            io.AddKeyEvent(ImGuiKey.Escape, true);
            Frame(second, true);
            Frame(second, false);
            io.AddKeyEvent(ImGuiKey.Escape, false);
            Frame(second, false);
            Check(EditorState.CurrentScene.GameObjects.SequenceEqual(beforeOutsideDrop) && b.Parent == null,
                "Escape cancels pending drop");

            // The root drop area must keep the existing empty-space context menu available.
            Frame(new Vector2(80, 300), false);
            io.AddMouseButtonEvent(1, true);
            Frame(new Vector2(80, 300), false);
            io.AddMouseButtonEvent(1, false);
            Frame(new Vector2(80, 300), false);
            ImGui.NewFrame();
            Check(ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel),
                "Empty-space context menu still opens");
            ImGui.EndFrame();
        }
        finally
        {
            ImGui.DestroyContext(context);
        }
    }
}
