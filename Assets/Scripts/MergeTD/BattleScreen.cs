using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace MergeTD
{
    public sealed class BattleScreen:MonoBehaviour
    {
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private RectTransform board,attackRing,supportRing;
        [SerializeField] private Button[] cells,deckButtons,upgradeButtons;
        [SerializeField] private Image[] cellIcons,enemyIcons,shotIcons,beamImages;
        [SerializeField] private Text[] cellLabels,enemyLabels,deckLabels,upgradeLabels;
        [SerializeField] private UnitLevelIndicator[] cellLevels,deckLevels;
        [SerializeField] private Text headline,instruction,selectedLabel,resultLabel;
        [SerializeField] private Text cashLabel,livesLabel;
        [SerializeField] private Button startButton,speedButton,returnButton;
        [SerializeField] private Button deploymentButton;
        [SerializeField] private GameObject upgradeRoot,placementHelp;
        [SerializeField] private Text[] upgradeDescriptions,upgradeStates;
        [SerializeField] private Image[] upgradeSteps,enemyHealth;
        [SerializeField] private Sprite[] enemySprites;
        [SerializeField] private GameObject selectionRoot,pauseRoot;
        [SerializeField] private Image towerPortrait;
        [SerializeField] private Image[] deckPortraits;
        [SerializeField] private Button targetButton,settingsButton,resumeButton;
        private DefenseBattle battle;
        private MergeGameDatabase database;private CraftOwnedUnit[] deck;private Sprite[] icons;
        private DefenseBattle.Tower selected;private int selectedDeck;private float drawClock,speed=1;
        private Func<int,bool> reward;private Action closed;private bool claimed,paused;
        private bool placing=true;private int hovered=-1;
        [SerializeField, Min(1)] private float tileSize=48;
        private float Tile=>tileSize;
        public DefenseBattle Simulation=>battle;
        private void Awake()
        {
            for(int i=0;i<cells.Length;i++){int index=i;cells[i].onClick.AddListener(()=>ClickCell(index));}
            for(int i=0;i<deckButtons.Length;i++){int index=i;deckButtons[i].onClick.AddListener(()=>{if(deck!=null&&index<deck.Length){selectedDeck=index;selected=null;placing=true;hovered=-1;instruction.text=database.GetUnit(deck[index].unitId).name+" 선택 · 빈 칸 위에서 사거리 미리보기";Draw();}});}
            for(int i=0;i<upgradeButtons.Length;i++){int index=i;upgradeButtons[i].onClick.AddListener(()=>{if(battle.Upgrade(selected,index,out var reason))instruction.text="특성 구매 완료";else instruction.text=reason;Draw();});}
            startButton.onClick.AddListener(()=>{if(battle==null||battle.Finished)return;if(!battle.Running){battle.Start();paused=false;instruction.text="아군을 선택해 강화 · 유닛 목록에서 추가 배치";}else paused=!paused;Draw();});
            speedButton.onClick.AddListener(()=>{speed=speed==1?2:1;speedButton.GetComponentInChildren<Text>().text=speed+"×";});
            returnButton.onClick.AddListener(ReturnToWorkshop);
            if(deploymentButton!=null)deploymentButton.onClick.AddListener(()=>{selected=null;placing=true;hovered=-1;Draw();});
            if(settingsButton!=null)settingsButton.onClick.AddListener(()=>{if(battle==null)return;paused=!paused;Draw();});
            if(resumeButton!=null)resumeButton.onClick.AddListener(()=>{paused=false;Draw();});
            if(targetButton!=null)targetButton.onClick.AddListener(()=>{if(selected==null||battle.Finished)return;selected.targetMode=(selected.targetMode+1)%4;selected.charge=0;selected.cooldown=0;selected.target=null;Draw();});
        }
        public void Open(MergeGameDatabase db,GameContent content,CraftOwnedUnit[] deck,Sprite[] icons,Func<int,bool> reward,Action closed)
        {
            if(deck==null||deck.Length<1||deck.Length>5)throw new ArgumentException("Battle requires 1-5 unit types");
            database=db;this.deck=deck;this.icons=icons;this.reward=reward;this.closed=closed;battle=new DefenseBattle(db,content);selected=null;selectedDeck=0;speed=1;claimed=paused=false;placing=true;hovered=-1;
            gameObject.SetActive(true);instruction.text="오른쪽 유닛 선택 → 배치 → ▶ 전투 시작";resultLabel.text="";speedButton.GetComponentInChildren<Text>().text="1×";Draw();
        }
        private void ClickCell(int index)
        {
            if(battle==null||battle.Finished)return;int x=index%DefenseBattle.Columns,y=index/DefenseBattle.Columns;
            var existing=battle.Towers.FirstOrDefault(t=>t.Alive&&Mathf.RoundToInt(t.position.x)==x&&Mathf.RoundToInt(t.position.y)==y);
            if(existing!=null){selected=existing;placing=false;hovered=-1;instruction.text="아군 선택 · 오른쪽에서 특성을 강화하세요";Draw();return;}
            if(!placing){selected=null;placing=true;Draw();return;}
            var choice=deck[selectedDeck];if(battle.Deploy(choice.unitId,choice.level,new Vector2(x,y),out var reason)){selected=battle.Towers.Last();placing=false;hovered=-1;instruction.text="배치 완료 · 오른쪽에서 특성을 강화하세요";}else instruction.text=reason;Draw();
        }
        public void PreviewCell(int index){if(battle==null||!placing||battle.Finished)return;hovered=index;Draw();}
        public void ClearPreview(int index){if(hovered!=index)return;hovered=-1;Draw();}
        private bool CanPlace(int index)
        {
            int x=index%DefenseBattle.Columns,y=index/DefenseBattle.Columns;
            return battle!=null&&!battle.Finished&&!battle.IsPath(x,y)&&!battle.Towers.Any(t=>t.Alive&&Mathf.RoundToInt(t.position.x)==x&&Mathf.RoundToInt(t.position.y)==y)&&battle.Cash>=DeployCost();
        }
        private int DeployCost()=>(int)battle.Content.Rules["deployCost"]*database.GetUnit(deck[selectedDeck].unitId).tier;
        private DefenseBattle.Tower PreviewTower()=>new DefenseBattle.Tower{id=deck[selectedDeck].unitId,level=deck[selectedDeck].level,tier=database.GetUnit(deck[selectedDeck].unitId).tier,definition=database.GetUnit(deck[selectedDeck].unitId),profile=battle.Content.Profile(deck[selectedDeck].unitId)};
        private void ReturnToWorkshop()
        {
            if(battle==null)return;
            if(battle.Finished&&!claimed){int amount=RewardAmount();if(!reward(amount)){instruction.text="보상 저장 실패 · 복귀 버튼으로 다시 시도하세요";return;}claimed=true;}
            gameObject.SetActive(false);battle=null;closed?.Invoke();
        }
        private int RewardAmount()=>battle==null?0:(battle.Won?120:25)+battle.Wave*10+battle.Kills*2;
        private Vector2 Point(Vector2 p)=>new Vector2((p.x+.5f)*Tile,-(p.y+.5f)*Tile);
        private void Update()
        {
            if(scaler!=null)scaler.matchWidthOrHeight=Screen.width/(float)Mathf.Max(1,Screen.height)<scaler.referenceResolution.x/scaler.referenceResolution.y?0f:1f;
            if(battle==null)return;if(!paused){float remaining=Mathf.Min(UnityEngine.Time.deltaTime,.1f)*speed;while(remaining>0){float step=Mathf.Min(.05f,remaining);battle.Tick(step);remaining-=step;}}drawClock-=UnityEngine.Time.deltaTime;if(drawClock<=0){drawClock=.05f;Draw();}
        }
        private void Draw()
        {
            if(battle==null)return;headline.text="라운드 "+battle.Wave+" / "+battle.TotalWaves;if(cashLabel!=null)cashLabel.text=battle.Cash.ToString("N0");if(livesLabel!=null)livesLabel.text=Mathf.Max(0,battle.Lives).ToString();
            startButton.GetComponentInChildren<Text>().text=battle.Finished?"종료":!battle.Running||paused?"▶":"Ⅱ";
            startButton.interactable=!battle.Finished;
            for(int i=0;i<cells.Length;i++){
                int x=i%DefenseBattle.Columns,y=i/DefenseBattle.Columns;bool path=battle.IsPath(x,y);var u=battle.Towers.FirstOrDefault(t=>t.Alive&&Mathf.RoundToInt(t.position.x)==x&&Mathf.RoundToInt(t.position.y)==y);
                var baseColor=u==selected&&u!=null?new Color(.10f,.35f,.23f,.38f):Color.clear;
                cells[i].GetComponent<Image>().color=placing&&i==hovered?(CanPlace(i)?new Color(.15f,.7f,.3f,.38f):new Color(.9f,.15f,.10f,.48f)):baseColor;
                cellIcons[i].enabled=u!=null;
                if(i<cellLevels.Length)cellLevels[i].SetLevel(u==null?0:u.level);
                if(u!=null){cellIcons[i].sprite=icons[MainCraftingScreen.RoleIndex(u.definition)];cellIcons[i].color=MainCraftingScreen.TierColor(u.tier);cellLabels[i].text=u==selected||u.hp<u.maxHp?"HP "+Mathf.CeilToInt(u.hp)+(u.shield>0?" +":""):(u.definition.name.Length>4?u.definition.name.Substring(0,4):u.definition.name);}
                else cellLabels[i].text=x==0&&y==2?"입구 →":x==3&&y==5?"출구":"";
                cellLabels[i].color=new Color(.12f,.20f,.10f);
            }
            var enemies=battle.Enemies.Where(e=>e.Alive).Take(enemyIcons.Length).ToArray();
            for(int i=0;i<enemyIcons.Length;i++){
                bool on=i<enemies.Length;enemyIcons[i].gameObject.SetActive(on);if(!on)continue;var e=enemies[i];int variant=e.boss?2:e.serial%5==0?1:0;float size=e.boss?64:variant==1?42:38;
                enemyIcons[i].rectTransform.anchoredPosition=Point(e.position);enemyIcons[i].color=Color.white;enemyIcons[i].rectTransform.sizeDelta=Vector2.one*size;
                if(enemySprites!=null&&enemySprites.Length==3)enemyIcons[i].sprite=enemySprites[variant];
                enemyLabels[i].text=e.boss?"BOSS":e.slowUntil>battle.Time?"둔화":"";enemyLabels[i].color=e.slowUntil>battle.Time?new Color(.1f,.4f,.7f):new Color(.4f,.2f,.55f);
                enemyLabels[i].rectTransform.sizeDelta=new Vector2(46,14);enemyLabels[i].rectTransform.anchoredPosition=new Vector2((size-46)/2,18);
                if(i<enemyHealth.Length){var bar=enemyHealth[i];bar.transform.parent.gameObject.SetActive(e.hp<e.maxHp||e.boss);((RectTransform)bar.transform.parent).anchoredPosition=new Vector2((size-32)/2,4);bar.rectTransform.sizeDelta=new Vector2(32*Mathf.Clamp01(e.hp/e.maxHp),3);}
            }
            for(int i=0;i<shotIcons.Length;i++){
                bool on=i<battle.Shots.Count;shotIcons[i].gameObject.SetActive(on);if(on){var s=battle.Shots[i];shotIcons[i].rectTransform.anchoredPosition=Point(s.position);shotIcons[i].color=MainCraftingScreen.TierColor(s.owner.tier);}
            }
            for(int i=0;i<beamImages.Length;i++){
                bool on=i<battle.Flashes.Count;beamImages[i].gameObject.SetActive(on);if(on){var f=battle.Flashes[i];var from=Point(f.from);var delta=Point(f.to)-from;var rt=beamImages[i].rectTransform;rt.anchoredPosition=from;rt.sizeDelta=new Vector2(delta.magnitude,3);rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);beamImages[i].color=new Color(f.color.r,f.color.g,f.color.b,Mathf.Clamp01(f.life/.15f));}
            }
            for(int i=0;i<deckButtons.Length;i++){
                deckButtons[i].gameObject.SetActive(i<deck.Length);if(i>=deck.Length)continue;var u=database.GetUnit(deck[i].unitId);if(i<deckLevels.Length)deckLevels[i].SetLevel(deck[i].level);int cost=(int)battle.Content.Rules["deployCost"]*u.tier;deckLabels[i].text=u.name+"\n"+cost;deckLabels[i].color=battle.Cash>=cost?Color.white:new Color(1,.48f,.4f);deckButtons[i].GetComponent<Image>().color=placing&&i==selectedDeck?new Color(.34f,.53f,.38f):new Color(.27f,.28f,.23f);deckButtons[i].interactable=!battle.Finished;
                if(i<deckPortraits.Length){deckPortraits[i].sprite=icons[MainCraftingScreen.RoleIndex(u)];deckPortraits[i].color=battle.Cash>=cost?MainCraftingScreen.TierColor(u.tier):new Color(.38f,.38f,.38f);}
            }
            if(selected!=null&&!selected.Alive){selected=null;placing=true;}
            bool has=selected!=null;if(upgradeRoot!=null)upgradeRoot.SetActive(has);if(placementHelp!=null)placementHelp.SetActive(!has);if(deploymentButton!=null)deploymentButton.interactable=has&&!battle.Finished;
            if(selectionRoot!=null)selectionRoot.SetActive(has);
            if(has&&towerPortrait!=null){towerPortrait.sprite=icons[MainCraftingScreen.RoleIndex(selected.definition)];towerPortrait.color=MainCraftingScreen.TierColor(selected.tier);}
            if(targetButton!=null){targetButton.GetComponentInChildren<Text>().text="타겟: "+new[]{"기본","선두","강한 적","가까운 적"}[has?selected.targetMode:0];targetButton.interactable=has&&!battle.Finished;}
            if(pauseRoot!=null)pauseRoot.SetActive(paused||battle.Finished);if(resumeButton!=null)resumeButton.gameObject.SetActive(!battle.Finished);
            bool preview=placing&&hovered>=0&&!battle.Finished;var shown=has?selected:preview?PreviewTower():null;
            attackRing.gameObject.SetActive(shown!=null&&battle.AttackRange(shown)>0);supportRing.gameObject.SetActive(shown!=null&&battle.SupportRange(shown)>0);
            if(shown!=null){var position=has?shown.position:new Vector2(hovered%DefenseBattle.Columns,hovered/DefenseBattle.Columns);attackRing.anchoredPosition=supportRing.anchoredPosition=Point(position);attackRing.sizeDelta=Vector2.one*battle.AttackRange(shown)*Tile*2;supportRing.sizeDelta=Vector2.one*battle.SupportRange(shown)*Tile*2;var validColor=CanPlace(Mathf.Max(0,hovered))?new Color(.4f,1,.55f,.8f):new Color(1,.3f,.25f,.8f);attackRing.GetComponent<Image>().color=preview?validColor:new Color(1,.8f,.3f,.8f);supportRing.GetComponent<Image>().color=preview?validColor:new Color(.3f,1,.8f,.65f);}
            var info=has?selected:PreviewTower();selectedLabel.text=info.definition.name+"  T"+info.tier+" · Lv."+info.level+"\n"+(has?"HP "+Mathf.CeilToInt(info.hp)+" / "+Mathf.CeilToInt(info.maxHp):"배치 비용 "+DeployCost()+" 재화")+"\n공격 "+battle.AttackRange(info).ToString("0.0")+"칸 · 지원 "+battle.SupportRange(info).ToString("0.0")+"칸\n"+(has?"특성 "+string.Join(" / ",info.paths):preview?(CanPlace(hovered)?"배치 가능":"배치 불가 · 길 / 점유 / 재화 확인"):"빈 칸 위에서 사거리 미리보기");
            for(int i=0;i<upgradeButtons.Length;i++){
                if(!has){upgradeLabels[i].text=@"특성 경로 "+(char)('A'+i)+"\n아군 선택";upgradeButtons[i].interactable=false;continue;}
                var path=battle.Content.Path(selected.id,i);int current=selected.paths[i];bool allowed=battle.Content.CanUpgrade(selected.level,selected.paths,i,out var reason);int cost=current<5?battle.Content.UpgradeCost(selected.tier,current+1):0;
                int cap=battle.Content.Cap(selected.level);bool otherMain=selected.paths.Where((n,j)=>j!=i).Any(n=>n>2);if(otherMain)cap=Mathf.Min(cap,2);bool locked=current==0&&selected.paths.Count(n=>n>0)>=2;if(locked)cap=0;
                string state=allowed?(battle.Cash>=cost?cost+" 재화 · 강화":"재화 부족 · "+cost+" 필요"):locked?"잠김 · 두 경로 선택 완료":current>=5?"경로 완성":otherMain&&current>=2?"보조 경로 최대":selected.level<5?"다음 단계는 Lv."+(selected.level+1)+" 필요":"최대 단계";
                if(battle.Finished)state="전투 종료";
                upgradeLabels[i].text=(string)path["name"]+"  "+current+" / "+cap;
                if(i<upgradeDescriptions.Length)upgradeDescriptions[i].text=current>=5?"완성 · 해당 경로 효과 1.5배":GameContent.EffectDescription((string)path["archetype"],current+1).Replace("누적 ","다음 ");
                if(i<upgradeStates.Length){upgradeStates[i].text=state;upgradeStates[i].color=allowed&&battle.Cash>=cost?new Color(.65f,.95f,.63f):allowed?new Color(1,.48f,.4f):new Color(.9f,.76f,.48f);}
                for(int j=0;j<5&&i*5+j<upgradeSteps.Length;j++)upgradeSteps[i*5+j].color=j<current?new Color(1,.78f,.34f):j<cap?new Color(.38f,.48f,.47f):new Color(.13f,.18f,.19f);
                upgradeButtons[i].interactable=allowed&&battle.Cash>=cost&&!battle.Finished;
            }
            if(battle.Finished){resultLabel.text=(battle.Won?"방어 성공":"방어 실패")+" · 처치 "+battle.Kills+"\n공방 복귀 시 골드 +"+RewardAmount();returnButton.GetComponentInChildren<Text>().text="보상 받고 복귀";}
            else{returnButton.GetComponentInChildren<Text>().text="공방 복귀";resultLabel.text=paused?"일시 정지 · 전투 재개 또는 공방 복귀":"";}
            returnButton.gameObject.SetActive(true);
        }
    }
}


