using System;
using System.IO;
using System.Linq;
using MergeTD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Edit-time migration and authoring. Re-running replaces only battle visuals, never inventory or HUD.
public static class BattleWorldAuthoring
{
    private const string Folder = "Assets/Prefabs/DefenseWorld";
    private static Material spriteMaterial;
    private static Sprite square;
    private static int layer;
    private static SpriteRenderer Sprite(string name, Transform parent, Sprite sprite, Vector2 size, int order, Color color)
    {
        var go = new GameObject(name, typeof(SpriteRenderer)); go.layer = layer; go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = spriteMaterial;
        renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
        BattleWorldView.Size(renderer, size); return renderer;
    }
    private static void Assets()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        string path = Folder + "/WorldSquare.png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            t.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        square = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        path = Folder + "/WorldSprites.mat"; spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (spriteMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Sprite shader missing");
            spriteMaterial = new Material(shader); AssetDatabase.CreateAsset(spriteMaterial, path);
        }
        layer = LayerMask.NameToLayer("MergeBattleWorld");
        if (layer < 0)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layers.GetArrayElementAtIndex(i).stringValue = "MergeBattleWorld"; layer = i; break; }
            if (layer < 0) throw new InvalidOperationException("No free world rendering layer");
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    private static WorldActorView ActorPrefab(bool tower)
    {
        var go = new GameObject(tower ? "Tower Actor" : "Enemy Actor", typeof(WorldActorView)); go.layer = layer;
        var view = go.GetComponent<WorldActorView>();
        view.icon = Sprite("Body", go.transform, tower ? HighResolutionRoleIcons.Load(4) : DefenseArtAssets.Import("AmberSlime-v1"), Vector2.one * .66f, 100, Color.white);
        view.healthTrack = Sprite("Health Track", go.transform, square, new Vector2(.7f, .075f), 102, new Color(.04f, .07f, .06f));
        view.healthTrack.transform.localPosition = new Vector3(0, -.44f, 0);
        view.healthFill = Sprite("Health Fill", go.transform, square, new Vector2(.66f, .045f), 103, new Color(.5f, .9f, .35f));
        view.healthFill.transform.localPosition = new Vector3(0, -.44f, 0);
        view.levelBadge = Sprite("Level Backplate", go.transform, square, new Vector2(.34f, .3f), 102, new Color(.035f, .065f, .07f, .9f));
        view.levelBadge.transform.localPosition = new Vector3(.3f, .27f, 0);
        var label = new GameObject(tower ? "Level" : "Status", typeof(TextMesh)); label.layer = layer; label.transform.SetParent(go.transform, false);
        label.transform.localPosition = tower ? new Vector3(.3f, .27f, 0) : new Vector3(0, .62f, 0);
        view.caption = label.GetComponent<TextMesh>(); view.caption.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Workshop/WorkshopKorean.ttf");
        view.caption.fontSize = 48; view.caption.characterSize = .065f; view.caption.anchor = TextAnchor.MiddleCenter;
        view.caption.alignment = TextAlignment.Center; view.caption.text = tower ? "1" : "BOSS";
        var mesh = label.GetComponent<MeshRenderer>(); mesh.sharedMaterial = view.caption.font.material; mesh.sortingOrder = 104;
        view.Health(1, false); view.levelBadge.gameObject.SetActive(tower);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + (tower ? "/TowerActor.prefab" : "/EnemyActor.prefab"));
        UnityEngine.Object.DestroyImmediate(go); return prefab.GetComponent<WorldActorView>();
    }
    private static void RouteMarker(Transform parent, string name, string text, Vector2 position)
    {
        var plate = Sprite(name, parent, square, new Vector2(1.35f, .36f), -10, new Color(.04f, .08f, .07f, .85f));
        plate.transform.localPosition = position;
        var go = new GameObject("Label", typeof(TextMesh)); go.layer = layer; go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var label = go.GetComponent<TextMesh>(); label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Workshop/WorkshopKorean.ttf");
        label.fontSize = 48; label.characterSize = .06f; label.anchor = TextAnchor.MiddleCenter; label.text = text;
        var mesh = go.GetComponent<MeshRenderer>(); mesh.sharedMaterial = label.font.material; mesh.sortingOrder = -9;
    }
    public static BattleWorldView Build(BattleScreen screen, RectTransform viewport, MergeGameDatabase db)
    {
        Assets();
        foreach (var old in UnityEngine.Object.FindObjectsByType<BattleWorldView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (old.gameObject.scene == screen.gameObject.scene) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Battle World", typeof(BattleWorldView)); root.layer = layer;
        Undo.RegisterCreatedObjectUndo(root, "Author battle world"); var world = root.GetComponent<BattleWorldView>();
        var cameraObject = new GameObject("Battle Camera", typeof(Camera)); cameraObject.layer = layer; cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.5f;
        camera.transform.localPosition = new Vector3(8.5f, -5, -20); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.045f, .065f, .075f); camera.cullingMask = 1 << layer;
        camera.depth = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 50; camera.allowHDR = false;
        foreach (var other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (other != camera && other.gameObject.scene == screen.gameObject.scene)
            { other.cullingMask &= ~(1 << layer); other.backgroundColor = camera.backgroundColor; }
        var terrain = Sprite("Meadow", root.transform, DefenseArtAssets.Import("Meadow-v1"), new Vector2(18, 11), -100, Color.white);
        terrain.transform.localPosition = new Vector3(8.5f, -5, 0);
        var route = Sprite("Enemy Route", root.transform, DefenseArtAssets.Road(new DefenseBattle(db, new GameContent())), new Vector2(18, 11), -90, Color.white);
        route.transform.localPosition = terrain.transform.localPosition;
        RouteMarker(root.transform, "Entrance Marker", "입구 →", new Vector2(.5f, -1.35f));
        RouteMarker(root.transform, "Exit Marker", "출구", new Vector2(3, -4.4f));
        var attack = Sprite("Attack Range", root.transform, DefenseVisualAssets.Sprite("RangeThin"), Vector2.one, -20, Color.yellow);
        var support = Sprite("Support Range", root.transform, DefenseVisualAssets.Sprite("RangeThin"), Vector2.one, -19, Color.cyan);
        var placement = Sprite("Cell Highlight", root.transform, square, Vector2.one, -30, Color.green);
        var ghost = Sprite("Placement Ghost", root.transform, HighResolutionRoleIcons.Load(4), Vector2.one * .66f, 300, Color.white);
        var towerRoot = new GameObject("Towers"); towerRoot.layer = layer; towerRoot.transform.SetParent(root.transform, false);
        var towerPrefab = ActorPrefab(true); var enemyPrefab = ActorPrefab(false);
        var towers = new System.Collections.Generic.List<WorldActorView>();
        for (int y = 0; y < DefenseBattle.Rows; y++) for (int x = 0; x < DefenseBattle.Columns; x++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(towerPrefab.gameObject, towerRoot.transform);
            actor.name = "Tower " + x + "," + y; actor.transform.localPosition = new Vector3(x, -y, 0);
            towers.Add(actor.GetComponent<WorldActorView>()); actor.SetActive(false);
        }
        var enemies = new System.Collections.Generic.List<WorldActorView>();
        var actors = new GameObject("Moving Actors"); actors.layer = layer; actors.transform.SetParent(root.transform, false);
        for (int i = 0; i < 32; i++)
        {
            var actor = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab.gameObject, actors.transform);
            actor.name = "Enemy " + i; enemies.Add(actor.GetComponent<WorldActorView>()); actor.SetActive(false);
        }
        var shots = new System.Collections.Generic.List<SpriteRenderer>(); var traces = new System.Collections.Generic.List<SpriteRenderer>();
        for (int i = 0; i < 96; i++) shots.Add(Sprite("Projectile " + i, actors.transform, DefenseVisualAssets.Sprite("Orb"), Vector2.one * .1f, 350, Color.white));
        for (int i = 0; i < 64; i++) traces.Add(Sprite("Attack Trace " + i, actors.transform, square, Vector2.one, 340, Color.white));
        var so = new SerializedObject(world);
        LandscapeSceneAuthoring.Bind(so, "worldCamera", camera); LandscapeSceneAuthoring.Bind(so, "viewport", viewport); LandscapeSceneAuthoring.Bind(so, "screen", screen);
        LandscapeSceneAuthoring.Bind(so, "terrain", terrain); LandscapeSceneAuthoring.Bind(so, "route", route);
        LandscapeSceneAuthoring.Bind(so, "attackRange", attack); LandscapeSceneAuthoring.Bind(so, "supportRange", support);
        LandscapeSceneAuthoring.Bind(so, "placement", placement); LandscapeSceneAuthoring.Bind(so, "previewIcon", ghost);
        LandscapeSceneAuthoring.ArrayBind(so, "towers", towers); LandscapeSceneAuthoring.ArrayBind(so, "enemies", enemies);
        LandscapeSceneAuthoring.ArrayBind(so, "projectiles", shots); LandscapeSceneAuthoring.ArrayBind(so, "traces", traces);
        LandscapeSceneAuthoring.ArrayBind(so, "enemySprites", new System.Collections.Generic.List<Sprite> { DefenseArtAssets.Import("AmberSlime-v1"), DefenseArtAssets.Import("CyanRunner-v1"), DefenseArtAssets.Import("AmethystGolem-v1") });
        so.ApplyModifiedPropertiesWithoutUndo(); world.ResetViews(); root.SetActive(false); return world;
    }
    public static void PrepareViewport(BattleScreen screen, RectTransform board)
    {
        var image = board.GetComponent<Image>(); image.sprite = null; image.color = Color.clear; image.raycastTarget = false;
        var mask = board.GetComponent<RectMask2D>(); if (mask != null) UnityEngine.Object.DestroyImmediate(mask);
        foreach (var item in screen.GetComponentsInChildren<Image>(true))
        {
            if (item.name == "Letterbox" || item.name == "Background") { item.color = Color.clear; item.raycastTarget = false; }
            if (item.name == "Header") item.rectTransform.sizeDelta = new Vector2(1280, 54);
            if (item.name == "Header Divider") item.rectTransform.anchoredPosition = new Vector2(0, -53);
        }
    }
    [MenuItem("Tools/MergeTD/Migrate Battle To World")]
    public static void Migrate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before migration");
        var main = UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();
        if (main == null) throw new InvalidOperationException("Open SampleScene first");
        var mainData = new SerializedObject(main); var db = (MergeGameDatabase)mainData.FindProperty("database").objectReferenceValue; db.Reload();
        var screen = (BattleScreen)mainData.FindProperty("battleScreen").objectReferenceValue;
        var data = new SerializedObject(screen); var board = (RectTransform)data.FindProperty("board").objectReferenceValue;
        var children = board.Cast<Transform>().Select(t => t.gameObject).ToArray(); foreach (var child in children) Undo.DestroyObjectImmediate(child);
        PrepareViewport(screen, board); var world = Build(screen, board, db);
        data.Update(); LandscapeSceneAuthoring.Bind(data, "worldView", world); data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(main.gameObject.scene); EditorSceneManager.SaveScene(main.gameObject.scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = world.gameObject; Debug.Log("Battle world migration complete: scene-authored SpriteRenderers and camera; HUD preserved.");
    }
}
