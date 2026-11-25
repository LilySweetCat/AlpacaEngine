using System.Numerics;
using AlpacaEngine;
using AlpacaEngine.Editor;
using AlpacaEngine.Extensions;
using AlpacaEngine.GameObjects;
using AlpacaEngine.Managers;

internal static class GizmoPivotChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.0001f;

    public static void Run()
    {
        foreach (var pivot in Enum.GetValues<Gizmo.PivotPoint>())
        {
            var left = new GameObject { LocalPosition = new Vector3(-2, 0, 0) };
            var right = new GameObject { LocalPosition = new Vector3(2, 0, 0) };
            GameObject[] selection = [left, right];
            var gesture = new GizmoGesture(selection, right, pivot, false);
            var expectedPivot = pivot == Gizmo.PivotPoint.SelectionCenter ? Vector3.Zero : right.WorldPosition;
            Check(Near(gesture.Pivot, expectedPivot), $"Pivot location: {pivot}");
            Check(gesture.Rotate(2, MathF.PI / 2), $"Group rotation accepted: {pivot}");

            var expectedLeft = pivot switch
            {
                Gizmo.PivotPoint.SelectionCenter => new Vector3(0, -2, 0),
                Gizmo.PivotPoint.ActiveObject => new Vector3(2, -4, 0),
                _ => new Vector3(-2, 0, 0)
            };
            var expectedRight = pivot == Gizmo.PivotPoint.SelectionCenter ? new Vector3(0, 2, 0) : new Vector3(2, 0, 0);
            Check(Near(left.WorldPosition, expectedLeft) && Near(right.WorldPosition, expectedRight),
                $"Rotation changes positions according to pivot: {pivot}");
            Check(Near(Vector3.Transform(Vector3.UnitX, left.WorldRotation), Vector3.UnitY)
                && Near(Vector3.Transform(Vector3.UnitX, right.WorldRotation), Vector3.UnitY),
                $"Both object orientations rotate: {pivot}");

            // Preview replacement is relative to the original snapshot, not the previous angle.
            Check(gesture.Rotate(2, MathF.PI / 2), "Repeated rotation preview accepted");
            Check(Near(left.WorldPosition, expectedLeft), "Repeated preview does not accumulate rotation");
            CommandManager.Clear();
            gesture.Finish(false);
            CommandManager.Undo();
            Check(Near(left.LocalPosition, new Vector3(-2, 0, 0)) && left.LocalRotation == Vector3.Zero
                && right.LocalRotation == Vector3.Zero && !CommandManager.CanUndo, "One Undo restores the whole rotation");
            CommandManager.Redo();
            Check(Near(left.WorldPosition, expectedLeft), "Redo restores the whole rotation");
            CommandManager.Undo();

            gesture = new GizmoGesture(selection, right, pivot, false);
            Check(gesture.Scale(0, 2), $"Group scale accepted: {pivot}");
            expectedLeft = pivot switch
            {
                Gizmo.PivotPoint.SelectionCenter => new Vector3(-4, 0, 0),
                Gizmo.PivotPoint.ActiveObject => new Vector3(-6, 0, 0),
                _ => new Vector3(-2, 0, 0)
            };
            expectedRight = pivot == Gizmo.PivotPoint.SelectionCenter ? new Vector3(4, 0, 0) : new Vector3(2, 0, 0);
            Check(Near(left.WorldPosition, expectedLeft) && Near(right.WorldPosition, expectedRight)
                && Near(left.LocalScale, new Vector3(2, 1, 1)) && Near(right.LocalScale, new Vector3(2, 1, 1)),
                $"Scale changes sizes and positions according to pivot: {pivot}");
            CommandManager.Clear();
            gesture.Finish(false);
            CommandManager.Undo();
            Check(left.LocalScale == Vector3.One && right.LocalScale == Vector3.One && !CommandManager.CanUndo,
                "One Undo restores the whole scale");
            CommandManager.Redo();
            Check(Near(left.WorldPosition, expectedLeft) && Near(right.LocalScale, new Vector3(2, 1, 1)), "Redo restores scale");
            CommandManager.Clear();
            gesture.Finish(true);
            Check(left.LocalScale == Vector3.One && Near(left.WorldPosition, new Vector3(-2, 0, 0)),
                "Cancel restores the full snapshot without history");
        }

        var first = new GameObject();
        var second = new GameObject { LocalPosition = new Vector3(2, 0, 0) };
        var third = new GameObject { LocalPosition = new Vector3(10, 0, 0) };
        Check(Near(GizmoGesture.GetPivot([first, second, third], third, Gizmo.PivotPoint.SelectionCenter), new Vector3(4, 0, 0)),
            "Selection center is the mean of origins, not the bounds center");

        var rotated = new GameObject { LocalRotation = new Vector3(0, 0, 45) };
        var safe = new GameObject { LocalPosition = Vector3.UnitX };
        var skew = new GizmoGesture([safe, rotated], safe, Gizmo.PivotPoint.SelectionCenter, false);
        Check(skew.Move(Vector3.UnitY), "Valid preview before skew");
        var lastSafe = safe.WorldMatrix;
        var lastRotated = rotated.WorldMatrix;
        Check(!skew.Scale(0, 2) && skew.Error!.Contains("skew"), "Reject world-axis scale of a rotated object");
        Check(TransformMath.MatricesMatch(safe.WorldMatrix, lastSafe)
            && TransformMath.MatricesMatch(rotated.WorldMatrix, lastRotated), "Skew rejection leaves the entire last valid preview intact");
        skew.Finish(true);

        var local = new GizmoGesture([safe, rotated], rotated, Gizmo.PivotPoint.IndividualOrigins, true);
        Check(local.Scale(0, 2) && Near(rotated.LocalScale, new Vector3(2, 1, 1))
            && Near(safe.LocalScale, new Vector3(2, 1, 1)), "Individual local scale uses each object's own axes");
        local.Finish(true);

        rotated.LocalRotation = new Vector3(0, 0, 90);
        var activeAxes = new GizmoGesture([safe, rotated], rotated, Gizmo.PivotPoint.SelectionCenter, true);
        Check(activeAxes.Scale(0, 2) && Near(safe.LocalScale, new Vector3(1, 2, 1))
            && Near(rotated.LocalScale, new Vector3(2, 1, 1)), "Shared local axes follow the active object's rotation");
        activeAxes.Finish(true);

        var parent = new GameObject { LocalRotation = new Vector3(0, 0, 90), LocalScale = new Vector3(2, 3, 1) };
        var child = new GameObject { LocalPosition = Vector3.UnitX };
        parent.Children = [child];
        var childWorld = child.WorldMatrix;
        var nested = new GizmoGesture([child, parent], child, Gizmo.PivotPoint.SelectionCenter, false);
        var pivotPosition = nested.Pivot;
        var turn = Matrix4x4.CreateTranslation(-pivotPosition) * Matrix4x4.CreateRotationZ(MathF.PI / 2)
            * Matrix4x4.CreateTranslation(pivotPosition);
        Check(nested.Rotate(2, MathF.PI / 2), "Selected parent and child can rotate together");
        Check(TransformMath.MatricesMatch(child.WorldMatrix, childWorld * turn)
            && child.LocalPosition == Vector3.UnitX && child.LocalRotation == Vector3.Zero,
            "Selected descendant is not rotated twice, regardless of selection order");
        nested.Finish(true);

        parent.LocalScale = Vector3.One;
        var ownOrigins = new GizmoGesture([parent, child], child, Gizmo.PivotPoint.IndividualOrigins, false);
        var parentPosition = parent.WorldPosition;
        var childPosition = child.WorldPosition;
        Check(ownOrigins.Rotate(2, MathF.PI / 2) && Near(parent.WorldPosition, parentPosition)
            && Near(child.WorldPosition, childPosition), "Individual origins keep selected parent and child at their own world positions");
        ownOrigins.Finish(true);
        parent.LocalScale = new Vector3(2, 3, 1);

        var middle = new GameObject { LocalPosition = Vector3.UnitY, Children = [child] };
        parent.Children = [middle];
        childWorld = child.WorldMatrix;
        nested = new GizmoGesture([child, parent], parent, Gizmo.PivotPoint.ActiveObject, false);
        Check(nested.Move(Vector3.One) && TransformMath.MatricesMatch(child.WorldMatrix,
            childWorld * Matrix4x4.CreateTranslation(Vector3.One)), "Unselected intermediate parent follows its selected ancestor once");
        nested.Finish(true);

        var singleChild = new GizmoGesture([child], child, Gizmo.PivotPoint.IndividualOrigins, false);
        Check(!singleChild.Rotate(2, MathF.PI / 4) && singleChild.Error!.Contains("skew"),
            "Rotation beneath a non-uniform parent rejects an unrepresentable local transform");
        parent.LocalScale = new Vector3(0, 1, 1);
        var singular = new GizmoGesture([child], child, Gizmo.PivotPoint.ActiveObject, false);
        Check(!singular.Move(Vector3.One) && singular.Error!.Contains("zero scale"), "Singular parent is rejected");

        CommandManager.Clear();
        var noOp = new GizmoGesture([safe], safe, Gizmo.PivotPoint.SelectionCenter, false);
        Check(noOp.Rotate(2, 0), "Zero rotation accepted");
        noOp.Finish(false);
        Check(!CommandManager.CanUndo, "No-op preview does not create history");
        Console.WriteLine("Gizmo pivot, group transforms, hierarchy, skew rejection and Undo/Redo checks passed.");
    }
}
