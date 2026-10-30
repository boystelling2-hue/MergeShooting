using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeTD
{
    public sealed class MainCraftingScreen : MonoBehaviour
    {
        private MergeGameDatabase database;
        private CraftInventory inventory;
        private List<UnitDefinition> definitions;
        private string first, second;
        private Text firstLabel, secondLabel, preview, status, summary, detail;
        private Button craftButton;
        private RectTransform content;
        private ScrollRect scroll;
        private readonly Dictionary<string,Button> cards = new Dictionary<string,Button>();
        private readonly Dictionary<string,Text> cardLabels = new Dictionary<string,Text>();
        private Font font;
        private CanvasScaler canvasScaler;
        private bool saveReady;
        private static readonly Color Ink = new Color(.055f,.07f,.12f);
        private static readonly Color PanelColor = new Color(.105f,.13f,.2f);
        private static readonly Color Muted = new Color(.64f,.7f,.8f);
        private static readonly Color Accent = new Color(.35f,.84f,.7f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if(SceneManager.GetActiveScene().name=="SampleScene" && FindFirstObjectByType<MergeGameDatabase>()!=null && FindFirstObjectByType<MainCraftingScreen>()==null)
                new GameObject("Main Crafting Screen").AddComponent<MainCraftingScreen>();
        }

        private void Start()
        {
            database=FindFirstObjectByType<MergeGameDatabase>();
            if(database==null || !database.IsLoaded) { Debug.LogError("Crafting requires a loaded database."); enabled=false; return; }
            definitions=database.GetAllUnits();
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},28);
            var save=new CraftSaveFile(Path.Combine(Application.persistentDataPath,"main-crafting-v1.json"));
            inventory=new CraftInventory(database,save.Write);
            string startup="보관함에서 재료 두 개를 선택하세요. 같은 종류도 두 개 있으면 합칠 수 있어요.";
            try
            {
                if(save.Exists) inventory.Restore(save.Read());
                else
                {
                    // Initial test supply, explicitly separate from production acquisition rules.
                    var starters=new List<CraftOwnedUnit>();
                    foreach(var unit in definitions) if(unit.tier==1)
                        for(int i=0;i<4;i++) starters.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),unit.unitId));
                    var initial=new CraftSaveState { units=starters.ToArray(),discoveredPaths=new string[0] };
                    save.Write(initial); inventory.Restore(initial);
                }
                saveReady=true;
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { startup="저장 파일을 읽지 못했습니다. 원본 보호를 위해 합성을 잠갔습니다. "+ex.Message; Debug.LogError(startup); }
            BuildUI();
            inventory.Changed+=Refresh;
            Refresh(); status.text=startup;
        }

        private void BuildUI()
        {
            var root=new GameObject("Crafting Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);canvasScaler=scaler;FitCanvas();
            var backdrop=Panel("Full Background",root.transform,0,0,1080,1920,Ink);
            backdrop.anchorMin=Vector2.zero;backdrop.anchorMax=Vector2.one;backdrop.offsetMin=backdrop.offsetMax=Vector2.zero;
            var layout=new GameObject("Portrait Layout",typeof(RectTransform));layout.transform.SetParent(root.transform,false);
            var layoutRect=(RectTransform)layout.transform;layoutRect.anchorMin=layoutRect.anchorMax=new Vector2(.5f,.5f);
            layoutRect.pivot=new Vector2(.5f,.5f);layoutRect.sizeDelta=new Vector2(1080,1920);layoutRect.anchoredPosition=Vector2.zero;
            root=layout;
            if(EventSystem.current==null)
            {
                var events=new GameObject("Crafting EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Panel("Background",root.transform,0,0,1080,1920,Ink);
            Label("MERGE SHOOTING  /  WORKSHOP",root.transform,56,42,970,50,23,Accent);
            Label("유닛 합성",root.transform,52,100,970,100,64,Color.white);
            summary=Label("",root.transform,56,210,970,50,25,Muted);

            Panel("Crafting",root.transform,48,296,984,540,PanelColor);
            Label("재료 선택",root.transform,76,316,800,46,27,Color.white);
            var a=Button("Material A",root.transform,76,382,414,128,new Color(.16f,.2f,.29f),()=>ClearMaterial(0));
            firstLabel=Label("",a.transform,12,6,390,116,29,Color.white,TextAnchor.MiddleCenter);
            Label("+",root.transform,493,409,94,70,42,Accent,TextAnchor.MiddleCenter);
            var b=Button("Material B",root.transform,590,382,414,128,new Color(.16f,.2f,.29f),()=>ClearMaterial(1));
            secondLabel=Label("",b.transform,12,6,390,116,29,Color.white,TextAnchor.MiddleCenter);
            preview=Label("",root.transform,76,530,928,94,30,Accent,TextAnchor.MiddleCenter);
            craftButton=Button("Craft",root.transform,76,658,928,116,Accent,Craft);
            Label("합성하기",craftButton.transform,0,0,928,116,36,Ink,TextAnchor.MiddleCenter);

            status=Label("",root.transform,56,860,968,110,26,Accent);
            Label("보유 유닛",root.transform,56,990,500,54,32,Color.white);
            Label("카드를 눌러 재료 추가",root.transform,554,990,470,54,23,Muted,TextAnchor.MiddleRight);
            var viewport=Panel("Inventory Viewport",root.transform,48,1060,984,544,new Color(.07f,.09f,.145f));
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.viewport=viewport;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            content=new GameObject("Owned Units",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            content.sizeDelta=new Vector2(0,544);scroll.content=content;
            foreach(var unit in definitions)
            {
                string id=unit.unitId;
                var button=Button(id,content,0,0,230,128,PanelColor,()=>SelectMaterial(id));
                cards[id]=button;
                cardLabels[id]=Label("",button.transform,8,4,214,120,25,Color.white,TextAnchor.MiddleCenter);
                button.gameObject.SetActive(false);
            }
            detail=Label("",root.transform,56,1620,968,80,24,Muted);
            Panel("Test Supply",root.transform,48,1722,984,146,PanelColor);
            Label("개발 테스트용 · 기본 재료 지급",root.transform,68,1732,940,42,22,Muted);
            int index=0;
            foreach(var unit in definitions)
            {
                if(unit.tier!=1) continue;
                string id=unit.unitId;
                var button=Button("Grant "+id,root.transform,68+index*190,1788,180,58,new Color(.18f,.23f,.32f),()=>Grant(id));
                Label(unit.name+" +2",button.transform,0,0,180,58,24,Color.white,TextAnchor.MiddleCenter);
                button.interactable=saveReady;index++;
            }
        }

        private void SelectMaterial(string unitId)
        {
            if(!saveReady) return;
            CraftOwnedUnit available=null;
            foreach(var unit in inventory.GetOwnedUnits())
                if(unit.unitId==unitId && unit.instanceId!=first && unit.instanceId!=second) { available=unit;break; }
            if(available==null) { status.text="선택할 재료가 부족합니다. 이미 선택한 개체는 중복 사용할 수 없어요.";return; }
            if(first==null) first=available.instanceId;
            else if(second==null) second=available.instanceId;
            else { status.text="재료 칸을 눌러 비운 뒤 다른 유닛을 선택하세요.";return; }
            var definition=database.GetUnit(unitId);
            detail.text=definition.name+" · "+definition.role+" · "+definition.attackElement+"\n"+definition.skillName+": "+definition.skillDescription;
            status.text="재료 칸을 누르면 선택을 취소할 수 있어요."; Refresh();
        }
        private void ClearMaterial(int index)
        { if(index==0) first=null; else second=null; Refresh();status.text="재료 선택을 취소했습니다."; }
        private void Grant(string unitId)
        { if(!saveReady) return;inventory.TryGrant(unitId,2,out var message);status.text=message; }
        private void Craft()
        {
            if(!saveReady) return;
            if(inventory.TryCraft(first,second,out var result,out var message))
            {
                first=null;second=null;Refresh();
                detail.text=result.name+" · 제작 "+result.tier+"티어 · "+result.attackElement+"\n"+result.skillName+": "+result.skillDescription;
            }
            status.text=message;
        }

        private void Refresh()
        {
            if(content==null) return;
            var owned=inventory.GetOwnedUnits();
            CraftOwnedUnit a=null,b=null;
            var counts=new Dictionary<string,int>();var reserved=new Dictionary<string,int>();
            foreach(var unit in owned)
            {
                counts.TryGetValue(unit.unitId,out int n);counts[unit.unitId]=n+1;
                if(unit.instanceId==first) a=unit;
                if(unit.instanceId==second) b=unit;
            }
            if(a==null) first=null;if(b==null) second=null;
            if(a!=null) reserved[a.unitId]=1;
            if(b!=null) { reserved.TryGetValue(b.unitId,out int n);reserved[b.unitId]=n+1; }
            firstLabel.text=MaterialText(a,"첫 번째 재료");secondLabel.text=MaterialText(b,"두 번째 재료");
            bool valid=a!=null && b!=null && database.TryResolveCraftMerge(a.unitId,b.unitId,out _);
            craftButton.interactable=saveReady && valid;
            if(a==null || b==null) preview.text="보관함에서 두 개체를 선택하세요";
            else if(!valid) preview.text="조합 불가 · 같은 제작 티어의 재료를 선택하세요";
            else if(inventory.IsDiscovered(a.unitId,b.unitId))
            { database.TryResolveCraftMerge(a.unitId,b.unitId,out var result);preview.text="발견한 제작법 → "+result.name; }
            else preview.text="?  미발견 제작법 · 합성하여 결과 확인";
            int i=0;
            foreach(var unit in definitions)
            {
                counts.TryGetValue(unit.unitId,out int count);
                var button=cards[unit.unitId];button.gameObject.SetActive(count>0);if(count==0) continue;
                var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(12+(i%4)*242,-12-(i/4)*140);i++;
                reserved.TryGetValue(unit.unitId,out int selectedCount);
                cardLabels[unit.unitId].text=unit.name+"\nT"+unit.tier+" · 보유 "+count+(selectedCount>0?" / 선택 "+selectedCount:"");
                button.interactable=saveReady && count>selectedCount && (first==null || second==null);
                button.GetComponent<Image>().color=unit.tier==3?new Color(.25f,.17f,.35f):unit.tier==2?new Color(.13f,.26f,.29f):PanelColor;
            }
            content.sizeDelta=new Vector2(0,Mathf.Max(544,24+Mathf.CeilToInt(i/4f)*140));
            summary.text="유닛 "+inventory.Count+"개체 · 발견한 제작 경로 "+inventory.DiscoveredCount+" / "+database.GetAllCraftRecipes().Count;
        }
        private string MaterialText(CraftOwnedUnit item,string empty) => item==null ? empty+"\n선택 대기" : database.GetUnit(item.unitId).name+"\n제작 "+database.GetUnit(item.unitId).tier+"티어 · 눌러 취소";

        private RectTransform Panel(string name,Transform parent,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);go.GetComponent<Image>().color=color;return rect;
        }
        private Text Label(string text,Transform parent,float x,float y,float w,float h,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var label=go.GetComponent<Text>();label.font=font;label.fontSize=size;label.color=color;label.text=text;label.alignment=alignment;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;return label;
        }
        private Button Button(string name,Transform parent,float x,float y,float w,float h,Color color,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(name,parent,x,y,w,h,color);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(action);return button;
        }
        private void OnDestroy() { if(inventory!=null) inventory.Changed-=Refresh;if(font!=null) Destroy(font); }
        private void Update() { FitCanvas(); }
        private void FitCanvas()
        { if(canvasScaler!=null) canvasScaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)<1080f/1920f ? 0f : 1f; }
    }
}
