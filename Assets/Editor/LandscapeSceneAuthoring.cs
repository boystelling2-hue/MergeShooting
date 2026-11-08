using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using MergeTD;

// Edit-time authoring only. Runtime screens bind the serialized scene objects.
public static class LandscapeSceneAuthoring
{
    private static Font font;
    private static readonly Color Ink=new Color(.045f,.065f,.075f),Surface=new Color(.11f,.15f,.17f),Light=new Color(.88f,.92f,.90f),Accent=new Color(.42f,.73f,.72f),Gold=new Color(.94f,.76f,.42f);
    internal static RectTransform Panel(string name,Transform parent,float x,float y,float w,float h,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=5;go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);go.GetComponent<Image>().color=color;return rt;
    }
    internal static Text Label(string name,string text,Transform parent,float x,float y,float w,float h,int size=20,TextAnchor alignment=TextAnchor.MiddleCenter)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CrispText));go.layer=5;go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=Light;t.alignment=alignment;t.raycastTarget=false;return t;
    }
    internal static Button Button(string name,string text,Transform parent,float x,float y,float w,float h,int size=19)
    {
        var rt=Panel(name,parent,x,y,w,h,new Color(.23f,.34f,.36f));var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=rt.GetComponent<Image>();Label("Label",text,rt,6,3,w-12,h-6,size);return button;
    }
    internal static UnitLevelIndicator LevelIndicator(Transform parent,float w,float h,bool compact=false)
    {
        var go=new GameObject("Growth Level",typeof(RectTransform),typeof(UnitLevelIndicator));go.layer=5;go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.sizeDelta=new Vector2(w,h);
        float badgeW=compact?16:40,badgeH=compact?15:20;
        var badge=Panel("Level Badge",rt,w-badgeW-(compact?1:4),compact?1:5,badgeW,badgeH,Surface).GetComponent<Image>();badge.raycastTarget=false;
        var label=Label("Level Number",compact?"1":"Lv.1",badge.transform,0,0,badgeW,badgeH,compact?10:12);label.fontStyle=FontStyle.Bold;
        float gap=compact?2:5,stepW=Mathf.Min(compact?5:16,(w-16-gap*4)/5),stepH=compact?2:4,total=stepW*5+gap*4;
        var steps=new List<Image>();for(int i=0;i<5;i++){var step=Panel("Growth Step "+(i+1),rt,(w-total)/2+i*(stepW+gap),h-(compact?3:8),stepW,stepH,Light).GetComponent<Image>();step.raycastTarget=false;steps.Add(step);}
        var view=go.GetComponent<UnitLevelIndicator>();var so=new SerializedObject(view);Bind(so,"badge",badge);Bind(so,"label",label);ArrayBind(so,"steps",steps);so.FindProperty("compact").boolValue=compact;so.ApplyModifiedPropertiesWithoutUndo();view.SetLevel(1);return view;
    }
    internal static void Bind(SerializedObject so,string name,UnityEngine.Object value)=>so.FindProperty(name).objectReferenceValue=value;
    internal static void ArrayBind<T>(SerializedObject so,string name,List<T> values)where T:UnityEngine.Object
    {var a=so.FindProperty(name);a.arraySize=values.Count;for(int i=0;i<values.Count;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
    internal static RectTransform Frame(GameObject root,string name)
    {
        var c=root.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.enabled=true;c.pixelPerfect=true;
        var s=root.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1280,720);s.matchWidthOrHeight=1;
        var backdrop=Panel("Letterbox",root.transform,0,0,1280,720,Ink);backdrop.anchorMin=Vector2.zero;backdrop.anchorMax=Vector2.one;backdrop.offsetMin=backdrop.offsetMax=Vector2.zero;
        var frame=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();frame.SetParent(root.transform,false);frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.sizeDelta=new Vector2(1280,720);
        Panel("Background",frame,0,0,1280,720,Ink);Panel("Header",frame,0,0,1280,70,new Color(.09f,.125f,.14f));Panel("Header Divider",frame,0,69,1280,2,new Color(.28f,.36f,.36f));return frame;
    }
    [MenuItem("Tools/MergeTD/Build Landscape UI")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring UI.");
        var main=UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();if(main==null)throw new InvalidOperationException("Open SampleScene first.");
        var scene=main.gameObject.scene;Undo.SetCurrentGroupName("Convert to landscape UI");Undo.RecordObject(main,"Bind landscape UI");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Workshop/WorkshopKorean.ttf");if(font==null)throw new InvalidOperationException("Workshop font missing.");
        var db=UnityEngine.Object.FindFirstObjectByType<MergeGameDatabase>();db.Reload();
        foreach(var old in UnityEngine.Object.FindObjectsByType<BattleScreen>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(old.gameObject.scene==scene)Undo.DestroyObjectImmediate(old.gameObject);
        var children=new List<GameObject>();foreach(Transform child in main.transform)children.Add(child.gameObject);foreach(var child in children)Undo.DestroyObjectImmediate(child);
        var frame=Frame(main.gameObject,"Landscape Layout");
        Label("Title","MERGE SHOOTING  /  WORKSHOP",frame,28,15,590,40,25,TextAnchor.MiddleLeft);
        var summary=Label("Inventory Summary","보유 0 · 골드 0",frame,702,15,248,40,21);
        var discovery=Label("Discovery Summary","발견 0/90 · 제작법 0",frame,960,15,292,40,19);
        Label("Inventory Heading","유닛 성장 / 합성",frame,28,88,248,40,26,TextAnchor.MiddleLeft);
        var sort=Button("Sort Inventory","정렬",frame,282,88,112,40,18);
        var collection=Button("Collection","보관함",frame,414,88,106,40,18);
        var recipes=Button("Recipes","제작법",frame,532,88,106,40,18);
        var permanent=Button("Permanent Upgrades","영구 강화",frame,650,88,126,40,18);
        var grid=Panel("Merge Board",frame,28,144,748,500,new Color(.075f,.105f,.12f));
        var slots=new List<CraftUnitView>();var sprites=new List<Sprite>();for(int i=0;i<5;i++)sprites.Add(HighResolutionRoleIcons.Load(i));
        var baseUnits=db.GetAllUnits().Where(u=>u.tier==1).ToArray();
        for(int i=0;i<25;i++){
            var rt=Panel("Unit Slot "+(i+1).ToString("00"),grid,12+i%5*146,12+i/5*96,136,86,new Color(.13f,.18f,.20f));
            var outline=rt.gameObject.AddComponent<Outline>();outline.effectDistance=new Vector2(2,-2);outline.enabled=false;
            var icon=Panel("Role Icon",rt,52,7,32,32,Light).GetComponent<Image>();icon.sprite=sprites[i%5];icon.preserveAspect=true;icon.raycastTarget=false;icon.enabled=i<20;
            var name=Label("Unit Name",i<20?baseUnits[i%5].name:"",rt,4,42,128,24,18);name.resizeTextForBestFit=true;name.resizeTextMinSize=12;name.resizeTextMaxSize=18;
            var tier=Label("Tier and Level",i<20?"T1":"",rt,6,7,34,22,12,TextAnchor.MiddleLeft);tier.color=Gold;
            var levelIndicator=LevelIndicator(rt,136,86);levelIndicator.SetLevel(i<20?1:0);
            var view=rt.gameObject.AddComponent<CraftUnitView>();var so=new SerializedObject(view);Bind(so,"icon",icon);Bind(so,"surface",rt.GetComponent<Image>());Bind(so,"unitName",name);Bind(so,"tier",tier);Bind(so,"outline",outline);Bind(so,"levelIndicator",levelIndicator);so.ApplyModifiedPropertiesWithoutUndo();slots.Add(view);
        }
        var status=Label("Status","겹쳐 합성 · 빈 칸으로 이동 · 쓰레기통에 버리기",frame,28,654,394,46,15,TextAnchor.MiddleLeft);status.color=Accent;
        var trash=Panel("Trash Drop Target",frame,434,654,130,46,new Color(.32f,.15f,.15f));
        var trashOutline=trash.gameObject.AddComponent<Outline>();trashOutline.effectColor=new Color(1,.4f,.28f);trashOutline.effectDistance=new Vector2(2,-2);trashOutline.enabled=false;
        Label("Trash Label","쓰레기통 · 드래그",trash,4,3,122,40,14);
        var trashTarget=trash.gameObject.AddComponent<CraftTrashDropTarget>();
        var prev=Button("Previous Page","◀",frame,580,662,48,34,18);var page=Label("Page","1 / 1",frame,632,662,88,34,18);var next=Button("Next Page","▶",frame,724,662,48,34,18);
        Panel("Unit and Production Panel",frame,800,88,452,608,Surface);
        Label("Selected Unit Heading","선택한 유닛",frame,824,106,404,30,19,TextAnchor.MiddleLeft).color=Gold;
        var detail=Label("Unit Detail","유닛을 눌러 능력을 확인하세요\n같은 유닛 두 개를 겹쳐 성장시킬 수 있습니다",frame,824,150,404,100,18,TextAnchor.UpperLeft);
        var deckToggle=Button("Deck Toggle","덱 추가",frame,824,268,404,42,19);
        Label("Deck Heading","출전 덱  /  최대 5종",frame,824,330,404,26,19,TextAnchor.MiddleLeft).color=Gold;
        var deckSummary=Label("Deck Summary","출전 유닛을 선택하세요",frame,824,366,404,66,17,TextAnchor.UpperLeft);
        var spawn=Button("Produce Unit","유닛 제작\n무료 · 제작 확률 적용",frame,824,452,404,76,23);spawn.GetComponent<Image>().color=new Color(.27f,.49f,.51f);
        var enter=Button("Enter Battle","전투 입장",frame,824,548,404,68,24);enter.GetComponent<Image>().color=new Color(.36f,.46f,.33f);
        Label("Growth Hint","Lv.1~5 성장  ·  특성 최대 5 / 2 / 0",frame,824,642,404,30,16);
        var modal=Panel("Information Modal",frame,0,0,1280,720,new Color(0,0,0,.8f));
        Panel("Dialog Frame",modal,340,56,600,608,new Color(.13f,.18f,.20f));
        var title=Label("Modal Title","공방 안내",modal,368,76,544,46,28);title.color=Gold;
        var body=Label("Modal Body","",modal,368,140,544,314,18,TextAnchor.UpperLeft);body.resizeTextForBestFit=true;body.resizeTextMinSize=14;body.resizeTextMaxSize=18;
        var level=Button("Level Upgrade","시작 레벨 강화",modal,368,478,260,68,19);var tierUpgrade=Button("Tier Upgrade","티어 확률 강화",modal,652,478,260,68,19);
        var discard=Button("Confirm Discard","유닛 삭제",modal,368,478,544,68,22);discard.gameObject.SetActive(false);
        var close=Button("Close Dialog","닫기",modal,530,588,220,48,20);level.gameObject.SetActive(false);tierUpgrade.gameObject.SetActive(false);modal.gameObject.SetActive(false);
        var ghost=Panel("Drag Preview",frame,0,0,136,86,new Color(.2f,.29f,.30f,.95f));ghost.anchorMin=ghost.anchorMax=ghost.pivot=new Vector2(.5f,.5f);ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        var ghostIcon=Panel("Icon",ghost,52,7,32,32,Light).GetComponent<Image>();ghostIcon.preserveAspect=true;ghostIcon.raycastTarget=false;
        var ghostLabel=Label("Unit Name","유닛",ghost,4,42,128,24,17);ghostLabel.resizeTextForBestFit=true;ghostLabel.resizeTextMinSize=12;ghostLabel.resizeTextMaxSize=17;
        var ghostLevel=LevelIndicator(ghost,136,86);ghost.gameObject.SetActive(false);
        var bind=new SerializedObject(main);Bind(bind,"database",db);Bind(bind,"canvas",main.GetComponent<Canvas>());Bind(bind,"scaler",main.GetComponent<CanvasScaler>());ArrayBind(bind,"slots",slots);ArrayBind(bind,"roleIcons",sprites);
        Bind(bind,"summary",summary);Bind(bind,"discovery",discovery);Bind(bind,"status",status);Bind(bind,"detail",detail);Bind(bind,"pageLabel",page);Bind(bind,"spawnButton",spawn);Bind(bind,"previousButton",prev);Bind(bind,"nextButton",next);Bind(bind,"collectionButton",collection);Bind(bind,"recipesButton",recipes);Bind(bind,"helpButton",permanent);Bind(bind,"closeButton",close);Bind(bind,"modal",modal.gameObject);Bind(bind,"modalTitle",title);Bind(bind,"modalBody",body);Bind(bind,"dragPreview",ghost);Bind(bind,"dragIcon",ghostIcon);Bind(bind,"dragLabel",ghostLabel);Bind(bind,"battleButton",enter);Bind(bind,"deckButton",deckToggle);Bind(bind,"deckLabel",deckSummary);Bind(bind,"levelUpgradeButton",level);Bind(bind,"tierUpgradeButton",tierUpgrade);
        Bind(bind,"sortButton",sort);Bind(bind,"discardButton",discard);Bind(bind,"trashTarget",trashTarget);Bind(bind,"dragLevel",ghostLevel);
        var battle=BuildBattle(db);Bind(bind,"battleScreen",battle);bind.ApplyModifiedPropertiesWithoutUndo();
        if(UnityEngine.Object.FindFirstObjectByType<EventSystem>()==null){var events=new GameObject("UI EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();Undo.RegisterCreatedObjectUndo(events,"UI input");}
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
        PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.defaultIsNativeResolution=true;PlayerSettings.defaultWebScreenWidth=1920;PlayerSettings.defaultWebScreenHeight=1080;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=main.gameObject;
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(WorkshopSkinAuthoring.AtlasPath)!=null)WorkshopSkinAuthoring.Apply();
        Debug.Log("Landscape scene saved: 1280x720 workshop + battle.");
    }
    private static BattleScreen BuildBattle(MergeGameDatabase db) => DefenseSceneAuthoring.Build(db);
}
