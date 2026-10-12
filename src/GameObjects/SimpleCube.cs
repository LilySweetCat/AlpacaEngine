using Raylib_cs;

namespace AlpacaEngine;

public class SimpleCube : GameObject
{
    public Color cubeColor = Color.Gray ;
    
    public override void Render()
    {
        BeginTransform();
        Raylib.DrawCube(System.Numerics.Vector3.Zero, Scale.X, Scale.Y, Scale.Z, cubeColor);
        Rlgl.PopMatrix();
    }

    public override void RenderWireframe()
    {
        BeginTransform();
        Raylib.DrawCubeWires(System.Numerics.Vector3.Zero, Scale.X, Scale.Y, Scale.Z, Color.Yellow);
        Rlgl.PopMatrix();
    }

    private void BeginTransform()
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(Position.X, Position.Y, Position.Z);
        Rlgl.Rotatef(Rotation.Y, 0, 1, 0);
        Rlgl.Rotatef(Rotation.X, 1, 0, 0);
        Rlgl.Rotatef(Rotation.Z, 0, 0, 1);
    }
}
