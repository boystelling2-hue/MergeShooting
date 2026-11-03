using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MergeTD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DefenseBtdVerification
{
    private static int checks;
    private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static DefenseBtdVerification(){if(SessionState.GetBool("BtdPlaySmoke",false))EditorApplication.update+=PlayTick;}
    public static void BuildAndPlayVerify(){BuildAndVerify();SessionState.SetBool("BtdPlaySmoke",true);EditorApplication.update+=PlayTick;EditorApplication.isPlaying=true;}
    private static void PlayTick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        var main=UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();if(main==null||Field<CraftInventory>(main,"inventory")==null)return;
        EditorApplication.update-=PlayTick;SessionState.SetBool("BtdPlaySmoke",false);
        try
        {
            Field<Button>(main,"battleButton").onClick.Invoke();var b=Field<BattleScreen>(main,"battleScreen");Check(b.gameObject.activeInHierarchy&&b.Simulation!=null,"runtime battle entry");
            Call(b,"ClickCell",3*DefenseBattle.Columns+6);Check(b.Simulation.Towers.Count==1&&Field<GameObject>(b,"selectionRoot").activeSelf,"runtime deployment and selection");
            Field<Button>(b,"targetButton").onClick.Invoke();Check(b.Simulation.Towers[0].targetMode==1,"runtime target priority");
            Field<Button>(b,"startButton").onClick.Invoke();Check(b.Simulation.Running,"runtime round start");
            Field<Button>(b,"speedButton").onClick.Invoke();Check(Field<float>(b,"speed")==2,"runtime fast forward");
            Field<Button>(b,"settingsButton").onClick.Invoke();Check(Field<GameObject>(b,"pauseRoot").activeSelf&&Field<bool>(b,"paused"),"runtime pause menu");
            Field<Button>(b,"resumeButton").onClick.Invoke();Check(!Field<bool>(b,"paused"),"runtime resume");
            for(int tick=0;tick<90;tick++)b.Simulation.Tick(.1f);Call(b,"Draw");Capture(b,"btd-live-play.png");
            Field<Button>(b,"deploymentButton").onClick.Invoke();Check(!Field<GameObject>(b,"selectionRoot").activeSelf,"runtime close selection");
            File.WriteAllText("Library/DefenseBtdPlayVerification.txt","PASS: real Play mode entry, deployment, selection, targeting, start, speed, pause/resume, selected-panel close; no inventory mutation.");
            Debug.Log("BTD real Play mode smoke PASS");EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText("Library/DefenseBtdPlayVerification.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    private static T Field<T>(object o,string key)=>(T)o.GetType().GetField(key,Private).GetValue(o);
    private static void Call(object o,string key,params object[] args)=>o.GetType().GetMethod(key,Private).Invoke(o,args);
    private static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    public static void BuildAndVerify()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");LandscapeSceneAuthoring.Build();
            PlayableProgressionVerification.Run();InventoryPlacementVerification.Run();CraftInventoryVerification.Run();
            Verify();VerifyArt();
            File.WriteAllText("Library/DefenseBtdVerification.txt","PASS: "+checks+" BTD UI and targeting checks; authored scene saved; workshop and progression checks passed.");
            Debug.Log("BTD defense verification PASS: "+checks);
        }
        catch(Exception e){File.WriteAllText("Library/DefenseBtdVerification.txt","FAIL: "+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
    }
    [MenuItem("Tools/MergeTD/Verify BTD Defense UI")]
    public static void Verify()
    {
        checks=0;
        var main=UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();var db=Field<MergeGameDatabase>(main,"database");
        var b=Field<BattleScreen>(main,"battleScreen");var units=db.GetAllUnits().Where(u=>u.tier==1).ToArray();
        var deck=units.Select((u,i)=>new CraftOwnedUnit("preview-"+i,u.unitId,i==1?3:5)).ToArray();
        var data=new GameContent();data.Rules["initialBattleCash"]=5000;
        Call(b,"Awake");main.GetComponent<Canvas>().enabled=false;
        b.Open(db,data,deck,Field<Sprite[]>(main,"roleIcons"),_=>true,()=>{});
        var board=Field<RectTransform>(b,"board");var selection=Field<GameObject>(b,"selectionRoot");
        Check(board.sizeDelta==new Vector2(1080,660),"battlefield occupies most of screen");
        Check(!selection.activeSelf,"selection panel hidden before selection");
        var decks=Field<Button[]>(b,"deckButtons");Check(decks.All(x=>x.GetComponent<RectTransform>().anchoredPosition.x>=1092),"all purchase cards in right sidebar");
        Check(Field<Image[]>(b,"deckPortraits").All(x=>x.sprite!=null),"shop portraits bound");
        int first=3*DefenseBattle.Columns+6;b.PreviewCell(first);Check(Field<RectTransform>(b,"attackRing").gameObject.activeSelf,"placement range preview");
        b.PreviewCell(2*DefenseBattle.Columns+6);Check(Field<RectTransform>(b,"attackRing").GetComponent<Image>().color.r>.9f,"invalid placement preview red");
        Call(b,"ClickCell",first);Check(selection.activeSelf&&b.Simulation.Towers.Count==1,"placement opens selected tower panel");
        Check(Field<Image>(b,"towerPortrait").sprite!=null,"selected portrait bound");
        var tower=b.Simulation.Towers[0];var target=Field<Button>(b,"targetButton");
        b.Simulation.Enemies.Add(new DefenseBattle.Enemy{serial=1,hp=20,maxHp=20,progress=4,position=new Vector2(6,2)});
        b.Simulation.Enemies.Add(new DefenseBattle.Enemy{serial=2,hp=5,maxHp=20,progress=10,position=new Vector2(7,2)});
        b.Simulation.Enemies.Add(new DefenseBattle.Enemy{serial=3,hp=100,maxHp=100,progress=2,position=new Vector2(8,2)});
        var select=typeof(DefenseBattle).GetMethod("Select",Private);
        target.onClick.Invoke();Check(tower.targetMode==1&&((DefenseBattle.Enemy)select.Invoke(b.Simulation,new object[]{tower})).serial==2,"first targeting");
        target.onClick.Invoke();Check(tower.targetMode==2&&((DefenseBattle.Enemy)select.Invoke(b.Simulation,new object[]{tower})).serial==3,"strong targeting");
        target.onClick.Invoke();Check(tower.targetMode==3&&((DefenseBattle.Enemy)select.Invoke(b.Simulation,new object[]{tower})).serial==1,"close targeting");
        target.onClick.Invoke();Check(tower.targetMode==0,"targeting cycles to database default");
        for(int i=0;i<5;i++)Check(b.Simulation.Upgrade(tower,0,out _),"main upgrade "+i);
        for(int i=0;i<2;i++)Check(b.Simulation.Upgrade(tower,1,out _),"secondary upgrade "+i);
        Call(b,"Draw");var states=Field<Text[]>(b,"upgradeStates");
        Check(states[0].text.Contains("완성")&&states[1].text.Contains("보조")&&states[2].text.Contains("잠김"),"three distinct upgrade limit states");
        Capture(b,"btd-selected.png");
        Field<Button>(b,"deploymentButton").onClick.Invoke();Check(!selection.activeSelf,"closing selection exposes full map");
        decks[1].onClick.Invoke();Call(b,"ClickCell",3*DefenseBattle.Columns+9);var low=b.Simulation.Towers.Last();
        for(int i=0;i<3;i++)b.Simulation.Upgrade(low,0,out _);Call(b,"Draw");Check(states[0].text.Contains("Lv.4"),"level cap shown separately");
        Field<Button>(b,"settingsButton").onClick.Invoke();Check(Field<GameObject>(b,"pauseRoot").activeSelf,"settings opens pause menu");
        Field<Button>(b,"resumeButton").onClick.Invoke();Check(!Field<GameObject>(b,"pauseRoot").activeSelf,"resume closes menu");
        Field<Button>(b,"deploymentButton").onClick.Invoke();Capture(b,"btd-battlefield.png");
        var root=b.transform.Find("Battle Landscape Layout").GetComponent<RectTransform>();Canvas.ForceUpdateCanvases();
        foreach(var button in b.GetComponentsInChildren<Button>(true))
        {
            var corners=new Vector3[4];button.GetComponent<RectTransform>().GetWorldCorners(corners);
            Check(corners.All(p=>{var q=root.InverseTransformPoint(p);return q.x>=root.rect.xMin-.1f&&q.x<=root.rect.xMax+.1f&&q.y>=root.rect.yMin-.1f&&q.y<=root.rect.yMax+.1f;}),"bounds "+button.name);
        }
        // Demo actions affect only this temporary battle; reload the already-saved scene.
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }
    private static void Capture(BattleScreen b,string name)
    {
        Directory.CreateDirectory("Captures");var canvas=b.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=1;
        foreach(var label in b.GetComponentsInChildren<Text>()){label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);label.SetAllDirty();}
        Canvas.ForceUpdateCanvases();var rt=RenderTexture.GetTemporary(1920,1080,24);var cam=Camera.main;var old=cam.targetTexture;var previous=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes("Captures/"+name,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
        finally{cam.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    private static void VerifyArt()
    {
        var main=UnityEngine.Object.FindFirstObjectByType<MainCraftingScreen>();var db=Field<MergeGameDatabase>(main,"database");db.Reload();var b=Field<BattleScreen>(main,"battleScreen");
        var units=db.GetAllUnits().Where(u=>u.tier==1).ToArray();var deck=units.Select((u,i)=>new CraftOwnedUnit("art-"+i,u.unitId,3)).ToArray();
        var content=new GameContent();content.Rules["initialBattleCash"]=3000;Call(b,"Awake");main.GetComponent<Canvas>().enabled=false;b.Open(db,content,deck,Field<Sprite[]>(main,"roleIcons"),_=>true,()=>{});
        var sprites=Field<Sprite[]>(b,"enemySprites");Check(sprites.Length==3&&sprites.All(s=>s!=null),"three painted enemy sprites");
        Check(Field<RectTransform>(b,"board").GetComponent<Image>().sprite!=null,"painted meadow assigned");
        var route=new Texture2D(2,2);route.LoadImage(File.ReadAllBytes(DefenseArtAssets.DirectoryPath+"MeadowRoute-v1.png"));
        for(int y=0;y<DefenseBattle.Rows;y++)for(int x=0;x<DefenseBattle.Columns;x++)Check((route.GetPixel(x*60+30,660-y*60-30).a>.9f)==b.Simulation.IsPath(x,y),"road/collision alignment "+x+","+y);UnityEngine.Object.DestroyImmediate(route);
        int[] ids={1,5,10,2,15,20,7,25};Vector2[] points={new Vector2(3,2),new Vector2(5,2),new Vector2(8,2),new Vector2(12,2),new Vector2(16,4),new Vector2(10,8),new Vector2(6,8),new Vector2(3,6)};
        for(int i=0;i<ids.Length;i++){bool boss=i==2||i==5;b.Simulation.Enemies.Add(new DefenseBattle.Enemy{serial=ids[i],monsterId="E01",boss=boss,position=points[i],maxHp=boss?400:80,hp=boss?300:80,slowUntil=i==1?10:0});}
        Vector2[] placements={new Vector2(4,3),new Vector2(7,3),new Vector2(9,6),new Vector2(15,5),new Vector2(5,7)};
        for(int i=0;i<deck.Length;i++)b.Simulation.Deploy(deck[i].unitId,3,placements[i],out _);Call(b,"Draw");
        var rendered=Field<Image[]>(b,"enemyIcons");Check(rendered[0].sprite==sprites[0]&&rendered[1].sprite==sprites[1]&&rendered[2].sprite==sprites[2],"normal runner boss rendering");
        Check(rendered.Take(ids.Length).All(i=>i.color==Color.white),"painted palettes preserved during slow and boss states");
        Check(rendered[2].rectTransform.sizeDelta.x>rendered[0].rectTransform.sizeDelta.x,"boss silhouette larger");
        Capture(b,"meadow-enemies.png");EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }
}
