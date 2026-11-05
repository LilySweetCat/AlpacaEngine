using System.Numerics;
using System.Text.Json;
using AlpacaEngine;
using AlpacaEngine.Editor;
using AlpacaEngine.GameObjects;
using AlpacaEngine.GameObjects.Components;
using AlpacaEngine.Managers;
using AlpacaEngine.Rendering;
using Raylib_cs;

internal static class LightingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        EditorState.CurrentScene = new Scene { GameObjects = [] };
        EditorState.AddSunLight();
        var sun = EditorState.Selected!;
        var light = (SunLightSourceComponent)sun.Components.Single();
        EditorState.AddSunLight();
        EditorState.AddComponent(sun, new SunLightSourceComponent());
        Check(EditorState.CurrentScene.GameObjects.Count == 1 && sun.Components.Count == 1,
            "Only one sun can be created");
        CommandManager.Undo();
        Check(!EditorState.HasSunLight, "Undo sun creation");
        CommandManager.Redo();
        Check(EditorState.HasSunLight, "Redo sun creation");
        EditorState.DeleteGameObject(sun);
        Check(!EditorState.HasSunLight, "Deleting the sun removes lighting");
        CommandManager.Undo();
        Check(EditorState.HasSunLight, "Undo deletion restores the sun");

        var parent = new GameObject { LocalRotation = new Vector3(0, 90, 0), Child = [sun] };
        EditorState.CurrentScene = new Scene { GameObjects = [parent] };
        sun.LocalRotation = Vector3.Zero;
        Check(Vector3.Distance(SunLightSourceComponent.GetDirection(sun), Vector3.UnitX) < 0.0001f,
            "Parent rotation controls the sun direction");
        parent.LocalScale = new Vector3(2, 4, 3);
        sun.LocalPosition = new Vector3(2, 5, 8);
        Check(Vector3.Distance(SunLightSourceComponent.GetDirection(sun), Vector3.UnitX) < 0.0001f,
            "Translation and scale do not change the sun direction");
        var rows = SceneLighting.GetNormalRows(sun.WorldMatrix);
        var tangent = Vector3.TransformNormal(new Vector3(1, -1, 0), sun.WorldMatrix);
        var normal = Vector3.Normalize(Vector3.One);
        var worldNormal = new Vector3(Vector3.Dot(rows.Row0, normal), Vector3.Dot(rows.Row1, normal),
            Vector3.Dot(rows.Row2, normal));
        Check(MathF.Abs(Vector3.Dot(worldNormal, tangent)) < 0.0001f,
            "Normal stays perpendicular after non-uniform parent scale");

        var folder = Path.Combine(Path.GetTempPath(), "AlpacaLighting-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        var previousDirectory = EditorState.WorkingDirectory;

        try
        {
            EditorState.WorkingDirectory = folder;
            light.Color = new Vector3(0.3f, 0.6f, 0.9f);
            light.Intensity = 2f;
            light.Ambient = 0.35f;
            EditorState.SaveScene();
            var path = EditorState.CurrentScenePath!;
            EditorState.OpenScene(path);
            var loaded = SceneLighting.GetSuns(EditorState.CurrentScene.GameObjects).Single();
            Check(loaded.Light.Color == light.Color && loaded.Light.Intensity == 2f
                && loaded.Light.Ambient == 0.35f && loaded.Owner.Parent != null,
                "Sun settings and hierarchy survive scene round trip");
            var activeScene = EditorState.CurrentScene;
            var invalidPath = Path.Combine(folder, "Duplicate.scene");
            File.WriteAllText(invalidPath,
                "{\"GameObjects\":[{\"Components\":[{\"$type\":\"sunLight\"},{\"$type\":\"sunLight\"}]}]}");

            try
            {
                EditorState.OpenScene(invalidPath);
                throw new Exception("Duplicate sun accepted");
            }
            catch (JsonException) { }

            Check(EditorState.CurrentScene == activeScene, "Invalid scene preserves the active scene");
        }
        finally
        {
            EditorState.WorkingDirectory = previousDirectory;
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }

        RunGpuChecks();
        Console.WriteLine("Sun creation, Undo/Redo, hierarchy, serialization and GPU lighting checks passed.");
    }

    private static unsafe void RunGpuChecks()
    {
        Raylib.SetConfigFlags(ConfigFlags.HiddenWindow);
        Raylib.InitWindow(128, 128, "Lighting checks");
        SceneLighting.Initialize();
        var target = Raylib.LoadRenderTexture(128, 128);
        var meshModel = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1, 1, 1));
        var image = Raylib.GenImageColor(2, 2, new Color(180, 100, 60, 255));
        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        meshModel.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = texture;

        try
        {
            var cube = new GameObject();
            var sun = new GameObject();
            var light = new SunLightSourceComponent { Ambient = 0f };
            sun.Components.Add(light);
            var scene = new Scene { GameObjects = [sun, cube] };
            var camera = new Camera3D { Position = new Vector3(0, 0, 3), Target = Vector3.Zero,
                Up = Vector3.UnitY, FovY = 45, Projection = CameraProjection.Perspective };

            Color Render(bool model = false)
            {
                Raylib.BeginTextureMode(target);
                Raylib.ClearBackground(Color.Magenta);
                Raylib.BeginMode3D(camera);
                SceneLighting.Prepare(scene);
                cube.PushTransform();

                if (model) SceneLighting.DrawModel(meshModel, cube, Color.White);
                else SceneLighting.DrawCube(cube, Color.White);

                cube.PopTransform();
                Raylib.EndMode3D();
                Raylib.EndTextureMode();
                var pixels = Raylib.LoadImageFromTexture(target.Texture);
                var pixel = Raylib.GetImageColor(pixels, 64, 64);
                Raylib.UnloadImage(pixels);
                return pixel;
            }

            Check(Render().R < 5, "A surface facing away from the sun is black without ambient");
            light.Ambient = 0.25f;
            var ambientPixel = Render();
            Check(ambientPixel.R is >= 60 and <= 68, "Ambient lights the unlit side");
            light.Ambient = 0f;
            sun.LocalRotation = new Vector3(0, 180, 0);
            Check(Render().R > 250, "Rotating the sun lights the front face");
            sun.LocalPosition = new Vector3(100, -30, 70);
            sun.LocalScale = new Vector3(3, 5, 7);
            Check(Render().R > 250, "Moving and scaling the sun leaves rendered lighting unchanged");
            cube.LocalRotation = new Vector3(15, 25, 35);
            cube.LocalScale = new Vector3(0.7f, 1.1f, 0.5f);
            var rotatedPixel = Render();
            Check(rotatedPixel.R is > 150 and < 245, "Object rotation changes diffuse lighting");
            cube.LocalRotation = Vector3.Zero;
            cube.LocalScale = Vector3.One;
            light.Color = Vector3.UnitX;
            var red = Render();
            Check(red.R > 250 && red.G < 5 && red.B < 5, "Sun color tints diffuse lighting");
            light.Intensity = 0f;
            Check(Render().R < 5, "Zero intensity preserves ambient-only lighting");
            scene.GameObjects.Remove(sun);
            Check(Render().R > 250, "Removing the sun restores unlit surface color");
            var textured = Render(model: true);
            Check(Math.Abs(textured.R - 180) <= 2 && Math.Abs(textured.G - 100) <= 2
                && Math.Abs(textured.B - 60) <= 2, "Model material texture survives the shared shader");
            scene.GameObjects.Add(sun);
            light.Color = Vector3.One;
            light.Intensity = 0.5f;
            var litTexture = Render(model: true);
            Check(Math.Abs(litTexture.R - 90) <= 2 && Math.Abs(litTexture.G - 50) <= 2,
                "The shared shader lights textured model materials");
            meshModel.Transform = Raymath.MatrixRotateY(MathF.PI / 6f);
            sun.LocalRotation = new Vector3(0, 210, 0);
            light.Intensity = 1f;
            var transformedModel = Render(model: true);
            Check(Math.Abs(transformedModel.R - 180) <= 2 && Math.Abs(transformedModel.G - 100) <= 2,
                "Native model transform and object transform agree in the lighting shader");
            Check(Picking.PickAtMouse(new Ray(new Vector3(100, -30, 75), -Vector3.UnitZ)) == null,
                "Unrelated scene does not pick the marker");
            EditorState.CurrentScene = scene;
            Check(Picking.PickAtMouse(new Ray(new Vector3(100, -30, 75), -Vector3.UnitZ)) == sun,
                "Sun marker is selectable at its world position");
        }
        finally
        {
            Raylib.UnloadModel(meshModel);
            Raylib.UnloadTexture(texture);
            Raylib.UnloadRenderTexture(target);
            SceneLighting.Unload();
            Raylib.CloseWindow();
        }
    }
}
