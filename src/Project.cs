using System.Text.Json;

namespace AlpacaEngine;

[Serializable]
public class Project
{
    public static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true, // GameObject хранит данные в полях, а не свойствах
    };
    
    public string Name = "New Project";

    public List<Scene> Scenes;

    public string StartSceneId;
}