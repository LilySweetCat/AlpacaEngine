using System.Numerics;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using Raylib_cs;

namespace AlpacaEngine.Rendering;

// Owns the shared shader and cube mesh; components only serialize light settings.
public static class SceneLighting
{
    private static Shader _shader;
    private static Model _cube;
    private static bool _initialized;
    private static int _enabledLocation;
    private static int _directionLocation;
    private static int _colorLocation;
    private static int _intensityLocation;
    private static int _ambientLocation;
    private static readonly int[] NormalLocations = new int[3];

    public static IEnumerable<(GameObject Owner, SunLightSourceComponent Light)> GetSuns(IEnumerable<GameObject> objects)
    {
        foreach (var owner in objects)
        {
            foreach (var light in owner.Components.OfType<SunLightSourceComponent>())
                yield return (owner, light);

            if (owner.Child == null) continue;

            foreach (var sun in GetSuns(owner.Child)) yield return sun;
        }
    }

    public static void Initialize()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "assets", "shaders");
        _shader = Raylib.LoadShader(Path.Combine(directory, "lighting.vert"), Path.Combine(directory, "lighting.frag"));
        _enabledLocation = Raylib.GetShaderLocation(_shader, "lightingEnabled");

        // Raylib can return its default shader when compilation fails.
        if (!Raylib.IsShaderValid(_shader) || _enabledLocation < 0)
        {
            Raylib.UnloadShader(_shader);
            throw new InvalidOperationException("Could not load the scene lighting shader.");
        }

        _directionLocation = Raylib.GetShaderLocation(_shader, "sunDirection");
        _colorLocation = Raylib.GetShaderLocation(_shader, "sunColor");
        _intensityLocation = Raylib.GetShaderLocation(_shader, "sunIntensity");
        _ambientLocation = Raylib.GetShaderLocation(_shader, "ambient");

        for (var i = 0; i < NormalLocations.Length; i++)
            NormalLocations[i] = Raylib.GetShaderLocation(_shader, "normalRow" + i);

        _cube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
        _initialized = true;
    }

    public static void Prepare(Scene scene)
    {
        var (owner, light) = GetSuns(scene.GameObjects).FirstOrDefault();
        Raylib.SetShaderValue(_shader, _enabledLocation, light == null ? 0 : 1, ShaderUniformDataType.Int);

        if (light == null) return;

        Raylib.SetShaderValue(_shader, _directionLocation, SunLightSourceComponent.GetDirection(owner), ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, _colorLocation, light.Color, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, _intensityLocation, MathF.Max(0f, light.Intensity), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(_shader, _ambientLocation, Math.Clamp(light.Ambient, 0f, 1f), ShaderUniformDataType.Float);
    }

    internal static (Vector3 Row0, Vector3 Row1, Vector3 Row2) GetNormalRows(Matrix4x4 world)
    {
        // Normals use the inverse transpose, including non-uniform parent scale.
        // Explicit rows avoid confusing System.Numerics and OpenGL matrix layouts.
        if (!Matrix4x4.Invert(world, out var inverse)) inverse = Matrix4x4.Identity;

        return (new Vector3(inverse.M11, inverse.M12, inverse.M13),
            new Vector3(inverse.M21, inverse.M22, inverse.M23),
            new Vector3(inverse.M31, inverse.M32, inverse.M33));
    }

    public static unsafe void DrawModel(Model model, GameObject owner, Color tint)
    {
        if (!_initialized)
        {
            Raylib.DrawModel(model, Vector3.Zero, 1f, tint);
            return;
        }

        // Model.Transform comes from native raylib in OpenGL layout, whereas
        // GameObject.WorldMatrix uses System.Numerics row-vector layout.
        var rows = GetNormalRows(Matrix4x4.Transpose(model.Transform) * owner.WorldMatrix);
        Raylib.SetShaderValue(_shader, NormalLocations[0], rows.Row0, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, NormalLocations[1], rows.Row1, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, NormalLocations[2], rows.Row2, ShaderUniformDataType.Vec3);

        // Models can share cached materials. Restore their shaders so resource ownership
        // stays with this class and wireframe selection keeps using the default shader.
        var originalShaders = new Shader[model.MaterialCount];

        for (var i = 0; i < model.MaterialCount; i++)
        {
            originalShaders[i] = model.Materials[i].Shader;
            model.Materials[i].Shader = _shader;
        }

        try { Raylib.DrawModel(model, Vector3.Zero, 1f, tint); }
        finally
        {
            for (var i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = originalShaders[i];
        }
    }

    public static void DrawCube(GameObject owner, Color tint)
    {
        if (!_initialized)
        {
            Raylib.DrawCube(Vector3.Zero, 1f, 1f, 1f, tint);
            return;
        }

        DrawModel(_cube, owner, tint);
    }

    public static void DrawGizmos(Scene scene, GameObject? selected)
    {
        foreach (var (owner, _) in GetSuns(scene.GameObjects))
        {
            var position = owner.WorldPosition;
            var direction = SunLightSourceComponent.GetDirection(owner);
            var color = owner == selected ? Color.Yellow : Color.Orange;
            Raylib.DrawSphereWires(position, 0.2f, 8, 12, color);
            var tip = position + direction;
            var side = Vector3.Normalize(Vector3.Cross(direction,
                MathF.Abs(direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX)) * 0.15f;
            Raylib.DrawLine3D(position, tip, color);
            Raylib.DrawLine3D(tip, tip - direction * 0.25f + side, color);
            Raylib.DrawLine3D(tip, tip - direction * 0.25f - side, color);
        }
    }

    public static void Unload()
    {
        if (!_initialized) return;

        Raylib.UnloadModel(_cube);
        Raylib.UnloadShader(_shader);
        _initialized = false;
    }
}
