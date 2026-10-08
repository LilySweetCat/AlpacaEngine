using Raylib_cs;

namespace AlpacaEngine;

public class SimpleCube : GameObject
{
    public Color cubeColor = Color.Gray ;
    
    public override void Render()
    {
        Raylib.DrawCube(Position, Scale.X, Scale.Y, Scale.Z, cubeColor);
        Raylib.DrawCubeWires(Position, Scale.X, Scale.Y, Scale.Z, Color.Yellow);
    }
}