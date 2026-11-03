using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MergeTD
{
    public sealed class MainCraftingScreen:MonoBehaviour
    {
        [SerializeField] private MergeGameDatabase database;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private CraftUnitView[] slots;
        [SerializeField] private Sprite[] roleIcons;
        [SerializeField] private Text summary,discovery,status,detail,pageLabel;
        [SerializeField] private Button spawnButton,previousButton,nextButton,collectionButton,recipesButton,helpButton,closeButton;
        [SerializeField] private GameObject modal;
        [SerializeField] private Text modalTitle,modalBody;
        [SerializeField] private RectTransform dragPreview;
        [SerializeField] private Image dragIcon;
        [SerializeField] private Text dragLabel;
        [SerializeField] private UnitLevelIndicator dragLevel;
        [SerializeField] private Button battleButton,deckButton,levelUpgradeButton,tierUpgradeButton;
        [SerializeField] private Text deckLabel;
        [SerializeField] private BattleScreen battleScreen;
        [SerializeField] private Button sortButton,discardButton;
        [SerializeField] private CraftTrashDropTarget trashTarget;
        private CraftInventory inventory;
        private CraftOwnedUnit[] owned=new CraftOwnedUnit[0];
        private readonly List<string> deck=new List<string>();
        private string inspected;private CraftUnitView dragging;private int page,pointerId;private bool saveReady,dropHandled,showingPermanent;
        private string pendingDiscard;
        private const string Hint="겹쳐 합성 · 빈 칸으로 이동 · 쓰레기통에 버리기";
        public static Color TierColor(int tier)=>tier==3?new Color(.78f,.57f,.96f):tier==2?new Color(.41f,.84f,.75f):new Color(.85f,.85f,.77f);
        public static int RoleIndex(UnitDefinition unit){var c=unit.GetComposition();int best=0;for(int i=1;i<c.Length;i++)if(c[i]>c[best])best=i;return best;}
        private void Start()
        {
            if(database==null||slots==null||slots.Length==0){Debug.LogError("씬 UI 참조를 연결하세요.");enabled=false;return;}
            database.Reload();var file=new CraftSaveFile(Path.Combine(Application.persistentDataPath,"main-crafting-v1.json"));inventory=new CraftInventory(database,file.Write);
            try{
                if(file.Exists)inventory.Restore(file.Read());else{
                    var initial=new List<CraftOwnedUnit>();foreach(var u in database.GetAllUnits().Where(u=>u.tier==1))for(int i=0;i<4;i++)initial.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),u.unitId,1,initial.Count));
                    var state=inventory.Export();state.units=initial.ToArray();file.Write(state);inventory.Restore(state);
                }saveReady=true;
            }catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException){status.text="저장 오류 · 원본을 보호하기 위해 잠겼습니다. "+ex.Message;Debug.LogError(status.text);}
            foreach(var slot in slots)slot.Owner=this;inventory.Changed+=Refresh;
            spawnButton.onClick.AddListener(Produce);previousButton.onClick.AddListener(()=>ChangePage(-1));nextButton.onClick.AddListener(()=>ChangePage(1));
            collectionButton.onClick.AddListener(ShowCollection);recipesButton.onClick.AddListener(ShowRecipes);helpButton.onClick.AddListener(ShowPermanent);closeButton.onClick.AddListener(()=>{modal.SetActive(false);showingPermanent=false;});
            battleButton.onClick.AddListener(StartBattle);deckButton.onClick.AddListener(ToggleDeck);levelUpgradeButton.onClick.AddListener(()=>BuyPermanent(false));tierUpgradeButton.onClick.AddListener(()=>BuyPermanent(true));
            if(sortButton!=null)sortButton.onClick.AddListener(SortInventory);
            if(discardButton!=null)discardButton.onClick.AddListener(ConfirmDiscard);
            if(trashTarget!=null)trashTarget.Owner=this;
            modal.SetActive(false);dragPreview.gameObject.SetActive(false);owned=inventory.GetOwnedUnits();foreach(var u in owned)if(!deck.Contains(u.unitId)&&deck.Count<5)deck.Add(u.unitId);Refresh();if(saveReady)status.text=Hint;
        }
        private void Refresh()
        {
            owned=inventory.GetOwnedUnits();deck.RemoveAll(id=>!owned.Any(u=>u.unitId==id));page=Mathf.Clamp(page,0,Mathf.Max(0,(inventory.SlotCount-1)/slots.Length));
            for(int i=0;i<slots.Length;i++){int index=page*slots.Length+i;var item=owned.FirstOrDefault(u=>u.slot==index);var def=item==null?null:database.GetUnit(item.unitId);slots[i].Bind(item,def,def==null?null:roleIcons[RoleIndex(def)]);}
            summary.text="보유 "+owned.Length+" · 골드 "+inventory.Gold;discovery.text="발견 "+inventory.DiscoveredUnitCount+"/90 · 제작법 "+inventory.DiscoveredCount;
            pageLabel.text=(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(inventory.SlotCount/(float)slots.Length));previousButton.interactable=page>0;nextButton.interactable=(page+1)*slots.Length<inventory.SlotCount;spawnButton.interactable=saveReady;battleButton.interactable=saveReady&&owned.Length>0;
            if(sortButton!=null)sortButton.interactable=saveReady&&owned.Length>0;
            deckLabel.text="출전 "+deck.Count+"/5종 · "+string.Join(" / ",deck.Select(id=>database.GetUnit(id).name));deckButton.interactable=saveReady&&owned.Any(u=>u.instanceId==inspected);
            if(inspected!=null&&!owned.Any(u=>u.instanceId==inspected)){inspected=null;detail.text="유닛을 눌러 능력을 확인하세요";}
            if(showingPermanent)RefreshPermanent();
        }
        public void Inspect(CraftUnitView view)
        {
            if(view.Item==null)return;inspected=view.Item.instanceId;var u=database.GetUnit(view.Item.unitId);int level=view.Item.level;
            detail.text=u.name+" T"+u.tier+" Lv."+level+" · 최대 "+level+"/"+Mathf.Min(level,2)+"/0\n"+u.skillName+": "+u.skillDescription;deckButton.interactable=saveReady;
            deckButton.GetComponentInChildren<Text>().text=deck.Contains(u.unitId)?"덱 제외":"덱 추가";
        }
        public void BeginDrag(CraftUnitView source,PointerEventData e)
        {
            if(!saveReady||source.Item==null||dragging!=null||e.button!=PointerEventData.InputButton.Left)return;dragging=source;pointerId=e.pointerId;dropHandled=false;Inspect(source);
            dragIcon.sprite=source.Icon.sprite;dragIcon.color=source.Icon.color;dragLabel.text=database.GetUnit(source.Item.unitId).name;if(dragLevel!=null)dragLevel.SetLevel(source.Item.level);dragPreview.gameObject.SetActive(true);dragPreview.SetAsLastSibling();
            foreach(var slot in slots)slot.Highlight(CanDrop(source,slot),slot==source);status.text="빈 칸: 이동 · 빛나는 유닛: 합성 · 쓰레기통: 버리기";MoveDrag(e);
        }
        public void MoveDrag(PointerEventData e){if(dragging==null||e.pointerId!=pointerId)return;if(RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragPreview.parent,e.position,null,out var point))dragPreview.anchoredPosition=point;}
        private bool CanDrop(CraftUnitView a,CraftUnitView b)=>a!=null&&b!=null&&a!=b&&a.Item!=null&&(b.Item==null||inventory.CanCraft(a.Item.instanceId,b.Item.instanceId,out _,out _,out _));
        public void Drop(CraftUnitView target,PointerEventData e)
        {
            if(dragging==null||e.pointerId!=pointerId||dropHandled)return;dropHandled=true;
            int local=Array.IndexOf(slots,target);if(local<0){status.text="취소";return;}
            string a=dragging.Item.instanceId,b=target.Item?.instanceId;int destination=page*slots.Length+local;CancelDrag();
            if(b==null){inventory.TryMove(a,destination,out var moved);status.text=moved;return;}
            if(inventory.TryCraft(a,b,out _,out var message)){Refresh();Inspect(slots[local]);}
            status.text=message;
        }
        public void EndDrag(PointerEventData e){if(dragging==null||e.pointerId!=pointerId)return;if(!dropHandled)status.text="취소 · "+Hint;CancelDrag();}
        private void CancelDrag(){dragging=null;if(dragPreview!=null)dragPreview.gameObject.SetActive(false);if(slots!=null)foreach(var s in slots)if(s!=null)s.Highlight(false,false);}
        private void ChangePage(int direction){CancelDrag();page+=direction;Refresh();}
        private void Produce(){if(!saveReady)return;CancelDrag();if(inventory.TryProduce(Mathf.Min(UnityEngine.Random.value,.999999f),Mathf.Min(UnityEngine.Random.value,.999999f),Mathf.Min(UnityEngine.Random.value,.999999f),out var message)){page=inventory.GetOwnedUnits().Last().slot/slots.Length;Refresh();}status.text=message;}
        private void SortInventory(){if(!saveReady)return;CancelDrag();if(inventory.TrySort(out var message)){page=0;Refresh();}status.text=message;}
        public bool IsDragging=>dragging!=null;
        public void DropTrash(PointerEventData e)
        {
            if(!saveReady||dragging==null||e.pointerId!=pointerId||dropHandled)return;
            dropHandled=true;var item=dragging.Item;CancelDrag();if(item==null)return;
            Open("유닛 버리기",database.GetUnit(item.unitId).name+" · Lv."+item.level+"\n\n이 유닛 한 개를 삭제할까요?\n삭제하면 복구할 수 없습니다.\n발견한 유닛과 제작법 기록은 유지됩니다.");
            pendingDiscard=item.instanceId;discardButton.gameObject.SetActive(true);
        }
        private void ConfirmDiscard()
        {
            if(!saveReady||string.IsNullOrEmpty(pendingDiscard))return;
            if(inventory.TryDiscard(pendingDiscard,out var message)){modal.SetActive(false);pendingDiscard=null;discardButton.gameObject.SetActive(false);}
            else modalBody.text=message+"\n\n다시 시도하거나 닫기를 누르세요.";
            status.text=message;
        }
        private void ToggleDeck(){var u=owned.FirstOrDefault(x=>x.instanceId==inspected);if(u==null)return;if(deck.Contains(u.unitId))deck.Remove(u.unitId);else if(deck.Count<5)deck.Add(u.unitId);else{status.text="덱은 중복 없이 최대 5종입니다";return;}Refresh();status.text="출전 덱 변경 완료";}
        private void Open(string title,string body){CancelDrag();pendingDiscard=null;showingPermanent=false;modalTitle.text=title;modalBody.text=body;levelUpgradeButton.gameObject.SetActive(false);tierUpgradeButton.gameObject.SetActive(false);if(discardButton!=null)discardButton.gameObject.SetActive(false);modal.SetActive(true);}
        private void ShowCollection(){Open("발견한 유닛",string.Join("\n",database.GetAllUnits().Where(u=>inventory.IsUnitDiscovered(u.unitId)).Select(u=>"T"+u.tier+" "+u.name).Take(14))+"\n\n발견 기록은 유닛을 소모해도 유지됩니다.");}
        private void ShowRecipes(){Open("성장 / 합성 규칙","동일 유닛 + 동일 Lv. → Lv.+1\n최대 성장 Lv.5\n\nLv.5 + Lv.5 → 제작식의 상위 티어 Lv.1\n최고 제작 티어 T3\n\n특성 한도\nLv.1: 1/1/0 · Lv.2: 2/2/0\nLv.3: 3/2/0 · Lv.4: 4/2/0\nLv.5: 5/2/0\n\n유닛 선택 → 덱 추가 → 전투 입장");}
        private void ShowPermanent(){Open("영구 업그레이드","");showingPermanent=true;levelUpgradeButton.gameObject.SetActive(true);tierUpgradeButton.gameObject.SetActive(true);RefreshPermanent();}
        private void RefreshPermanent()
        {
            var t=inventory.TierProbabilities();var l=inventory.LevelProbabilities();
            modalBody.text="보유 골드 "+inventory.Gold+"\n\n[제작 티어 확률] 강화 "+inventory.TierUpgrade+"/10\nT1 "+t[0].ToString("P0")+" · T2 "+t[1].ToString("P0")+" · T3 "+t[2].ToString("P0")+"\n발견한 상위 유닛만 등장합니다.\n\n[시작 레벨 확률] 강화 "+inventory.LevelUpgrade+"/10\n"+string.Join(" / ",l.Select((p,i)=>"L"+(i+1)+" "+p.ToString("P0")))+"\n\n전투 보상으로 골드를 획득합니다.\n현재 제작은 무료 프로토타입 규칙입니다.";
            levelUpgradeButton.GetComponentInChildren<Text>().text="시작 레벨 강화\n"+(inventory.UpgradeCost(false)>0?inventory.UpgradeCost(false)+" 골드":"최대 단계");tierUpgradeButton.GetComponentInChildren<Text>().text="티어 확률 강화\n"+(inventory.UpgradeCost(true)>0?inventory.UpgradeCost(true)+" 골드":"최대 단계");
            levelUpgradeButton.interactable=saveReady&&inventory.UpgradeCost(false)>0&&inventory.Gold>=inventory.UpgradeCost(false);tierUpgradeButton.interactable=saveReady&&inventory.UpgradeCost(true)>0&&inventory.Gold>=inventory.UpgradeCost(true);
        }
        private void BuyPermanent(bool tier){if(!saveReady)return;inventory.TryUpgradePermanent(tier,out var message);status.text=message;RefreshPermanent();}
        private void StartBattle()
        {
            if(!saveReady||battleScreen==null)return;CancelDrag();modal.SetActive(false);
            if(deck.Count==0)foreach(var u in owned)if(!deck.Contains(u.unitId)&&deck.Count<5)deck.Add(u.unitId);
            var selected=deck.Select(id=>owned.Where(u=>u.unitId==id).OrderByDescending(u=>u.level).First()).Select(u=>new CraftOwnedUnit(u.instanceId,u.unitId,u.level)).ToArray();
            canvas.enabled=false;battleScreen.Open(database,inventory.Content,selected,roleIcons,amount=>{if(!inventory.TryReward(amount,out var message)){status.text=message;return false;}return true;},()=>{canvas.enabled=true;Refresh();status.text="공방으로 복귀했습니다";});
        }
        private void Update(){if(scaler!=null)scaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)<scaler.referenceResolution.x/scaler.referenceResolution.y?0f:1f;}
        private void OnApplicationFocus(bool focus){if(!focus)CancelDrag();}
        private void OnDisable(){CancelDrag();}
        private void OnDestroy(){if(inventory!=null)inventory.Changed-=Refresh;}
    }
}

