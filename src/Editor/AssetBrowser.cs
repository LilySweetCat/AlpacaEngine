using System.Numerics;
using System.Text.Json;
using AlpacaEngine.Managers;
using ImGuiNET;
using Raylib_cs;

namespace AlpacaEngine.Editor;

internal sealed class AssetBrowser
{
    private static readonly AssetBrowser Main = new();
    private readonly IReadOnlySet<string>? _extensions;
    private readonly bool _isPicker;
    private string _search = "";
    private string? _searchKey;
    private int _searchRevision = -1;
    private static int _cacheRevision;
    private string[] _searchResults = [];
    private string? _searchWarning;

    internal AssetBrowser(IReadOnlySet<string>? extensions = null)
    {
        _extensions = extensions;
        _isPicker = extensions != null;
    }

    internal void OpenPicker(string currentPath)
    {
        _root = null;
        _search = "";
        _selectedFile = null;
        _initialPath = currentPath;
        ImGui.OpenPopup("Select model");
    }

    private string? _initialPath;

    internal string? DrawPicker()
    {
        ImGui.SetNextWindowSize(new Vector2(850, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal("Select model")) return null;
        string? result = null;
        try
        {
            DrawContents();
            var canSelect = _selectedFile != null && File.Exists(_selectedFile)
                && _extensions!.Contains(Path.GetExtension(_selectedFile));
            ImGui.BeginDisabled(!canSelect);
            if (ImGui.Button("Select"))
            {
                result = Path.GetRelativePath(_root!, _selectedFile!);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
                ImGui.CloseCurrentPopup();
            if (_selectedFile != null) ImGui.TextUnformatted(Path.GetRelativePath(_root!, _selectedFile));
        }
        finally { ImGui.EndPopup(); }
        return result;
    }
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
        ".scene", ".proj",                                // engine files
        ".mtl", ".mat" // materials
    };

    // Keep case-sensitive volumes distinct; Windows paths are case-insensitive.
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // A snapshot of one folder: immediate subfolders, supported files, or an error message.
    private sealed record FolderEntry(string Path, bool IsLink);
    private sealed record Folder(FolderEntry[] Folders, string[] Files, string? Error);

    private static readonly Dictionary<string, Folder> Cache = new(PathComparer);
    private string? _root;
    private string? _selectedFolder;
    private string? _selectedFile;
    private string _sceneName = "New Scene";
    private string? _createSceneFolder;
    private string? _sceneError;
    private bool _openCreateScene;
    private string? _deleteWarning;
    private bool _openDeleteWarning;

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
            Main.DrawContents();
            Main.DrawDeleteWarning();
        }
        finally
        {
            // Keep the ImGui window stack balanced if drawing exits unexpectedly.
            ImGui.End();
        }
    }

    private void DrawContents()
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
            _cacheRevision++;
            if (_initialPath != null)
            {
                try
                {
                    var initial = Path.GetFullPath(_initialPath, root);
                    var relative = Path.GetRelativePath(root, initial);
                    if (!Path.IsPathRooted(relative) && relative != ".."
                        && !relative.StartsWith(".." + Path.DirectorySeparatorChar)
                        && File.Exists(initial) && !HasLinkedParent(initial, root))
                    {
                        SelectFolder(Path.GetDirectoryName(initial)!);
                        _selectedFile = initial;
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException
                                           or UnauthorizedAccessException) { }
                _initialPath = null;
            }
        }

        // Folders are read from disk lazily and cached; "Refresh" re-reads them after external changes.
        if (ImGui.Button("Refresh"))
        {
            Cache.Clear();
            _cacheRevision++;
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(root);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search assets in project...", ref _search, 256);
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
                ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV,
                new Vector2(0, _isPicker ? MathF.Max(100, ImGui.GetContentRegionAvail().Y - 65) : 0))) return;

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
        if (ImGui.BeginChild("FolderFiles"))
        {
            DrawFiles();
            if (!_isPicker)
            {
                DrawContextMenu();
                DrawCreateScenePopup();
            }
            if (_sceneError != null) ImGui.TextWrapped(_sceneError);
        }
        ImGui.EndChild();
        ImGui.EndTable();
    }

    private void SelectFolder(string path)
    {
        _selectedFolder = path;
        // File selection belongs to the current folder, not to the scene object selection.
        _selectedFile = null;
        _sceneError = null;
    }

    private void DrawContextMenu()
    {
        if (!ImGui.BeginPopupContextWindow("AssetContext",
                ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems)) return;
        if (ImGui.MenuItem("New Scene"))
        {
            _createSceneFolder = _selectedFolder;
            _sceneName = "New Scene";
            _sceneError = null;
            _openCreateScene = true;
        }
        ImGui.EndPopup();
    }

    private void DrawCreateScenePopup()
    {
        var focusName = _openCreateScene;
        if (_openCreateScene)
        {
            ImGui.OpenPopup("New Scene");
            _openCreateScene = false;
        }
        if (!ImGui.BeginPopupModal("New Scene", ImGuiWindowFlags.AlwaysAutoResize)) return;
        if (focusName) ImGui.SetKeyboardFocusHere();
        var submitted = ImGui.InputText("Name", ref _sceneName, 256,
            ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll);
        if (ImGui.Button("Create") || submitted)
        {
            try
            {
                _selectedFile = EditorState.CreateScene(_createSceneFolder!, _sceneName);
                Cache.Remove(_createSceneFolder!);
                _cacheRevision++;
                _sceneError = null;
                ImGui.CloseCurrentPopup();
            }
            catch (Exception ex) when (IsSceneFileError(ex))
            {
                _sceneError = $"Cannot create scene: {ex.Message}";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel") || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            _sceneError = null;
            ImGui.CloseCurrentPopup();
        }
        if (_sceneError != null) ImGui.TextWrapped(_sceneError);
        ImGui.EndPopup();
    }

    private static bool IsSceneFileError(Exception ex) => ex is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or JsonException or System.Security.SecurityException;

    private void DrawFileContextMenu(string file)
    {
        if (!ImGui.BeginPopupContextItem("FileContext")) return;
        _selectedFile = file;
        if (ImGui.MenuItem("Delete"))
        {
            try
            {
                EditorState.DeleteAsset(file);
                Cache.Remove(Path.GetDirectoryName(file)!);
                _cacheRevision++;
                _selectedFile = null;
                _sceneError = null;
            }
            catch (Exception ex) when (IsSceneFileError(ex))
            {
                _deleteWarning = $"Cannot delete file: {ex.Message}";
                _openDeleteWarning = true;
            }
        }
        ImGui.EndPopup();
    }

    private void DrawDeleteWarning()
    {
        if (_openDeleteWarning)
        {
            ImGui.OpenPopup("Cannot delete asset");
            _openDeleteWarning = false;
        }
        if (!ImGui.BeginPopupModal("Cannot delete asset", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted(_deleteWarning);
        if (ImGui.Button("OK") || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
        {
            _deleteWarning = null;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
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
                                                && (entry.Attributes & FileAttributes.ReparsePoint) == 0
                                                && (SupportedExtensions.Contains(entry.Extension)
                                                    || ModelManager.SupportedExtensions.Contains(entry.Extension)))
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

    private void DrawFolder(FolderEntry folder, bool isRoot = false)
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

    private void DrawFiles()
    {
        if (_selectedFolder == null) return;

        var searching = !string.IsNullOrWhiteSpace(_search);
        var relative = Path.GetRelativePath(_root!, _selectedFolder);
        ImGui.TextUnformatted(searching ? "Search results (entire project)"
            : relative == "." ? Path.GetFileName(_root) : relative);
        ImGui.Separator();

        var contents = ReadFolder(_selectedFolder);
        if (contents.Error != null)
        {
            ImGui.TextWrapped($"Cannot read folder: {contents.Error}");
            return;
        }
        // Clear stale selection after a file is deleted or moved outside the editor.
        var files = searching ? SearchFiles() : contents.Files.Where(MatchesExtension).ToArray();
        if (_selectedFile != null && !files.Contains(_selectedFile, PathComparer))
            _selectedFile = null;
        if (searching && _searchWarning != null) ImGui.TextWrapped(_searchWarning);

        if (files.Length == 0)
        {
            ImGui.TextDisabled(searching ? "No matching assets found." : "No supported asset files in this folder.");
            return;
        }

        // Choose a column count from the available width, then stretch tiles to fill each row.
        const float tileWidth = 112;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var available = ImGui.GetContentRegionAvail().X;
        var columns = Math.Clamp((int)((available + spacing.X) / (tileWidth + spacing.X)), 1, 64);
        var width = MathF.Max(1, (available - spacing.X * (columns - 1)) / columns);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            // SameLine continues the current row; omitting it starts the next row.
            if (i % columns != 0) ImGui.SameLine();
            ImGui.PushID(file);
            var selected = PathComparer.Equals(file, _selectedFile);
            // A temporary button color highlights the selected file without affecting other tiles.
            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
            if (ImGui.Button("##file", new Vector2(width, 88))) _selectedFile = file;
            if (selected) ImGui.PopStyleColor();
            if (!_isPicker && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)
                && string.Equals(Path.GetExtension(file), ".scene", StringComparison.OrdinalIgnoreCase))
            {
                _selectedFile = file;
                try
                {
                    EditorState.OpenScene(file);
                    _sceneError = null;
                }
                catch (Exception ex) when (IsSceneFileError(ex))
                {
                    _sceneError = $"Cannot open scene: {ex.Message}";
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(file);

            DrawFileTile(file, width);
            if (!_isPicker) DrawFileContextMenu(file);
            ImGui.PopID();
        }
    }

    private bool MatchesExtension(string file) => _extensions == null || _extensions.Contains(Path.GetExtension(file));

    private string[] SearchFiles()
    {
        var query = _search.Trim();
        if (_searchKey == query && _searchRevision == _cacheRevision) return _searchResults;
        var results = new List<string>();
        _searchWarning = null;
        var pending = new Stack<string>();
        pending.Push(_root!);
        while (pending.TryPop(out var folder))
        {
            var contents = ReadFolder(folder);
            if (contents.Error != null) _searchWarning = "Some folders could not be read. Use Refresh to retry.";
            results.AddRange(contents.Files.Where(file => MatchesExtension(file)
                && Path.GetRelativePath(_root!, file).Contains(query, StringComparison.OrdinalIgnoreCase)));
            foreach (var child in contents.Folders)
                if (!child.IsLink) pending.Push(child.Path);
        }
        _searchKey = query;
        _searchRevision = _cacheRevision;
        return _searchResults = results.OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool HasLinkedParent(string file, string root)
    {
        for (var directory = Path.GetDirectoryName(file); directory != null && !PathComparer.Equals(directory, root);
             directory = Path.GetDirectoryName(directory))
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return true;
        return (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0;
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
