using AlpacaEngine;
using AlpacaEngine.Editor;

internal static class AssetFolderChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        throw new Exception("An invalid folder operation was accepted.");
    }

    public static void Run()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "AlpacaFolderTests-" + Guid.NewGuid());
        var root = Path.Combine(sandbox, "Project");
        var outside = Path.Combine(sandbox, "ProjectOther");
        var previousRoot = EditorState.WorkingDirectory;
        var previousScene = EditorState.CurrentScene;
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            EditorState.WorkingDirectory = root;
            var folder = EditorState.CreateAssetFolder(root, "  Новая папка  ");
            Check(Directory.Exists(folder) && Path.GetFileName(folder) == "Новая папка",
                "Create a folder with a trimmed Unicode name");

            foreach (var name in new[] { "", " ", ".", "..", "../Outside", "nested/name", "nested\\name", "trailing." })
                Reject(() => EditorState.CreateAssetFolder(root, name));

            Reject(() => EditorState.CreateAssetFolder(root, "Новая папка"));
            File.WriteAllText(Path.Combine(root, "File"), "keep");
            Reject(() => EditorState.CreateAssetFolder(root, "File"));
            Reject(() => EditorState.CreateAssetFolder(outside, "Outside"));
            Reject(() => EditorState.DeleteAssetFolder(root));
            Reject(() => EditorState.DeleteAssetFolder(outside));
            Reject(() => EditorState.DeleteAssetFolder(Path.Combine(root, "..")));

            var nested = EditorState.CreateAssetFolder(folder, "Nested");
            File.WriteAllText(Path.Combine(nested, "unsupported.txt"), "contents");
            var scene = EditorState.CreateScene(nested, "Active");
            EditorState.OpenScene(scene);
            EditorState.DeleteAssetFolder(folder);
            Check(!Directory.Exists(folder), "Delete includes all nested files, even unsupported assets");
            Check(EditorState.CurrentScenePath == null, "Deleting the active scene's folder clears its save path");
            Check(Directory.Exists(outside) && File.Exists(Path.Combine(root, "File")),
                "Unrelated folders and files survive recursive deletion");

            var activeScene = EditorState.CurrentScene;
            var empty = EditorState.CreateAssetFolder(root, "Empty");
            EditorState.DeleteAssetFolder(empty);
            Check(!Directory.Exists(empty) && ReferenceEquals(activeScene, EditorState.CurrentScene),
                "Deleting an empty unrelated folder preserves the scene");

            var link = Path.Combine(root, "Link");

            try
            {
                Directory.CreateSymbolicLink(link, outside);
                Reject(() => EditorState.CreateAssetFolder(link, "Escape"));
                Reject(() => EditorState.DeleteAssetFolder(link));
                var container = EditorState.CreateAssetFolder(root, "Container");
                Directory.CreateSymbolicLink(Path.Combine(container, "Link"), outside);
                File.WriteAllText(Path.Combine(outside, "Keep.txt"), "keep");
                EditorState.DeleteAssetFolder(container);
                Check(File.Exists(Path.Combine(outside, "Keep.txt")), "Recursive deletion does not follow links");
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) == 1314)
            {
                Console.WriteLine("Skipping folder link checks: symbolic-link privilege is unavailable.");
            }
            finally
            {
                if (Directory.Exists(link)) Directory.Delete(link);
            }
        }
        finally
        {
            EditorState.WorkingDirectory = previousRoot;
            EditorState.CurrentScene = previousScene;
            Directory.Delete(sandbox, recursive: true);
        }

        Console.WriteLine("Asset folder checks passed.");
    }
}
