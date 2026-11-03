using UnityEngine;
using UnityEngine.UI;

namespace MergeTD
{
    // Scene-authored graphics; only their text, color and visibility change at runtime.
    public sealed class UnitLevelIndicator : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image badge;
        [SerializeField] private Image[] steps;
        [SerializeField] private bool compact;
        public int Level { get; private set; }
        public void SetLevel(int level)
        {
            Level=Mathf.Clamp(level,0,5);
            gameObject.SetActive(Level>0);
            if(Level==0)return;
            bool max=Level==5;
            var filled=max?new Color(1f,.77f,.3f):new Color(.88f,.94f,.91f);
            label.text=compact?Level.ToString():"Lv."+Level;
            label.color=max?new Color(.12f,.09f,.04f):Color.white;
            badge.color=max?filled:new Color(.15f,.22f,.24f);
            for(int i=0;i<steps.Length;i++)steps[i].color=i<Level?filled:new Color(.24f,.31f,.33f);
        }
    }
}
