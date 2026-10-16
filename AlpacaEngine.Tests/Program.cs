using System.Numerics;
using AlpacaEngine;
using AlpacaEngine.Editor;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;
using Raylib_cs;
using ImGuiNET;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

CommandManager.Clear();
CommandManager.Undo();
CommandManager.Redo();
Check(!CommandManager.CanUndo && !CommandManager.CanRedo, "Empty history is safe");
var edited = new SimpleCube();
CommandManager.Do(new ChangeValueCommand<string>(value => edited.Name = value, edited.Name, "Renamed"));
Check(edited.Name == "Renamed" && CommandManager.CanUndo, "Execute change");
EditorState.Selected = new SimpleCube();
CommandManager.Undo();
Check(edited.Name == "Game Object" && CommandManager.CanRedo, "Undo targets original object");
CommandManager.Redo();
Check(edited.Name == "Renamed" && !CommandManager.CanRedo, "Redo change");
CommandManager.Undo();
CommandManager.RecordChange(value => edited.Name = value, edited.Name, edited.Name);
Check(CommandManager.CanRedo && !CommandManager.CanUndo, "No-op preserves Redo");
edited.LocalPosition = Vector3.One;
CommandManager.RecordChange(value => edited.LocalPosition = value, Vector3.Zero, edited.LocalPosition);
Check(!CommandManager.CanRedo && edited.LocalPosition == Vector3.One, "Record applied edit clears Redo");
CommandManager.Undo();
Check(edited.LocalPosition == Vector3.Zero, "Undo recorded edit");
CommandManager.Redo();
Check(edited.LocalPosition == Vector3.One, "Redo recorded edit");
EditorState.CurrentScene = new Scene();
Check(!CommandManager.CanUndo && !CommandManager.CanRedo && EditorState.Selected == null, "Scene change clears history and selection");

var originalObjects = EditorState.CurrentScene.GameObjects;
var addedChild = new SimpleCube();
var addedObject = new SimpleCube { Child = [addedChild] };
var addedObjects = new List<GameObject>(originalObjects) { addedObject };
CommandManager.Do(new ChangeValueCommand<List<GameObject>>(
    objects => EditorState.CurrentScene.GameObjects = objects, originalObjects, addedObjects));
EditorState.Selected = addedObject;
EditorState.ValidateSelection();
Check(EditorState.Selected == addedObject, "Scene member stays selected");
CommandManager.Undo();
EditorState.ValidateSelection();
Check(EditorState.Selected == null && CommandManager.CanRedo, "Undo addition clears stale selection and preserves Redo");
CommandManager.Redo();
Check(EditorState.CurrentScene.GameObjects.Contains(addedObject), "Redo restores removed object");
EditorState.Selected = addedChild;
EditorState.ValidateSelection();
Check(EditorState.Selected == addedChild, "Nested scene member stays selected");
CommandManager.Undo();
EditorState.ValidateSelection();
Check(EditorState.Selected == null && CommandManager.CanRedo, "Removing parent clears selected descendant");
EditorState.Selected = originalObjects[0];
EditorState.ValidateSelection();
Check(EditorState.Selected == originalObjects[0], "Unrelated removal preserves valid selection");
CommandManager.Clear();

var sibling = new SimpleCube();
var descendant = new SimpleCube();
var nested = new SimpleCube { Child = [descendant] };
var parent = new SimpleCube { Child = [nested, sibling] };
EditorState.CurrentScene = new Scene { GameObjects = [parent, new SimpleCube()] };
EditorState.RenameGameObject(nested, "  Nested renamed  ");
Check(nested.Name == "Nested renamed", "Hierarchy rename trims the name");
CommandManager.Undo();
Check(nested.Name == "Game Object", "Undo hierarchy rename");
EditorState.RenameGameObject(nested, "Game Object");
EditorState.RenameGameObject(nested, "  ");
Check(!CommandManager.CanUndo && CommandManager.CanRedo, "Unchanged and empty names preserve history");
CommandManager.Redo();
Check(nested.Name == "Nested renamed", "Redo hierarchy rename");
CommandManager.Clear();
EditorState.Selected = descendant;
EditorState.DeleteGameObject(nested);
Check(parent.Child!.SequenceEqual(new[] { sibling }) && nested.Parent == null,
    "Delete nested object from its owning list");
