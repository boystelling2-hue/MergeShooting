using UnityEngine;
using UnityEngine.EventSystems;

namespace MergeTD
{
    public sealed class MergeSlotView : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public BattleMergeSandbox Owner { get; set; }
        public int Index { get; set; }
        public void OnPointerClick(PointerEventData e) { if (!e.dragging) Owner.SelectSlot(Index); }
        public void OnBeginDrag(PointerEventData e) { Owner.BeginDrag(Index, e); }
        public void OnDrag(PointerEventData e) { Owner.UpdateDrag(e); }
        public void OnEndDrag(PointerEventData e) { Owner.EndDrag(); }
        public void OnDrop(PointerEventData e)
        {
            var source = e.pointerDrag == null ? null : e.pointerDrag.GetComponent<MergeSlotView>();
            if (source != null && source.Owner == Owner) Owner.MoveOrMerge(source.Index, Index);
        }
    }
}
