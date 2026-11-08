using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MergeTD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Compatibility entry points retained for the existing build/verification commands.
[InitializeOnLoad]
public static class DefenseBtdVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static DefenseBtdVerification()
    { if (SessionState.GetBool("BtdPlaySmoke", false)) EditorApplication.update += PlayTick; }
    public static void BuildAndPlayVerify()
    { BuildAndVerify(); SessionState.SetBool("BtdPlaySmoke", true); EditorApplication.update += PlayTick; EditorApplication.isPlaying = true; }
    private static void PlayTick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        var main = UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();
        if (main == null || typeof(MainCraftingScreen).GetField("inventory", Private).GetValue(main) == null) return;
        EditorApplication.update -= PlayTick; SessionState.SetBool("BtdPlaySmoke", false);
        try
        {
            BattleWorldVerification.VerifyRuntime();
            File.WriteAllText("Library/DefenseBtdPlayVerification.txt", File.ReadAllText("Library/BattleWorldVerification.txt"));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText("Library/DefenseBtdPlayVerification.txt", "FAIL: " + e); Debug.LogException(e);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
    public static void BuildAndVerify()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity"); LandscapeSceneAuthoring.Build();
        PlayableProgressionVerification.Run(); InventoryPlacementVerification.Run(); CraftInventoryVerification.Run();
        Verify(); VerifyArt();
        File.WriteAllText("Library/DefenseBtdVerification.txt", "PASS: world scene, BTD targeting/upgrade rules, road alignment, inventory and progression. Run Verify World Runtime in Play for input, motion and HUD checks.");
    }
    [MenuItem("Tools/MergeTD/Verify BTD Defense UI")]
    public static void Verify()
    {
        BattleWorldVerification.VerifyScene();
        var db = UnityEngine.Object.FindFirstObjectByType<MergeGameDatabase>(); db.Reload();
        var content = new GameContent(); content.Rules["initialBattleCash"] = 5000;
        var battle = new DefenseBattle(db, content); var unit = db.GetAllUnits().First(u => u.tier == 1);
        Require(battle.Deploy(unit.unitId, 5, new Vector2(6, 3), out _), "Deployment");
        var tower = battle.Towers[0];
        battle.Enemies.Add(new DefenseBattle.Enemy { serial = 1, hp = 20, maxHp = 20, progress = 4, position = new Vector2(6, 2) });
        battle.Enemies.Add(new DefenseBattle.Enemy { serial = 2, hp = 5, maxHp = 20, progress = 10, position = new Vector2(7, 2) });
        battle.Enemies.Add(new DefenseBattle.Enemy { serial = 3, hp = 100, maxHp = 100, progress = 2, position = new Vector2(8, 2) });
        var select = typeof(DefenseBattle).GetMethod("Select", Private);
        foreach (var pair in new[] { new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 1 } })
        { tower.targetMode = pair[0]; Require(((DefenseBattle.Enemy)select.Invoke(battle, new object[] { tower })).serial == pair[1], "Target priority " + pair[0]); }
        for (int i = 0; i < 5; i++) Require(battle.Upgrade(tower, 0, out _), "Main path");
        for (int i = 0; i < 2; i++) Require(battle.Upgrade(tower, 1, out _), "Secondary path");
        Require(!battle.Upgrade(tower, 2, out _), "Third path must lock");
        Debug.Log("BTD world scene and targeting rules PASS");
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void VerifyArt()
    {
        var db = UnityEngine.Object.FindFirstObjectByType<MergeGameDatabase>(); db.Reload();
        var battle = new DefenseBattle(db, new GameContent());
        var route = new Texture2D(2, 2); route.LoadImage(File.ReadAllBytes(DefenseArtAssets.DirectoryPath + "MeadowRoute-v1.png"));
        try
        {
            for (int y = 0; y < DefenseBattle.Rows; y++) for (int x = 0; x < DefenseBattle.Columns; x++)
                Require((route.GetPixel(x * 60 + 30, 660 - y * 60 - 30).a > .9f) == battle.IsPath(x, y), "Route mismatch " + x + "," + y);
        }
        finally { UnityEngine.Object.DestroyImmediate(route); }
        foreach (string name in new[] { "AmberSlime-v1", "CyanRunner-v1", "AmethystGolem-v1" })
            Require(AssetDatabase.LoadAssetAtPath<Sprite>(DefenseArtAssets.DirectoryPath + name + ".png") != null, "Enemy sprite " + name);
    }
    public static void VerifyMotion()
    {
        var db = UnityEngine.Object.FindFirstObjectByType<MergeGameDatabase>(); db.Reload();
        var original = UnityEngine.Object.FindObjectsByType<BattleWorldView>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        // The disposable clone keeps edit-mode verification from changing the authored actor transforms.
        var clone = UnityEngine.Object.Instantiate(original.gameObject); clone.SetActive(false);
        var world = clone.GetComponent<BattleWorldView>();
        try
        {
            var battle = new DefenseBattle(db, new GameContent());
            var enemy = new DefenseBattle.Enemy { serial = 1, position = new Vector2(4, 2), progress = 4.5f, speed = 1, hp = 100, maxHp = 100 };
            battle.Enemies.Add(enemy); battle.Start(); int checks = 0;
            foreach (int fps in new[] { 30, 60, 120 }) foreach (float speed in new[] { 1f, 2f })
                for (int frame = 0; frame < 12; frame++)
                {
                    battle.Tick(speed / fps); world.DrawActors(battle);
                    Require(Vector3.Distance(world.EnemyViews[0].transform.localPosition, BattleWorldView.Point(enemy.position)) < .0001f, "World enemy motion"); checks++;
                }
            File.WriteAllText("Library/EnemyMotionVerification.txt", "PASS: " + checks + " world SpriteRenderer position checks at 30/60/120 FPS and 1x/2x. Play verification separately covers pause and input.");
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }
}
