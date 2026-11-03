using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MergeTD
{
    public sealed class CraftUnitView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon, surface;
        [SerializeField] private Text unitName, tier;
        [SerializeField] private Outline outline;
        [SerializeField] private UnitLevelIndicator levelIndicator;
        public MainCraftingScreen Owner { get; set; }
        public CraftOwnedUnit Item { get; private set; }
        public Image Icon => icon;
        public void Bind(CraftOwnedUnit item, UnitDefinition definition, Sprite sprite)
        {
            Item=item;icon.enabled=item!=null;icon.sprite=sprite;
            unitName.text=definition==null?"":definition.name;
            tier.text=definition==null?"":"T"+definition.tier;
            if(levelIndicator!=null)levelIndicator.SetLevel(item==null?0:item.level);
            tier.color=definition==null?new Color(.42f,.46f,.47f):MainCraftingScreen.TierColor(definition.tier);
            icon.color=definition==null?Color.white:MainCraftingScreen.TierColor(definition.tier);
            surface.color=definition==null?new Color(.12f,.15f,.17f,.35f):new Color(.13f,.17f,.19f,.88f);
            Highlight(false,false);
        }
        public void Highlight(bool valid,bool source) { outline.enabled=valid||source;outline.effectColor=source?new Color(1f,.8f,.35f):new Color(.43f,1f,.73f); }
        public void OnBeginDrag(PointerEventData e) { Owner?.BeginDrag(this,e); }
        public void OnDrag(PointerEventData e) { Owner?.MoveDrag(e); }
        public void OnEndDrag(PointerEventData e) { Owner?.EndDrag(e); }
        public void OnDrop(PointerEventData e) { Owner?.Drop(this,e); }
        public void OnPointerClick(PointerEventData e) { if(!e.dragging) Owner?.Inspect(this); }
    }
}

