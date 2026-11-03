using UnityEditor;
public static class GameplaySceneAuthoring
{
    [MenuItem("Tools/MergeTD/Build Playable Progression UI")]
    public static void Build() => LandscapeSceneAuthoring.Build();
}
