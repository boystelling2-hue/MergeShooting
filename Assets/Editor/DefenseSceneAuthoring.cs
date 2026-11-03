using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using MergeTD;
using static LandscapeSceneAuthoring;

public static class DefenseSceneAuthoring
{
    public static BattleScreen Build(MergeGameDatabase db)
    {
        var root=new GameObject("Battle UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(BattleScreen));
        Undo.RegisterCreatedObjectUndo(root,"Author defense UI");root.GetComponent<Canvas>().sortingOrder=10;
        var screen=root.GetComponent<BattleScreen>();var frame=Frame(root,"Battle Landscape Layout");
        var light=new Color(.88f,.93f,.9f);var gold=new Color(1,.78f,.34f);
        var heart=Panel("Lives Icon",frame,20,12,28,28,new Color(1,.3f,.26f)).GetComponent<Image>();heart.sprite=DefenseVisualAssets.Sprite("Heart");heart.raycastTarget=false;
        var livesLabel=Label("Lives Counter","20",frame,58,8,86,36,23,TextAnchor.MiddleLeft);
        var coin=Panel("Cash Icon",frame,156,12,28,28,new Color(1,.79f,.25f)).GetComponent<Image>();coin.sprite=DefenseVisualAssets.Sprite("Coin");coin.raycastTarget=false;
        var cashLabel=Label("Cash Counter","350",frame,194,8,260,36,23,TextAnchor.MiddleLeft);
        var headline=Label("Battle Status","라운드 0 / 10",frame,870,8,205,36,19,TextAnchor.MiddleRight);
        var instruction=Label("Instructions","유닛 구매 → 전장 배치",frame,290,688,780,24,14,TextAnchor.MiddleRight);
        var board=Panel("Battlefield",frame,4,54,1080,660,Color.white);board.GetComponent<Image>().sprite=DefenseArtAssets.Import("Meadow-v1");board.gameObject.AddComponent<RectMask2D>();
        var terrainSimulation=new DefenseBattle(db,new GameContent());
        var road=Panel("Painted Enemy Route",board,0,0,1080,660,Color.white).GetComponent<Image>();road.sprite=DefenseArtAssets.Road(terrainSimulation);road.raycastTarget=false;
        Panel("Tower Shop",frame,1092,0,188,720,new Color(.25f,.21f,.14f));
        Label("Shop Heading","출전 유닛",frame,1100,62,172,28,19).color=gold;
        var settings=Button("Battle Settings","설정",frame,1208,8,62,36,15);
        var selectionRoot=Panel("Unit Control Panel",frame,8,64,252,612,new Color(.11f,.18f,.17f));
        Label("Selection Heading","선택한 유닛",selectionRoot,14,10,185,26,18,TextAnchor.MiddleLeft).color=gold;
        var deselect=Button("Deployment Mode","×",selectionRoot,214,8,28,28,14);
        var portrait=Panel("Tower Portrait",selectionRoot,82,47,88,88,Color.white).GetComponent<Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;
        var selected=Label("Selected Unit","",selectionRoot,16,144,220,78,14,TextAnchor.UpperLeft);
        var targeting=Button("Target Priority","타겟: 기본",selectionRoot,16,226,220,32,15);
        var upgradeRoot=Panel("Upgrade List",selectionRoot,16,270,220,294,new Color(0,0,0,0));upgradeRoot.GetComponent<Image>().raycastTarget=false;
        var upgradeButtons=new List<Button>();var upgradeLabels=new List<Text>();var descriptions=new List<Text>();var states=new List<Text>();var upgradePips=new List<Image>();
        for(int p=0;p<3;p++)
        {
            var b=Button("Upgrade "+(char)('A'+p),"특성 경로",upgradeRoot,0,p*100,220,94,14);
            var title=b.GetComponentInChildren<Text>();title.alignment=TextAnchor.MiddleLeft;title.rectTransform.anchoredPosition=new Vector2(43,-5);title.rectTransform.sizeDelta=new Vector2(170,25);
            var pathIcon=Panel("Path Icon",b.transform,8,8,28,28,gold).GetComponent<Image>();pathIcon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Workshop/Role"+(p==0?0:p==1?1:4)+".png");pathIcon.preserveAspect=true;pathIcon.raycastTarget=false;
            var effect=Label("Next Effect","다음 단계 효과",b.transform,10,44,200,27,11,TextAnchor.UpperLeft);effect.resizeTextForBestFit=true;effect.resizeTextMinSize=9;effect.resizeTextMaxSize=11;
            var state=Label("Upgrade State","유닛 선택",b.transform,10,73,200,17,12,TextAnchor.MiddleLeft);
            for(int j=0;j<5;j++){var pip=Panel("Path Step "+(j+1),b.transform,44+j*17,32,13,5,light).GetComponent<Image>();pip.raycastTarget=false;upgradePips.Add(pip);}
            upgradeButtons.Add(b);upgradeLabels.Add(title);descriptions.Add(effect);states.Add(state);
        }
        var placementHelp=Label("Deployment Help","유닛 선택 후 배치\n\n초록: 배치 가능\n빨강: 배치 불가",frame,1102,435,168,100,13);
        var start=Button("Start or Pause","▶",frame,1100,636,100,68,30);
        var speed=Button("Battle Speed","1×",frame,1206,636,66,68,20);
        var pauseRoot=Panel("Battle Menu",frame,0,0,1280,720,new Color(0,0,0,.65f));
        var menuCard=Panel("Menu Card",pauseRoot,340,172,440,344,new Color(.06f,.1f,.1f,.98f));
        Label("Menu Heading","전투 메뉴",menuCard,30,20,380,42,26);
        var result=Label("Battle Result","",menuCard,30,80,380,82,18);
        var resume=Button("Resume Battle","계속 진행",menuCard,30,178,380,54,20);
        var back=Button("Return","공방 복귀",menuCard,30,252,380,54,20);pauseRoot.gameObject.SetActive(false);
        var cells=new List<Button>();var icons=new List<Image>();var labels=new List<Text>();var levels=new List<UnitLevelIndicator>();
        const float tile=60;
        for(int y=0;y<DefenseBattle.Rows;y++)for(int x=0;x<DefenseBattle.Columns;x++)
        {
            var b=Button("Cell "+x+","+y,"",board,x*tile,y*tile,60,60,9);
            b.GetComponent<Image>().color=Color.clear;
            var text=b.GetComponentInChildren<Text>();text.rectTransform.anchoredPosition=new Vector2(0,-34);text.rectTransform.sizeDelta=new Vector2(60,18);text.fontSize=9;
            text.text=x==0&&y==2?"입구":x==3&&y==5?"출구":"";
            var icon=Panel("Unit Icon",b.transform,12,4,30,30,light).GetComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;icon.enabled=false;
            var unitShadow=icon.gameObject.AddComponent<Shadow>();unitShadow.effectColor=new Color(.07f,.15f,.08f,.85f);unitShadow.effectDistance=new Vector2(1.2f,-1.2f);
            var lv=LevelIndicator(b.transform,60,60,true);lv.SetLevel(0);
            var hover=b.gameObject.AddComponent<BattleCellHover>();var hs=new SerializedObject(hover);Bind(hs,"screen",screen);hs.FindProperty("index").intValue=y*DefenseBattle.Columns+x;hs.ApplyModifiedPropertiesWithoutUndo();
            cells.Add(b);icons.Add(icon);labels.Add(text);levels.Add(lv);
        }
        var ring=DefenseVisualAssets.Sprite("RangeThin");
        var attack=Panel("Attack Range",board,0,0,150,150,gold);attack.pivot=new Vector2(.5f,.5f);attack.GetComponent<Image>().sprite=ring;attack.GetComponent<Image>().raycastTarget=false;attack.gameObject.SetActive(false);
        var support=Panel("Support Range",board,0,0,150,150,new Color(.3f,1,.8f,.65f));support.pivot=new Vector2(.5f,.5f);support.GetComponent<Image>().sprite=ring;support.GetComponent<Image>().raycastTarget=false;support.gameObject.SetActive(false);
        var enemies=new List<Image>();var enemyLabels=new List<Text>();var hp=new List<Image>();var normalEnemy=DefenseArtAssets.Import("AmberSlime-v1");
        for(int i=0;i<32;i++)
        {
            var rt=Panel("Enemy "+i,board,0,0,36,36,Color.white);rt.pivot=new Vector2(.5f,.5f);var image=rt.GetComponent<Image>();image.sprite=normalEnemy;image.preserveAspect=true;image.raycastTarget=false;
            var label=Label("Enemy Rank","",rt,-5,-15,34,14,9);label.color=gold;
            var track=Panel("Health Track",rt,2,-4,32,3,new Color(.06f,.09f,.08f));track.GetComponent<Image>().raycastTarget=false;
            var fill=Panel("Health Fill",track,0,0,32,3,new Color(.55f,.88f,.4f)).GetComponent<Image>();fill.raycastTarget=false;
            enemies.Add(image);enemyLabels.Add(label);hp.Add(fill);rt.gameObject.SetActive(false);
        }
        var shots=new List<Image>();for(int i=0;i<96;i++){var rt=Panel("Projectile "+i,board,0,0,6,6,gold);rt.pivot=new Vector2(.5f,.5f);var im=rt.GetComponent<Image>();im.sprite=DefenseVisualAssets.Sprite("Orb");im.raycastTarget=false;shots.Add(im);rt.gameObject.SetActive(false);}
        var beams=new List<Image>();for(int i=0;i<64;i++){var rt=Panel("Attack Trace "+i,board,0,0,48,2,gold);rt.pivot=new Vector2(0,.5f);var im=rt.GetComponent<Image>();im.raycastTarget=false;beams.Add(im);rt.gameObject.SetActive(false);}
        var decks=new List<Button>();var deckLabels=new List<Text>();var deckLevels=new List<UnitLevelIndicator>();
        var deckPortraits=new List<Image>();
        for(int i=0;i<5;i++){var b=Button("Deck "+(i+1),"출전 유닛",frame,1100+i%2*88,104+i/2*106,84,98,12);var label=b.GetComponentInChildren<Text>();label.rectTransform.anchoredPosition=new Vector2(4,-56);label.rectTransform.sizeDelta=new Vector2(76,29);label.resizeTextForBestFit=true;label.resizeTextMinSize=9;label.resizeTextMaxSize=12;var icon=Panel("Shop Portrait",b.transform,24,28,36,27,Color.white).GetComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;deckPortraits.Add(icon);decks.Add(b);deckLabels.Add(label);deckLevels.Add(LevelIndicator(b.transform,84,98));}
        var so=new SerializedObject(screen);
        ArrayBind(so,"enemySprites",new List<Sprite>{DefenseArtAssets.Import("AmberSlime-v1"),DefenseArtAssets.Import("CyanRunner-v1"),DefenseArtAssets.Import("AmethystGolem-v1")});
        Bind(so,"cashLabel",cashLabel);Bind(so,"livesLabel",livesLabel);
        Bind(so,"selectionRoot",selectionRoot.gameObject);Bind(so,"towerPortrait",portrait);Bind(so,"targetButton",targeting);Bind(so,"settingsButton",settings);Bind(so,"resumeButton",resume);Bind(so,"pauseRoot",pauseRoot.gameObject);ArrayBind(so,"deckPortraits",deckPortraits);
        Bind(so,"scaler",root.GetComponent<CanvasScaler>());Bind(so,"board",board);Bind(so,"attackRing",attack);Bind(so,"supportRing",support);Bind(so,"headline",headline);Bind(so,"instruction",instruction);Bind(so,"selectedLabel",selected);Bind(so,"resultLabel",result);Bind(so,"startButton",start);Bind(so,"speedButton",speed);Bind(so,"returnButton",back);Bind(so,"deploymentButton",deselect);Bind(so,"upgradeRoot",upgradeRoot.gameObject);Bind(so,"placementHelp",placementHelp.gameObject);so.FindProperty("tileSize").floatValue=tile;
        ArrayBind(so,"cells",cells);ArrayBind(so,"cellIcons",icons);ArrayBind(so,"cellLabels",labels);ArrayBind(so,"cellLevels",levels);ArrayBind(so,"enemyIcons",enemies);ArrayBind(so,"enemyLabels",enemyLabels);ArrayBind(so,"enemyHealth",hp);ArrayBind(so,"shotIcons",shots);ArrayBind(so,"beamImages",beams);ArrayBind(so,"deckButtons",decks);ArrayBind(so,"deckLabels",deckLabels);ArrayBind(so,"deckLevels",deckLevels);ArrayBind(so,"upgradeButtons",upgradeButtons);ArrayBind(so,"upgradeLabels",upgradeLabels);ArrayBind(so,"upgradeDescriptions",descriptions);ArrayBind(so,"upgradeStates",states);ArrayBind(so,"upgradeSteps",upgradePips);
        so.ApplyModifiedPropertiesWithoutUndo();instruction.transform.SetAsLastSibling();pauseRoot.SetAsLastSibling();root.SetActive(false);return screen;
    }
}
