using System.Numerics;
using Raylib_cs;

namespace AlpacaEngine.GameObjects;

public class SimpleCube : GameObject
{
    public Color CubeColor = Color.Gray ;

    public override void Render()
    {
        Raylib.DrawCube(Vector3.Zero, LocalScale.X, LocalScale.Y, LocalScale.Z, CubeColor);
    }

    public override void RenderWireframe()
    {
        Raylib.DrawCubeWires(Vector3.Zero, LocalScale.X, LocalScale.Y, LocalScale.Z, Color.Yellow);
    }
}
