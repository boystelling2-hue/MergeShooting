using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using MergeTD;

[InitializeOnLoad]
public static class MergeTDV2Verification
{
    static MergeTDV2Verification() { EditorApplication.delayCall += Run; EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run; }; }

    [MenuItem("Tools/MergeTD/Verify V2 Database")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/game_data.json");
        if (asset == null) return;
        var go = new GameObject("MergeTD verification (temporary)");
        go.SetActive(false);
        try
        {
            var db = go.AddComponent<MergeGameDatabase>();
            db.LoadJson(asset.text);
            Check(db.UnitCount == 90, "unit count");
            Check(db.TryResolveCraftMerge("U2_10010", "U2_01001", out var result) && result.name == "심판자", "craft merge");
            Check(db.GetElementDamageMultiplier("U3_02002", "E01") == 1.5f, "weakness");
            Check(db.GetElementDamageMultiplier("U1_10000", "E01") == 0.5f, "resistance");
            Check(db.GetElementDamageMultiplier("U1_10000", "E02") == 1f, "neutral");
            string[] deck = { "U1_10000", "U1_01000", "U1_00100", "U1_00010", "U1_00001" };
            Check(db.IsValidDeck(deck), "five-type deck");
            Check(db.IsValidDeck(new[] { deck[0] }), "one-type deck");
            Check(!db.IsValidDeck(new string[0]), "empty deck rejected");
            Check(!db.IsValidDeck(new[] { deck[0], deck[0] }), "duplicate rejected");
            Check(!db.IsValidDeck(new[] { deck[0], deck[1], deck[2], deck[3], deck[4], "U2_20000" }), "six types rejected");
            Check(!db.IsValidDeck(new[] { "missing" }), "unknown unit rejected");
            var one = db.GetThemeCounts(new[] { deck[0] });
            var two = db.GetThemeCounts(new[] { deck[0], deck[0] });
            foreach (var pair in one) Check(two[pair.Key] == pair.Value, "synergy deduplication");
            bool rejected = false;
            try { db.GetBattleSynergies(deck, deck); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "pending synergy rule blocked");
            var modified = JsonUtility.FromJson<MergeGameData>(asset.text);
            modified.monsters[0].resistElements = modified.monsters[0].weakElements;
            rejected = false;
            try { db.LoadJson(JsonUtility.ToJson(modified)); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && db.UnitCount == 90 && db.GetElementDamageMultiplier("U1_10000", "E01") == 0.5f, "invalid reload preserves valid data");
            modified = JsonUtility.FromJson<MergeGameData>(asset.text);
            modified.rules.synergySource = "deck";
            db.LoadJson(JsonUtility.ToJson(modified));
            Check(db.GetBattleSynergies(deck, new string[0]).Count == db.GetActiveSynergies(deck).Count, "deck synergy source");
            modified.rules.synergySource = "deployed";
            db.LoadJson(JsonUtility.ToJson(modified));
            Check(db.GetBattleSynergies(deck, new string[0]).Count == 0, "deployed synergy source");
            db.LoadJson(asset.text);
            File.WriteAllText("Library/MergeTDV2Verification.txt", "PASS: v2 load, 90 units, craft merge, weakness/neutral/resistance, deck boundaries, synergy dedup/source, invalid reload rollback.");
            Debug.Log("MergeTD v2 verification PASS");
        }
        catch (Exception ex)
        {
            File.WriteAllText("Library/MergeTDV2Verification.txt", "FAIL: " + ex);
            Debug.LogException(ex);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void Check(bool ok, string label)
    { if (!ok) throw new InvalidOperationException("MergeTD v2 verification failed: " + label); }
}
