using UnityEngine;
using UnityEngine.EventSystems;
namespace MergeTD
{
    public sealed class BattleCellHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] private BattleScreen screen;
        [SerializeField] private int index;
        public void OnPointerEnter(PointerEventData e)=>screen.PreviewCell(index);
        public void OnPointerExit(PointerEventData e)=>screen.ClearPreview(index);
    }
}
