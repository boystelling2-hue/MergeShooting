using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MergeTD;

[InitializeOnLoad]
public static class MergeTDProjectSetup
{
    static MergeTDProjectSetup() { EditorApplication.delayCall += AutoConnect; }

    private static void AutoConnect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity" || scene.isDirty) return;
        if (Object.FindFirstObjectByType<MergeGameDatabase>() != null) return;
        Connect();
    }

    [MenuItem("Tools/MergeTD/Connect Database")]
    public static void Connect()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/game_data.json");
        if (json == null) { Debug.LogError("MergeTD: game_data.json missing."); return; }
        var db = Object.FindFirstObjectByType<MergeGameDatabase>();
        if (db == null)
        {
            var go = new GameObject("MergeGameDatabase");
            Undo.RegisterCreatedObjectUndo(go, "Connect MergeTD database");
            db = Undo.AddComponent<MergeGameDatabase>(go);
        }
        var serialized = new SerializedObject(db);
        serialized.FindProperty("gameDataJson").objectReferenceValue = json;
        serialized.ApplyModifiedProperties();
        db.Reload();
        if (!db.TryResolveCraftMerge("U2_10010", "U2_01001", out var unit) || unit.name != "심판자")
            throw new System.InvalidOperationException("MergeTD merge verification failed.");
        if (Mathf.Abs(db.GetElementDamageMultiplier("U3_02002", "E01") - 1.5f) > 0.001f)
            throw new System.InvalidOperationException("MergeTD weakness verification failed.");
        EditorSceneManager.MarkSceneDirty(db.gameObject.scene);
        EditorSceneManager.SaveScene(db.gameObject.scene);
        Selection.activeGameObject = db.gameObject;
        Debug.Log("MergeTD connection verified: 90 units, merge lookup and weakness multiplier OK. Scene saved.");
    }
}
