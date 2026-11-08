using UnityEngine;

namespace MergeTD
{
    // Scene-authored visual only. Combat state belongs to DefenseBattle.
    public sealed class WorldActorView : MonoBehaviour
    {
        public SpriteRenderer icon;
        public SpriteRenderer healthTrack, healthFill, levelBadge;
        public TextMesh caption;
        public void Show(Sprite sprite, Vector2 position, float size, Color color, int order)
        {
            gameObject.SetActive(true);
            transform.localPosition = new Vector3(position.x, -position.y, 0);
            icon.sprite = sprite;
            icon.color = color;
            BattleWorldView.Size(icon, Vector2.one * size, true);
            icon.sortingOrder = order;
            healthTrack.sortingOrder = order + 2;
            healthFill.sortingOrder = levelBadge.sortingOrder = order + 3;
            caption.GetComponent<MeshRenderer>().sortingOrder = order + 4;
        }
        public void Health(float fraction, bool visible)
        {
            healthTrack.gameObject.SetActive(visible);
            healthFill.gameObject.SetActive(visible);
            if (!visible) return;
            float fill = Mathf.Clamp01(fraction);
            BattleWorldView.Size(healthFill, new Vector2(.66f * fill, .045f));
            healthFill.transform.localPosition = new Vector3(-.33f + .33f * fill, -.44f, 0);
        }
    }
}
