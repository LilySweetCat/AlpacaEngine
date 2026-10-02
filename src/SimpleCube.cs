using System.Numerics;
using Raylib_cs;

namespace AlpacaEngine;

public abstract class GameObject
{
    public Vector3 Position = Vector3.Zero;
    public Vector3 Scale = Vector3.One;
    public Vector3 Rotation = Vector3.Zero;

    public virtual void Render()
    {
        
    }
}

public class SimpleCube : GameObject
{
    public Color cubeColor = Color.Gray ;
    
    public override void Render()
    {
        Raylib.DrawCube(Position, Scale.X, Scale.Y, Scale.Z, cubeColor);
        Raylib.DrawCubeWires(Position, Scale.X, Scale.Y, Scale.Z, Color.Yellow);
    }
}