Check(EditorState.Selected == null && nested.Child![0] == descendant && descendant.Parent == nested,
    "Delete subtree clears descendant selection and keeps subtree intact");
CommandManager.Undo();
Check(parent.Child!.SequenceEqual(new[] { nested, sibling }) && nested.Parent == parent,
    "Undo deletion restores order and parent");
CommandManager.Redo();
Check(parent.Child!.SequenceEqual(new[] { sibling }) && nested.Parent == null,
    "Redo nested deletion");
CommandManager.Undo();
EditorState.Selected = sibling;
EditorState.DeleteGameObject(nested);
Check(EditorState.Selected == sibling, "Deleting a sibling preserves selection");
CommandManager.Undo();
EditorState.DeleteGameObject(parent);
Check(EditorState.CurrentScene.GameObjects.Count == 1 && EditorState.Selected == null,
    "Root deletion clears selected descendant");
CommandManager.Undo();
Check(EditorState.CurrentScene.GameObjects[0] == parent && parent.Child![0] == nested,
    "Undo root deletion restores subtree and order");
CommandManager.Clear();
EditorState.DeleteGameObject(new SimpleCube());
EditorState.RenameGameObject(new SimpleCube(), "Not in scene");
Check(!CommandManager.CanUndo, "Objects outside the scene do not create history");

Check(Gizmo.IntersectPlane(new Ray(new Vector3(2, 3, 5), -Vector3.UnitZ), Vector3.Zero,
    Vector3.UnitZ, out var hit) && Vector3.Distance(hit, new Vector3(2, 3, 0)) < 0.0001f,
    "Drag plane intersection");
Check(!Gizmo.IntersectPlane(new Ray(Vector3.UnitZ, Vector3.UnitX), Vector3.Zero,
    Vector3.UnitZ, out _), "Parallel ray must not start a drag");
Check(!Gizmo.IntersectPlane(new Ray(Vector3.UnitZ, Vector3.UnitZ), Vector3.Zero,
    Vector3.UnitZ, out _), "Intersection behind ray must be rejected");
Check(MathF.Abs(Gizmo.SignedAngle(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ) - MathF.PI / 2) < 0.0001f,
    "Signed rotation angle");
Check(MathF.Abs(Gizmo.SegmentDistance(new Vector2(5, 3), Vector2.Zero, new Vector2(10, 0)) - 3) < 0.0001f,
    "Screen-space handle hit testing");
