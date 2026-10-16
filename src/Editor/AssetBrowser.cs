using System.Numerics;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

internal static class AssetBrowser
{
    private static Texture2D _folderIcon;
    private static Texture2D _sceneIcon;
    private static Texture2D _projectIcon;

    internal static void LoadIcons()
    {
        _folderIcon = LoadIcon("folder");
        _sceneIcon = LoadIcon("scene");
        _projectIcon = LoadIcon("project");
    }

    private static Texture2D LoadIcon(string name)
    {
        var texture = Raylib.LoadTexture(Path.Combine(AppContext.BaseDirectory, "assets", "icons", $"{name}.png"));
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        return texture;
    }

    internal static void UnloadIcons()
    {
        Raylib.UnloadTexture(_folderIcon);
        Raylib.UnloadTexture(_sceneIcon);
        Raylib.UnloadTexture(_projectIcon);
    }

    // Asset files shown in the browser. Extend as the engine learns to load more formats.
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".gif", // images
        ".scene", ".proj"                                // engine files
    };

    // Windows and macOS compare paths case-insensitively; Linux does not.
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // A snapshot of one folder: immediate subfolders, supported files, or an error message.
    private sealed record FolderEntry(string Path, bool IsLink);
    private sealed record Folder(FolderEntry[] Folders, string[] Files, string? Error);

    private static readonly Dictionary<string, Folder> Cache = new(PathComparer);
    private static string? _root;
    private static string? _selectedFolder;
    private static string? _selectedFile;

    public static void Render()
    {
        // ImGui requires End even when Begin returns false (for example, for a collapsed window).
        if (!ImGui.Begin("Assets"))
        {
            ImGui.End();
            return;
        }

        try
        {
            DrawContents();
        }
        finally
        {
            // Keep the ImGui window stack balanced if drawing exits unexpectedly.
            ImGui.End();
        }
    }

    private static void DrawContents()
    {
        string root;
        try
        {
            if (string.IsNullOrWhiteSpace(EditorState.WorkingDirectory))
            {
                ImGui.TextWrapped("Working directory is not set.");
                return;
            }
            // Normalize relative paths and trailing separators before detecting root changes.
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(EditorState.WorkingDirectory));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                   or UnauthorizedAccessException)
        {
            ImGui.TextWrapped($"Invalid working directory: {ex.Message}");
            return;
        }

        // Switching projects resets the selection and discards snapshots from the previous root.
        if (!PathComparer.Equals(root, _root))
        {
            _root = root;
            SelectFolder(root);
            Cache.Clear();
        }

        // Folders are read from disk lazily and cached; "Refresh" re-reads them after external changes.
        if (ImGui.Button("Refresh")) Cache.Clear();
        ImGui.SameLine();
        ImGui.TextUnformatted(root);
        ImGui.Separator();

        if (!Directory.Exists(root))
        {
            ImGui.TextWrapped("Working directory does not exist or is not accessible.");
            return;
        }
        // Recover if the selected folder was removed outside the editor.
        if (_selectedFolder == null || !Directory.Exists(_selectedFolder)) SelectFolder(root);

        // A resizable two-column table acts as the splitter. Child windows scroll independently.
        if (!ImGui.BeginTable("AssetBrowserLayout", 2,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;

        ImGui.TableSetupColumn("Folders", ImGuiTableColumnFlags.WidthFixed, 220);
        ImGui.TableSetupColumn("Files", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.BeginChild("FolderTree", Vector2.Zero, ImGuiChildFlags.None,
                ImGuiWindowFlags.HorizontalScrollbar))
        {
            DrawFolder(new FolderEntry(root, false), isRoot: true);
        }
        ImGui.EndChild();

        ImGui.TableSetColumnIndex(1);
        if (ImGui.BeginChild("FolderFiles")) DrawFiles();
        ImGui.EndChild();
        ImGui.EndTable();
    }

    private static void SelectFolder(string path)
    {
        _selectedFolder = path;
        // File selection belongs to the current folder, not to the scene object selection.
        _selectedFile = null;
    }

    private static Folder ReadFolder(string path)
    {
        if (Cache.TryGetValue(path, out var contents)) return contents;
        try
        {
            // Enumerate once, then split and sort folders and files for a stable display order.
            var entries = new DirectoryInfo(path).GetFileSystemInfos();
            var folders = entries.Where(entry => (entry.Attributes & FileAttributes.Directory) != 0)
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new FolderEntry(entry.FullName,
                    (entry.Attributes & FileAttributes.ReparsePoint) != 0)).ToArray();
            // Filter files only: all folders remain available for tree navigation.
            var files = entries.Where(entry => (entry.Attributes & FileAttributes.Directory) == 0
                                                && SupportedExtensions.Contains(entry.Extension))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.FullName).ToArray();
            contents = new Folder(folders, files, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or System.Security.SecurityException)
        {
            // Cache failures too, so inaccessible folders are retried on refresh, not every frame.
            contents = new Folder([], [], ex.Message);
        }
        Cache[path] = contents;
        return contents;
    }

    private static void DrawFolder(FolderEntry folder, bool isRoot = false)
    {
        // Do not follow links/junctions: they may leave the project or form cycles.
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (isRoot) flags |= ImGuiTreeNodeFlags.DefaultOpen;
        if (folder.IsLink) flags |= ImGuiTreeNodeFlags.Leaf;
        if (PathComparer.Equals(folder.Path, _selectedFolder)) flags |= ImGuiTreeNodeFlags.Selected;

        // Full paths distinguish identically named folders; draw the icon and label manually.
        ImGui.PushID(folder.Path);
        var open = ImGui.TreeNodeEx("##folder", flags);
        // Clicking the arrow only expands/collapses; clicking the label selects the folder.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !ImGui.IsItemToggledOpen() && !folder.IsLink)
            SelectFolder(folder.Path);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(folder.IsLink ? $"{folder.Path}\nSymbolic links are not followed." : folder.Path);

        var name = Path.GetFileName(folder.Path);
        var iconMin = ImGui.GetItemRectMin() + new Vector2(ImGui.GetTreeNodeToLabelSpacing(), 0);
        var iconSize = ImGui.GetFontSize();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddImage((nint)_folderIcon.Id, iconMin, iconMin + new Vector2(iconSize));
        drawList.AddText(iconMin + new Vector2(iconSize + ImGui.GetStyle().ItemInnerSpacing.X, 0),
            ImGui.GetColorU32(ImGuiCol.Text), name.Length == 0 ? folder.Path : name);
        if (open)
        {
            // Recurse only into expanded nodes, leaving collapsed subtrees unread and undrawn.
            if (!folder.IsLink)
            {
                var contents = ReadFolder(folder.Path);
                if (contents.Error != null) ImGui.TextWrapped(contents.Error);
                foreach (var child in contents.Folders) DrawFolder(child);
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    private static void DrawFiles()
    {
        if (_selectedFolder == null) return;
        var relative = Path.GetRelativePath(_root!, _selectedFolder);
        ImGui.TextUnformatted(relative == "." ? Path.GetFileName(_root) : relative);
        ImGui.Separator();

        var contents = ReadFolder(_selectedFolder);
        if (contents.Error != null)
        {
            ImGui.TextWrapped($"Cannot read folder: {contents.Error}");
            return;
        }
        // Clear stale selection after a file is deleted or moved outside the editor.
        if (_selectedFile != null && !contents.Files.Contains(_selectedFile, PathComparer))
            _selectedFile = null;
        if (contents.Files.Length == 0)
        {
            ImGui.TextDisabled("No supported asset files in this folder.");
            return;
        }

        // Choose a column count from the available width, then stretch tiles to fill each row.
        const float tileWidth = 112;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var available = ImGui.GetContentRegionAvail().X;
        var columns = Math.Clamp((int)((available + spacing.X) / (tileWidth + spacing.X)), 1, 64);
        var width = MathF.Max(1, (available - spacing.X * (columns - 1)) / columns);
        for (var i = 0; i < contents.Files.Length; i++)
        {
            var file = contents.Files[i];
            // SameLine continues the current row; omitting it starts the next row.
            if (i % columns != 0) ImGui.SameLine();
            ImGui.PushID(file);
            var selected = PathComparer.Equals(file, _selectedFile);
            // A temporary button color highlights the selected file without affecting other tiles.
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            if (ImGui.Button("##file", new Vector2(width, 88))) _selectedFile = file;
            if (selected) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(file);

            DrawFileTile(file, width);
            ImGui.PopID();
        }
    }

    // The button handles interaction; its icon and filename are drawn manually
    // inside its bounds so long labels cannot spill into neighboring tiles.
    private static void DrawFileTile(string file, float width)
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(min + new Vector2(4), max - new Vector2(4), true);
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);
        var icon = Path.GetExtension(file).ToLowerInvariant() switch
        {
            ".scene" => _sceneIcon,
            ".proj" => _projectIcon,
            _ => default
        };
        if (icon.Id != 0)
        {
            var iconMin = min + new Vector2((width - 40) / 2, 8);
            drawList.AddImage((nint)icon.Id, iconMin, iconMin + new Vector2(40));
        }
        else
        {
            var extension = Path.GetExtension(file).TrimStart('.').ToUpperInvariant();
            if (extension.Length == 0) extension = "FILE";
            drawList.AddText(min + new Vector2(8, 14), textColor, extension);
        }
        drawList.AddText(min + new Vector2(8, 58), textColor, FitFileName(Path.GetFileName(file), width - 16));
        drawList.PopClipRect();
    }

    private static string FitFileName(string name, float width)
    {
        // Measure actual font width rather than assuming a fixed number of characters fits.
        if (ImGui.CalcTextSize(name).X <= width) return name;
        var length = name.Length;
        while (length > 0 && ImGui.CalcTextSize(name[..length] + "...").X > width) length--;
        return name[..length] + "...";
    }
}
