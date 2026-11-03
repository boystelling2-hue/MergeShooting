using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MergeTD
{
    public sealed class CraftTrashDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public MainCraftingScreen Owner { get; set; }
        private Outline outline;
        private void Awake(){outline=GetComponent<Outline>();}
        public void OnDrop(PointerEventData e){if(outline!=null)outline.enabled=false;Owner?.DropTrash(e);}
        public void OnPointerEnter(PointerEventData e){if(outline!=null)outline.enabled=Owner!=null&&Owner.IsDragging;}
        public void OnPointerExit(PointerEventData e){if(outline!=null)outline.enabled=false;}
        private void Update(){if(outline!=null&&(Owner==null||!Owner.IsDragging))outline.enabled=false;}
    }
}