foreach (var degrees in new[] { new Vector3(20, 40, 60), new Vector3(-70, 130, -45),
             new Vector3(90, 30, 20), new Vector3(-90, 30, 20), new Vector3(120, -150, 175) })
{
    var rotation = GameObject.Orientation(degrees);
    var roundTrip = GameObject.Orientation(Gizmo.EulerDegrees(rotation));
    Check(MathF.Abs(Quaternion.Dot(rotation, roundTrip)) > 0.99999f, "Euler/quaternion round trip: " + degrees);
}
var cube = new SimpleCube { LocalPosition = new Vector3(2, 1, 0), LocalScale = new Vector3(2, 4, 6), LocalRotation = new Vector3(0, 90, 0) };
EditorState.CurrentScene.GameObjects.Clear();
EditorState.CurrentScene.GameObjects.Add(cube);
Check(Picking.PickAtMouse(new Ray(new Vector3(2, 1, 10), -Vector3.UnitZ)) == cube, "Picking rotated cube");
Check(Picking.PickAtMouse(new Ray(new Vector3(2, 1, 10), Vector3.UnitZ)) == null, "Picking behind camera");

Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
Raylib.InitWindow(640, 480, "Gizmo checks");
try
{
    foreach (var projection in new[] { CameraProjection.Perspective, CameraProjection.Orthographic })
    {
        var camera = new Camera3D { Position = new Vector3(0, 0, 10), Target = Vector3.Zero,
            Up = Vector3.UnitY, FovY = projection == CameraProjection.Perspective ? 60 : 10, Projection = projection };
        foreach (var size in new[] { new Vector2(800, 600), new Vector2(400, 300) })
        {
            var world = new Vector3(1, 2, 0);
            var pixel = Raylib.GetWorldToScreenEx(world, camera, (int)size.X, (int)size.Y);
            var ray = Raylib.GetScreenToWorldRayEx(pixel, camera, (int)size.X, (int)size.Y);
            Check(Gizmo.IntersectPlane(ray, Vector3.Zero, Vector3.UnitZ, out hit)
                && Vector3.Distance(hit, world) < 0.001f, "Projection/ray round trip: " + projection);
        }
    }
    var context = ImGui.CreateContext();
    try
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(640, 480);
        io.DeltaTime = 1f / 60;
        unsafe { io.NativePtr->IniFilename = null; }
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height);
        var drawInspector = typeof(Inspector).GetMethod("DrawInspector",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var inspectorTarget = new SimpleCube();
        EditorState.Selected = inspectorTarget;
        void InspectorFrame(Vector2 mouse, bool down, string text = null)
        {
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, down);
            if (text != null) io.AddInputCharactersUTF8(text);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(io.DisplaySize);
            drawInspector.Invoke(null, null);
            ImGui.Render();
        }
        InspectorFrame(Vector2.Zero, false);
        var fieldY = ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y + ImGui.GetFrameHeight() / 2;
        var nameMouse = new Vector2(80, fieldY);
        InspectorFrame(nameMouse, true);
        InspectorFrame(nameMouse, false);
        InspectorFrame(nameMouse, false, "test");
        Check(inspectorTarget.Name != "Game Object" && !CommandManager.CanUndo,
            "Inspector text preview stays out of history");
        var renamed = inspectorTarget.Name;
        var emptyMouse = new Vector2(500, 300);
        InspectorFrame(emptyMouse, true);
        InspectorFrame(emptyMouse, false);
        Check(CommandManager.CanUndo, "Leaving Inspector field records edit");
        CommandManager.Undo();
        Check(inspectorTarget.Name == "Game Object" && !CommandManager.CanUndo, "Inspector name is one Undo");
        CommandManager.Redo();
        Check(inspectorTarget.Name == renamed, "Redo Inspector name");
        CommandManager.Clear();
        var positionMouse = nameMouse + new Vector2(0, ImGui.GetFrameHeightWithSpacing());
        InspectorFrame(positionMouse, true);
        InspectorFrame(positionMouse + new Vector2(20, 0), true);
        InspectorFrame(positionMouse + new Vector2(40, 0), true);
        Check(inspectorTarget.LocalPosition != Vector3.Zero && !CommandManager.CanUndo,
            "Inspector position previews while dragging");
        InspectorFrame(positionMouse + new Vector2(40, 0), false);
        var inspectorPosition = inspectorTarget.LocalPosition;
        CommandManager.Undo();
        Check(inspectorTarget.LocalPosition == Vector3.Zero && !CommandManager.CanUndo,
            "Inspector drag is one Undo");
        CommandManager.Redo();
        Check(inspectorTarget.LocalPosition == inspectorPosition, "Redo Inspector drag");
        CommandManager.Clear();
        var camera = new Camera3D { Position = new Vector3(0, 0, 10), Target = Vector3.Zero,
            Up = Vector3.UnitY, FovY = 60, Projection = CameraProjection.Perspective };
        var target = new SimpleCube();
        EditorState.ViewTexture = new RenderTexture2D { Texture = new Texture2D { Width = 640, Height = 480 } };
        Gizmo.Mode = Gizmo.Operation.Move;
        Gizmo.Local = false;
        bool Frame(Vector2 mouse, bool down)
        {
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, down);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(io.DisplaySize);
            ImGui.Begin("Gizmo test", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize);
            var consumed = Gizmo.Draw(target, camera, Vector2.Zero, io.DisplaySize, true);
            ImGui.End();
            ImGui.Render();
            Check(ImGui.GetDrawData().TotalVtxCount > 0, "Gizmo overlay emits draw commands");
            return consumed;
        }
        Frame(new Vector2(390, 240), false);
        Check(Frame(new Vector2(390, 240), true) && Gizmo.IsDragging, "Axis click captures drag");
        Check(Frame(new Vector2(410, 240), true) && target.LocalPosition.X > 0.4f
            && MathF.Abs(target.LocalPosition.Y) < 0.0001f, "Drag moves only selected axis");
        Check(!CommandManager.CanUndo, "Drag preview does not enter history");
        Check(Frame(new Vector2(410, 240), false) && !Gizmo.IsDragging, "Release ends drag and consumes click");
        var moved = target.LocalPosition;
        CommandManager.Undo();
        Check(target.LocalPosition == Vector3.Zero && !CommandManager.CanUndo, "Whole drag is one Undo");
        CommandManager.Redo();
        Check(target.LocalPosition == moved, "Redo restores drag result");
        CommandManager.Clear();
        target.LocalPosition = Vector3.Zero;
        Gizmo.Mode = Gizmo.Operation.Scale;
        Frame(new Vector2(390, 240), false);
        Check(Frame(new Vector2(390, 240), true) && Gizmo.IsDragging, "Scale handle captures drag");
        Frame(new Vector2(410, 240), true);
        Check(target.LocalScale.X > 1.1f && target.LocalScale.Y == 1 && target.LocalScale.Z == 1, "Scale only selected local component");
        Frame(new Vector2(410, 240), false);
        var scaled = target.LocalScale;
        CommandManager.Undo();
        Check(target.LocalScale == Vector3.One, "Undo scale drag");
        CommandManager.Redo();
        Check(target.LocalScale == scaled, "Redo scale drag");
        CommandManager.Clear();
        Gizmo.Mode = Gizmo.Operation.Rotate;
        Frame(new Vector2(380, 180), false);
        Check(Frame(new Vector2(380, 180), true) && Gizmo.IsDragging, "Rotation ring captures drag");
        Frame(new Vector2(260, 180), true);
        Check(MathF.Abs(target.LocalRotation.Z - 90) < 0.01f, "Ring drag rotates around selected axis");
        Gizmo.Cancel(true);
        Check(target.LocalRotation == Vector3.Zero && !Gizmo.IsDragging && !CommandManager.CanUndo,
            "Cancel restores starting transform without history");
        Frame(new Vector2(260, 180), false);
        Frame(new Vector2(380, 180), false);
        Frame(new Vector2(380, 180), true);
        Frame(new Vector2(260, 180), true);
        Frame(new Vector2(260, 180), false);
        var rotated = target.LocalRotation;
        CommandManager.Undo();
        Check(target.LocalRotation == Vector3.Zero, "Undo rotation drag");
        CommandManager.Redo();
        Check(target.LocalRotation == rotated, "Redo rotation drag");
        CommandManager.Clear();
        Frame(new Vector2(380, 180), false);
        Frame(new Vector2(380, 180), true);
        Frame(new Vector2(380, 180), false);
        Check(!CommandManager.CanUndo, "Click without movement does not enter history");
    }
    finally { Gizmo.Cancel(); ImGui.DestroyContext(context); }
}
finally { Raylib.CloseWindow(); }
Console.WriteLine("Command history, Inspector and Gizmo checks passed.");
