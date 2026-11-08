using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MergeTD
{
    // All objects and pools are authored in the scene, never built by the HUD at runtime.
    public sealed class BattleWorldView : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private BattleScreen screen;
        [SerializeField] private SpriteRenderer terrain, route, attackRange, supportRange, placement, previewIcon;
        [SerializeField] private WorldActorView[] towers, enemies;
        [SerializeField] private SpriteRenderer[] projectiles, traces;
        [SerializeField] private Sprite[] enemySprites;
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly Vector3[] corners = new Vector3[4];
        private PointerEventData pointerEvent;
        private EventSystem eventSystem;
        private readonly bool[] visibleTowers = new bool[DefenseBattle.Columns * DefenseBattle.Rows];
        private int hovered = -1;
        public Camera WorldCamera => worldCamera;
        public SpriteRenderer Terrain => terrain;
        public SpriteRenderer Route => route;
        public WorldActorView[] EnemyViews => enemies;
        public WorldActorView[] TowerViews => towers;
        public SpriteRenderer AttackRange => attackRange;

        public static Vector3 Point(Vector2 p) => new Vector3(p.x, -p.y, 0);
        public static void Size(SpriteRenderer renderer, Vector2 size, bool preserve = false)
        {
            if (renderer.sprite == null) return;
            Vector2 bounds = renderer.sprite.bounds.size;
            float x = size.x / bounds.x, y = size.y / bounds.y;
            if (preserve) x = y = Mathf.Min(x, y);
            renderer.transform.localScale = new Vector3(x, y, 1);
        }
        public void ResetViews()
        {
            foreach (var view in towers) view.gameObject.SetActive(false);
            foreach (var view in enemies) view.gameObject.SetActive(false);
            foreach (var view in projectiles) view.gameObject.SetActive(false);
            foreach (var view in traces) view.gameObject.SetActive(false);
            attackRange.gameObject.SetActive(false); supportRange.gameObject.SetActive(false);
            placement.gameObject.SetActive(false); previewIcon.gameObject.SetActive(false);
            hovered = -1;
        }
        public void SyncCamera()
        {
            if (viewport == null || worldCamera == null || Screen.width < 1 || Screen.height < 1) return;
            viewport.GetWorldCorners(corners);
            var canvas = viewport.GetComponentInParent<Canvas>();
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var lo = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            var hi = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            float width = uiCamera != null && uiCamera.targetTexture != null ? uiCamera.targetTexture.width : Screen.width;
            float height = uiCamera != null && uiCamera.targetTexture != null ? uiCamera.targetTexture.height : Screen.height;
            worldCamera.rect = new Rect(lo.x / width, lo.y / height,
                (hi.x - lo.x) / width, (hi.y - lo.y) / height);
            worldCamera.orthographicSize = DefenseBattle.Rows * .5f;
            worldCamera.aspect = (hi.x - lo.x) / Mathf.Max(1, hi.y - lo.y);
        }
        public bool TryCell(Vector2 screenPoint, out int index)
        {
            index = -1;
            if (!worldCamera.pixelRect.Contains(screenPoint)) return false;
            Vector3 p = transform.InverseTransformPoint(worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 20)));
            int x = Mathf.FloorToInt(p.x + .5f), y = Mathf.FloorToInt(-p.y + .5f);
            if (x < 0 || x >= DefenseBattle.Columns || y < 0 || y >= DefenseBattle.Rows) return false;
            index = y * DefenseBattle.Columns + x; return true;
        }
        public bool BlockedByUI(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            if (pointerEvent == null || eventSystem != EventSystem.current)
            { eventSystem = EventSystem.current; pointerEvent = new PointerEventData(eventSystem); }
            pointerEvent.Reset(); pointerEvent.position = position; hits.Clear();
            EventSystem.current.RaycastAll(pointerEvent, hits);
            foreach (var hit in hits) if (hit.module is GraphicRaycaster) return true;
            return false;
        }
        private void LateUpdate() => SyncCamera();
        private void Update()
        {
            if (screen == null || screen.Simulation == null) return;
            SyncCamera();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame ||
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            { screen.CancelWorldSelection(); ClearHover(); return; }
            Vector2 position; bool click;
            var touch = Touchscreen.current == null ? null : Touchscreen.current.primaryTouch;
            if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
            { position = touch.position.ReadValue(); click = touch.press.wasReleasedThisFrame; }
            else if (Mouse.current != null)
            { position = Mouse.current.position.ReadValue(); click = Mouse.current.leftButton.wasPressedThisFrame; }
            else { ClearHover(); return; }
            if (screen.WorldInputBlocked || BlockedByUI(position) || !TryCell(position, out var cell))
            { ClearHover(); return; }
            if (hovered != cell) { ClearHover(); hovered = cell; screen.PreviewCell(cell); }
            if (click) screen.ClickWorldCell(cell);
        }
        private void ClearHover()
        {
            if (hovered >= 0 && screen != null) screen.ClearPreview(hovered);
            hovered = -1;
        }
        public void DrawTowers(DefenseBattle battle, Sprite[] roles, DefenseBattle.Tower selected)
        {
            System.Array.Clear(visibleTowers, 0, visibleTowers.Length);
            foreach (var tower in battle.Towers)
            {
                if (!tower.Alive) continue;
                int index = Mathf.RoundToInt(tower.position.y) * DefenseBattle.Columns + Mathf.RoundToInt(tower.position.x);
                var view = towers[index]; int order = 100 + Mathf.RoundToInt(tower.position.y * 10);
                visibleTowers[index] = true;
                view.Show(roles[MainCraftingScreen.RoleIndex(tower.definition)], tower.position, .66f,
                    MainCraftingScreen.TierColor(tower.tier), order);
                view.caption.text = tower.level.ToString();
                view.caption.color = tower.level == 5 ? new Color(1, .8f, .3f) : Color.white;
                view.Health(tower.hp / tower.maxHp, tower == selected || tower.hp < tower.maxHp);
                view.levelBadge.gameObject.SetActive(true);
            }
            for (int i = 0; i < towers.Length; i++)
                if (!visibleTowers[i] && towers[i].gameObject.activeSelf) towers[i].gameObject.SetActive(false);
        }
        public void DrawActors(DefenseBattle battle)
        {
            int i = 0;
            foreach (var enemy in battle.Enemies)
            {
                if (!enemy.Alive || i >= enemies.Length) continue;
                int variant = enemy.boss ? 2 : enemy.serial % 5 == 0 ? 1 : 0;
                var view = enemies[i++];
                view.Show(enemySprites[variant], enemy.position, enemy.boss ? 1.07f : variant == 1 ? .7f : .64f,
                    Color.white, 100 + Mathf.RoundToInt(enemy.position.y * 10));
                view.caption.text = enemy.boss ? "BOSS" : "";
                view.Health(enemy.hp / enemy.maxHp, enemy.hp < enemy.maxHp || enemy.boss);
                view.levelBadge.gameObject.SetActive(false);
            }
            for (; i < enemies.Length; i++) enemies[i].gameObject.SetActive(false);
            for (i = 0; i < projectiles.Length; i++)
            {
                bool on = i < battle.Shots.Count; var image = projectiles[i]; image.gameObject.SetActive(on);
                if (!on) continue;
                var shot = battle.Shots[i]; image.transform.localPosition = Point(shot.position);
                image.color = MainCraftingScreen.TierColor(shot.owner.tier);
            }
            for (i = 0; i < traces.Length; i++)
            {
                bool on = i < battle.Flashes.Count; var image = traces[i]; image.gameObject.SetActive(on);
                if (!on) continue;
                var flash = battle.Flashes[i]; Vector3 delta = Point(flash.to) - Point(flash.from);
                // The unit square sprite is centred, so put a beam midway between its endpoints.
                image.transform.localPosition = (Point(flash.from) + Point(flash.to)) * .5f;
                image.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                Size(image, new Vector2(delta.magnitude, .045f));
                image.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.Clamp01(flash.life / .15f));
            }
        }
        public void DrawSelection(DefenseBattle battle, DefenseBattle.Tower tower, bool preview, int cell,
            bool valid, Sprite role)
        {
            bool shown = tower != null;
            attackRange.gameObject.SetActive(shown && battle.AttackRange(tower) > 0);
            supportRange.gameObject.SetActive(shown && battle.SupportRange(tower) > 0);
            placement.gameObject.SetActive(shown);
            previewIcon.gameObject.SetActive(shown && preview);
            if (!shown) return;
            Vector2 p = preview ? new Vector2(cell % DefenseBattle.Columns, cell / DefenseBattle.Columns) : tower.position;
            attackRange.transform.localPosition = supportRange.transform.localPosition = placement.transform.localPosition = previewIcon.transform.localPosition = Point(p);
            Size(attackRange, Vector2.one * battle.AttackRange(tower) * 2);
            Size(supportRange, Vector2.one * battle.SupportRange(tower) * 2);
            var color = valid ? new Color(.3f, 1, .5f, .85f) : new Color(1, .25f, .2f, .85f);
            attackRange.color = preview ? color : new Color(1, .8f, .3f, .85f);
            supportRange.color = preview ? color : new Color(.3f, 1, .8f, .7f);
            placement.color = preview ? new Color(color.r, color.g, color.b, .25f) : new Color(.1f, .4f, .25f, .3f);
            previewIcon.sprite = role; previewIcon.color = new Color(1, 1, 1, .5f); Size(previewIcon, Vector2.one * .66f, true);
        }
    }
}
