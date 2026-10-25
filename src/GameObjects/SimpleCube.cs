using System.Numerics;
using Raylib_cs;

namespace AlpacaEngine.GameObjects;

public class SimpleCube : GameObject
{
    public Color CubeColor = Color.Gray;

    public SimpleCube()
    {
        Name = "SimpleCube";
    }

    public override void Render()
    {
        base.Render();
        Raylib.DrawCube(Vector3.Zero, 1, 1, 1, CubeColor);
    }

    // Same shape as Render: components first, then the cube outline.
    public override void RenderOnSelected()
    {
        base.RenderOnSelected();
        Raylib.DrawCubeWires(Vector3.Zero, 1, 1, 1, Color.Yellow);
    }
}
