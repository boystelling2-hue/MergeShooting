using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MergeTD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BattleWorldVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static T Field<T>(object value, string key) => (T)value.GetType().GetField(key, Private).GetValue(value);
    private static void Check(bool value, string message) { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Draw(BattleScreen screen) => screen.GetType().GetMethod("Draw", Private).Invoke(screen, null);
    [MenuItem("Tools/MergeTD/Verify World Scene")]
    public static void VerifyScene()
    {
        var main = UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();
        var battle = Field<BattleScreen>(main, "battleScreen"); var world = battle.WorldView;
        Check(world != null && world.WorldCamera.orthographic, "Dedicated orthographic world camera missing");
        Check(world.GetComponentsInChildren<Canvas>(true).Length == 0, "World still contains a Canvas");
        Check(world.GetComponentsInChildren<Graphic>(true).Length == 0, "World still contains UI graphics");
        Check(Field<RectTransform>(battle, "board").childCount == 0, "Legacy UI field actors remain");
        Check(battle.GetComponentsInChildren<BattleCellHover>(true).Length == 0, "Legacy cell input remains");
        Check(world.TowerViews.Length == 198 && world.EnemyViews.Length == 32, "World pools missing");
        Check(world.Terrain.sprite != null && world.Route.sprite != null, "Terrain/route missing");
        Check(world.GetComponentsInChildren<SpriteRenderer>(true).All(r => r.sharedMaterial != null), "Sprite material missing");
        Check(world.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == world.gameObject.layer), "Mixed world layers");
        Check(battle.GetComponentsInChildren<Image>(true).Length < 100, "HUD still contains field graphics");
    }
    public static void Capture(BattleScreen battle, string file, int width, int height, bool validate = false)
    {
        var canvas = battle.GetComponent<Canvas>(); var world = battle.WorldView; var camera = world.WorldCamera;
        var mode = canvas.renderMode; var uiCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var target = camera.targetTexture; var cameraRect = camera.rect; float size = camera.orthographicSize;
        var position = camera.transform.position; float aspect = camera.aspect; int mask = camera.cullingMask;
        var active = RenderTexture.active; var rt = RenderTexture.GetTemporary(width, height, 24);
        try
        {
            camera.targetTexture = rt; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = width / (float)height;
            camera.cullingMask |= (1 << 5) | (1 << canvas.gameObject.layer);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.enabled = false; scaler.enabled = true;
            foreach (var text in canvas.GetComponentsInChildren<Text>()) text.SetAllDirty();
            Canvas.ForceUpdateCanvases(); Canvas.ForceUpdateCanvases();
            var points = new Vector3[4]; Field<RectTransform>(battle, "board").GetWorldCorners(points);
            Vector2 lo = RectTransformUtility.WorldToScreenPoint(camera, points[0]);
            Vector2 hi = RectTransformUtility.WorldToScreenPoint(camera, points[2]);
            float pixelsPerUnit = (hi.y - lo.y) / DefenseBattle.Rows;
            Vector2 center = (lo + hi) * .5f;
            camera.orthographicSize = height / (pixelsPerUnit * 2);
            camera.transform.position = new Vector3(8.5f + (width * .5f - center.x) / pixelsPerUnit,
                -5 + (height * .5f - center.y) / pixelsPerUnit, -20);
            Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render();
            if (validate)
            {
                for (int y = 0; y < DefenseBattle.Rows; y++) for (int x = 0; x < DefenseBattle.Columns; x++)
                {
                    Vector2 p = world.WorldCamera.WorldToScreenPoint(new Vector3(x, -y, 0));
                    Check(world.TryCell(p, out int cell) && cell == y * DefenseBattle.Columns + x, "Screen/world cell mapping " + width + ": " + x + "," + y);
                }
                var selected = Field<GameObject>(battle, "selectionRoot").GetComponent<RectTransform>();
                Vector2 blocked = RectTransformUtility.WorldToScreenPoint(camera, selected.TransformPoint(selected.rect.center));
                Check(world.BlockedByUI(blocked), "Selection panel click passes through to world");
                Vector2 free = world.WorldCamera.WorldToScreenPoint(new Vector3(12, -5, 0));
                Check(!world.BlockedByUI(free), "Battle viewport blocked by invisible UI");
                Vector2 shop = RectTransformUtility.WorldToScreenPoint(camera, Field<Button[]>(battle, "deckButtons")[0].transform.position);
                Check(world.BlockedByUI(shop), "Shop click passes through to world");
            }
            RenderTexture.active = rt; GL.Clear(true, true, new Color(.045f, .065f, .075f));
            camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllBytes(file, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            camera.targetTexture = target; camera.rect = cameraRect; camera.orthographicSize = size;
            camera.transform.position = position; camera.aspect = aspect; camera.cullingMask = mask;
            canvas.renderMode = mode; canvas.worldCamera = uiCamera; canvas.planeDistance = distance;
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Canvas.ForceUpdateCanvases(); world.SyncCamera();
        }
    }
    [MenuItem("Tools/MergeTD/Verify World Runtime")]
    public static void VerifyRuntime()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play for runtime checks");
        checks = 0; VerifyScene();
        var main = UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();
        var inventory = Field<CraftInventory>(main, "inventory"); string save = Newtonsoft.Json.JsonConvert.SerializeObject(inventory.Export());
        var db = Field<MergeGameDatabase>(main, "database"); var battle = Field<BattleScreen>(main, "battleScreen");
        var deck = db.GetAllUnits().Where(u => u.tier == 1).Select((u, i) => new CraftOwnedUnit("world-check-" + i, u.unitId, 5)).ToArray();
        var content = new GameContent(); content.Rules["initialBattleCash"] = 5000;
        int rewards = 0; main.GetComponent<Canvas>().enabled = false;
        try
        {
            battle.Open(db, content, deck, Field<Sprite[]>(main, "roleIcons"), _ => { rewards++; return true; }, () => { main.GetComponent<Canvas>().enabled = true; });
            Check(battle.WorldView.gameObject.activeInHierarchy, "Opening battle did not enable world");
            battle.PreviewCell(2 * 18 + 6); Check(battle.WorldView.AttackRange.color.r > .9f, "Path preview not invalid");
            int cash = battle.Simulation.Cash; battle.ClickWorldCell(2 * 18 + 6);
            Check(battle.Simulation.Towers.Count == 0 && battle.Simulation.Cash == cash, "Path placement charged cash");
            battle.ClickWorldCell(3 * 18 + 6); Check(battle.Simulation.Towers.Count == 1, "World deployment failed");
            var tower = battle.Simulation.Towers[0]; var view = battle.WorldView.TowerViews[3 * 18 + 6];
            Check(view.gameObject.activeSelf && view.transform.position == new Vector3(6, -3, 0), "Tower not rendered at world cell");
            Check(view.caption.text == "5", "World unit level missing");
            var target = Field<Button>(battle, "targetButton"); target.onClick.Invoke(); Check(tower.targetMode == 1, "Target priority binding lost");
            for (int i = 0; i < 5; i++) Check(battle.Simulation.Upgrade(tower, 0, out _), "Main path purchase failed");
            for (int i = 0; i < 2; i++) Check(battle.Simulation.Upgrade(tower, 1, out _), "Secondary path purchase failed");
            Check(!battle.Simulation.Upgrade(tower, 2, out _), "Third path lock regressed"); Draw(battle);
            var enemy = new DefenseBattle.Enemy { serial = 1, monsterId = "E01", position = new Vector2(4, 2), progress = 4.5f, hp = 100, maxHp = 100, speed = 1 };
            battle.Simulation.Enemies.Add(enemy); Draw(battle);
            Check(battle.WorldView.EnemyViews[0].transform.position == new Vector3(4, -2, 0), "Enemy world coordinates wrong");
            Check(!battle.WorldView.EnemyViews[0].healthFill.gameObject.activeSelf, "Full-health enemy bar should be hidden");
            battle.Simulation.Enemies.Add(new DefenseBattle.Enemy { serial = 5, hp = 80, maxHp = 100, position = new Vector2(8, 2) });
            battle.Simulation.Enemies.Add(new DefenseBattle.Enemy { serial = 10, boss = true, hp = 400, maxHp = 500, position = new Vector2(12, 2) });
            var positions = new[] { new Vector2(9, 3), new Vector2(15, 5), new Vector2(9, 7), new Vector2(4, 7) };
            for (int i = 1; i < deck.Length; i++)
            {
                Field<Button[]>(battle, "deckButtons")[i].onClick.Invoke();
                battle.ClickWorldCell((int)positions[i - 1].y * 18 + (int)positions[i - 1].x);
            }
            battle.ClickWorldCell(3 * 18 + 6); Draw(battle);
            Check(battle.WorldView.TowerViews.Count(v => v.gameObject.activeSelf) == 5, "Multiple tower roles not rendered");
            var enemyViews = battle.WorldView.EnemyViews;
            Check(enemyViews[0].icon.sprite != enemyViews[1].icon.sprite && enemyViews[1].icon.sprite != enemyViews[2].icon.sprite, "Enemy variants lost");
            Check(enemyViews[2].icon.bounds.size.x > enemyViews[0].icon.bounds.size.x, "Boss not larger than normal enemy");
            Check(enemyViews[1].healthFill.gameObject.activeSelf && enemyViews[2].caption.text == "BOSS", "Enemy damage/boss feedback missing");
            Check(enemyViews[1].healthFill.sortingOrder > enemyViews[1].healthTrack.sortingOrder, "Health fill hidden by track");
            battle.Simulation.Shots.Add(new DefenseBattle.Shot { owner = tower, position = new Vector2(6, 2.5f) });
            battle.Simulation.Flashes.Add(new DefenseBattle.Flash { from = new Vector2(6, 3), to = new Vector2(4, 2), life = .15f, color = Color.yellow }); Draw(battle);
            Check(battle.WorldView.transform.Find("Moving Actors/Projectile 0").gameObject.activeSelf, "Projectile world view missing");
            Check(battle.WorldView.transform.Find("Moving Actors/Attack Trace 0").gameObject.activeSelf, "Attack trace missing");
            Capture(battle, "Captures/world-selected-1280.png", 1280, 720, true);
            Capture(battle, "Captures/world-selected-1920.png", 1920, 1080, true);
            Capture(battle, "Captures/world-selected-wide.png", 1600, 720, true);
            // Dedicated motion scenario: keep combat and upgrades out of the per-frame position test.
            battle.Open(db, content, deck, Field<Sprite[]>(main, "roleIcons"), _ => { rewards++; return true; }, () => { main.GetComponent<Canvas>().enabled = true; });
            battle.Simulation.Enemies.Add(enemy); battle.Simulation.Start();
            var advance = battle.GetType().GetMethod("AdvanceFrame", Private);
            foreach (int fps in new[] { 30, 60, 120 }) foreach (float speed in new[] { 1f, 2f })
            {
                battle.GetType().GetField("speed", Private).SetValue(battle, speed); float previous = enemy.position.x;
                for (int frame = 0; frame < 12; frame++)
                {
                    advance.Invoke(battle, new object[] { 1f / fps });
                    Check(Vector3.Distance(battle.WorldView.EnemyViews[0].transform.position, BattleWorldView.Point(enemy.position)) < .0001f, "Enemy visual lags simulation");
                    Check(Mathf.Abs(enemy.position.x - previous - speed / fps) < .001f, "Uneven simulation step"); previous = enemy.position.x;
                }
            }
            Field<Button>(battle, "settingsButton").onClick.Invoke(); float frozen = enemy.progress;
            advance.Invoke(battle, new object[] { .1f }); Check(enemy.progress == frozen && battle.WorldInputBlocked, "Pause did not freeze world/input");
            int count = battle.Simulation.Towers.Count; battle.ClickWorldCell(60); Check(battle.Simulation.Towers.Count == count, "Paused click deployed unit");
            Field<Button>(battle, "resumeButton").onClick.Invoke(); Check(!battle.WorldInputBlocked, "Resume left input blocked");
            Field<Button>(battle, "speedButton").onClick.Invoke(); Check(Field<float>(battle, "speed") == 1, "Speed button lost binding");
            Field<Button>(battle, "returnButton").onClick.Invoke(); Check(!battle.WorldView.gameObject.activeSelf && main.GetComponent<Canvas>().enabled, "Return left world visible");
            Check(rewards == 0 && save == Newtonsoft.Json.JsonConvert.SerializeObject(inventory.Export()), "Real inventory changed during smoke test");
            File.WriteAllText("Library/BattleWorldVerification.txt", "PASS: " + checks + " checks: scene separation, world deployment, targeting/upgrades, projectiles/traces, 198 cells at 3 resolutions, UI input blocking, 72 motion frames at 30/60/120 FPS and 1x/2x, pause/resume, return; inventory unchanged.");
            Debug.Log("Battle world verification PASS: " + checks);
        }
        finally { battle.gameObject.SetActive(false); main.GetComponent<Canvas>().enabled = true; }
    }
}
