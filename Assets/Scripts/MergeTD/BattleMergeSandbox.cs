using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MergeTD
{
    // Interactive test harness, not the final summoning economy or battle balance.
    public sealed class BattleMergeSandbox : MonoBehaviour
    {
        [SerializeField, Range(2, 9)] private int prototypeMaxStar = 5;
        [SerializeField] private string[] deckUnitIds =
        { "U1_10000", "U1_01000", "U1_00100", "U1_00010", "U1_00001" };
        private readonly Color[] colors =
        { new Color(.36f,.65f,.98f), new Color(.32f,.81f,.63f), new Color(.93f,.67f,.32f),
          new Color(.93f,.49f,.64f), new Color(.67f,.54f,.98f) };
        private readonly string[] symbols = { "K", "A", "B", "H", "M" };
        private BattleMergeBoard board;
        private MergeGameDatabase database;
        private Canvas canvas;
        private Font font;
        private Text status, count;
        private Image[] slotImages;
        private Text[] slotLabels;
        private Outline[] outlines;
        private Button[] summonButtons;
        private int selected = -1;
        private RectTransform ghost;
        private bool createdEventSystem;
        private GameObject eventSystemObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name == "SampleScene" &&
                FindFirstObjectByType<MergeGameDatabase>() != null &&
                FindFirstObjectByType<BattleMergeSandbox>() == null)
                new GameObject("Battle Merge Sandbox").AddComponent<BattleMergeSandbox>();
        }

        private void Start()
        {
            database = FindFirstObjectByType<MergeGameDatabase>();
            if (database == null || !database.IsLoaded || !database.IsValidDeck(deckUnitIds))
            { Debug.LogError("Merge sandbox requires a loaded database and a valid deck."); enabled = false; return; }
            board = new BattleMergeBoard(deckUnitIds, 20, prototypeMaxStar);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 28);
            BuildUI();
            board.Changed += Refresh;
            // Seed a visible pair so the first action can be a merge.
            board.TrySpawn(deckUnitIds[0], out _, out _);
            board.TrySpawn(deckUnitIds[0], out _, out _);
            Refresh();
            status.text = "파란 기사 두 명을 겹쳐 보세요. 클릭으로도 합칠 수 있어요.";
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("Merge Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject("Merge EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystemObject.transform.SetParent(transform, false);
                createdEventSystem = true;
                eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Panel("Background", canvas.transform, 0, 0, 1080, 1920, new Color(.045f,.065f,.11f));
            Label("MERGE LAB  /  01", canvas.transform, 64, 64, 950, 42, 24, new Color(.39f,.83f,.75f));
            Label("전투 머지", canvas.transform, 60, 122, 950, 100, 68, Color.white);
            Label("같은 유닛 · 같은 성급 · 두 개를 하나로", canvas.transform, 64, 232, 950, 52, 29, new Color(.64f,.7f,.8f));
            Panel("Deck Panel", canvas.transform, 52, 334, 976, 272, new Color(.085f,.115f,.18f));
            Label("출전 덱", canvas.transform, 78, 356, 500, 40, 27, Color.white);
            Label("버튼을 눌러 1성 소환", canvas.transform, 550, 356, 440, 40, 24, new Color(.64f,.7f,.8f), TextAnchor.MiddleRight);
            summonButtons = new Button[deckUnitIds.Length];
            float w = 900f / deckUnitIds.Length;
            for (int i = 0; i < deckUnitIds.Length; i++)
            {
                int index = i;
                var def = database.GetUnit(deckUnitIds[i]);
                var button = MakeButton(def.name + "\n+ 소환", canvas.transform, 78 + i*w, 416, w-14, 158, colors[i % colors.Length], () => Spawn(index));
                summonButtons[i] = button;
            }
            Label("배치 보드", canvas.transform, 64, 664, 500, 52, 32, Color.white);
            count = Label("", canvas.transform, 560, 664, 454, 52, 25, new Color(.64f,.7f,.8f), TextAnchor.MiddleRight);
            slotImages = new Image[board.Capacity]; slotLabels = new Text[board.Capacity]; outlines = new Outline[board.Capacity];
            for (int i=0; i<board.Capacity; i++)
            {
                float x=64+(i%4)*242; float y=744+(i/4)*142;
                var cell=Panel("Slot " + i, canvas.transform, x,y,226,126,new Color(.085f,.115f,.18f));
                slotImages[i]=cell.GetComponent<Image>();
                var view=cell.gameObject.AddComponent<MergeSlotView>(); view.Owner=this; view.Index=i;
                outlines[i]=cell.gameObject.AddComponent<Outline>(); outlines[i].effectDistance=new Vector2(3,-3); outlines[i].enabled=false;
                slotLabels[i]=Label("",cell,4,4,218,118,27,Color.white,TextAnchor.MiddleCenter);
            }
            status=Label("",canvas.transform,64,1480,952,110,28,new Color(.65f,.88f,.8f));
            MakeButton("선택 해제",canvas.transform,64,1630,450,84,new Color(.15f,.2f,.29f),()=>{ selected=-1; EndDrag(); Refresh(); status.text="선택을 해제했습니다."; });
            MakeButton("새 테스트",canvas.transform,534,1630,482,84,new Color(.15f,.2f,.29f),()=>{ selected=-1; EndDrag(); board.Reset(); status.text="새 테스트를 시작했습니다. 덱에서 유닛을 소환하세요."; });
            Label("테스트 규칙  ·  무료 소환 / 보드 20칸 / 최대 " + prototypeMaxStar + "성\n제작 티어와 전투 성급은 별개입니다. 전투 종료 시 초기화됩니다.",canvas.transform,64,1760,952,100,23,new Color(.46f,.53f,.64f));
        }

        private void Spawn(int index)
        {
            board.TrySpawn(deckUnitIds[index],out _,out var message);
            status.text=database.GetUnit(deckUnitIds[index]).name + " · " + message;
        }

        public void SelectSlot(int index)
        {
            if (selected >= 0 && selected != index) { MoveOrMerge(selected,index); return; }
            selected = board.Get(index) == null || selected == index ? -1 : index;
            Refresh();
            status.text=selected<0 ? "유닛을 선택하세요." : "빈 칸으로 이동하거나, 같은 종류·성급의 유닛을 선택하세요.";
        }

        public void MoveOrMerge(int source,int target)
        {
            bool success=board.TryMoveOrMerge(source,target,out var message);
            if(success) selected=-1;
            status.text=message;
            Refresh();
        }

        public void BeginDrag(int index,PointerEventData e)
        {
            EndDrag();
            var piece=board.Get(index); if(piece==null) return;
            selected=index; Refresh();
            ghost=Panel("Drag Preview",canvas.transform,0,0,210,116,colors[DeckIndex(piece.UnitId)]);
            ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
            ghost.GetComponent<CanvasGroup>().alpha=.85f;
            Label(database.GetUnit(piece.UnitId).name+"\n"+piece.Star+"성",ghost,0,0,210,116,27,Color.white,TextAnchor.MiddleCenter);
            UpdateDrag(e);
        }
        public void UpdateDrag(PointerEventData e)
        {
            if(ghost==null) return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,e.pressEventCamera,out var local))
            { ghost.anchorMin=ghost.anchorMax=new Vector2(.5f,.5f); ghost.pivot=new Vector2(.5f,.5f); ghost.anchoredPosition=local; }
        }
        public void EndDrag() { if(ghost!=null) Destroy(ghost.gameObject); ghost=null; }

        private void Refresh()
        {
            if(slotImages==null) return;
            for(int i=0;i<board.Capacity;i++)
            {
                var piece=board.Get(i);
                slotImages[i].color=piece==null?new Color(.085f,.115f,.18f):Color.Lerp(new Color(.085f,.115f,.18f),colors[DeckIndex(piece.UnitId)],.46f);
                slotLabels[i].text=piece==null ? "+" : symbols[DeckIndex(piece.UnitId)]+"  "+database.GetUnit(piece.UnitId).name+"\n"+new string('★',piece.Star)+(piece.Star==board.MaxStar?" MAX":"");
                slotLabels[i].color=piece==null?new Color(.23f,.3f,.4f):Color.white;
                outlines[i].enabled=i==selected || (selected>=0 && board.CanMerge(selected,i));
                outlines[i].effectColor=i==selected?Color.white:new Color(.4f,.96f,.72f);
            }
            count.text=board.Count+" / "+board.Capacity+" 개체  ·  덱 "+deckUnitIds.Length+"종";
            foreach(var button in summonButtons) button.interactable=board.Count<board.Capacity;
        }
        private int DeckIndex(string id) => Array.IndexOf(deckUnitIds,id);

        private RectTransform Panel(string name,Transform parent,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h); go.GetComponent<Image>().color=color;
            return rect;
        }
        private Text Label(string text,Transform parent,float x,float y,float w,float h,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var label=go.GetComponent<Text>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=alignment;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap; label.verticalOverflow=VerticalWrapMode.Truncate;
            return label;
        }
        private Button MakeButton(string text,Transform parent,float x,float y,float w,float h,Color color,UnityEngine.Events.UnityAction action)
        {
            var rect=Panel(text,parent,x,y,w,h,color); var button=rect.gameObject.AddComponent<Button>();
            button.targetGraphic=rect.GetComponent<Image>(); var tint=button.colors; tint.disabledColor=new Color(.35f,.35f,.35f,.5f);button.colors=tint;
            Label(text,rect,6,4,w-12,h-8,28,Color.white,TextAnchor.MiddleCenter);button.onClick.AddListener(action);return button;
        }
        private void OnDestroy()
        {
            if(board!=null) board.Changed-=Refresh;
            if(createdEventSystem && eventSystemObject!=null) Destroy(eventSystemObject);
            if(font!=null) Destroy(font);
        }
    }
}
