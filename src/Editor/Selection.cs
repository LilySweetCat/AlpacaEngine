using AlpacaEngine.GameObjects;

namespace AlpacaEngine.Editor;

public abstract record Selection;

public sealed record GameObjectSelection(IReadOnlyList<GameObject> Objects) : Selection;

public sealed record SceneSettingsSelection : Selection;
