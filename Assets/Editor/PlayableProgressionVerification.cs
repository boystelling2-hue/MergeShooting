using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MergeTD;

public static class PlayableProgressionVerification
{
    private static int checks;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    [MenuItem("Tools/MergeTD/Verify Playable Progression")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Verify in Edit mode");
        var go=new GameObject("Progression verification");go.SetActive(false);checks=0;
        string temp=Path.Combine(Path.GetTempPath(),"merge-levels-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var db=go.AddComponent<MergeGameDatabase>();db.LoadJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/game_data.json").text);
            var content=new GameContent();int[] expected={7,19,34,49,64};
            for(int level=1;level<=5;level++){
                int count=0;for(int a=0;a<=5;a++)for(int b=0;b<=5;b++)for(int c=0;c<=5;c++)if(content.ValidPaths(level,new[]{a,b,c}))count++;
                Check(count==expected[level-1],"exhaustive trait states Lv."+level);
            }
            Check(!content.ValidPaths(5,new[]{1,1,1})&&!content.ValidPaths(5,new[]{3,3,0})&&!content.ValidPaths(2,new[]{3,0,0}),"illegal crosspaths");
            Check(content.CanUpgrade(3,new[]{2,2,0},0,out _)&&!content.CanUpgrade(3,new[]{3,2,0},1,out _),"primary commitment transition");
            foreach(var unit in db.GetAllUnits())for(int level=1;level<=5;level++){
                var inv=new CraftInventory(db);inv.TryGrant(unit.unitId,2,out _,level);var pair=inv.GetOwnedUnits();bool merged=inv.TryCraft(pair[0].instanceId,pair[1].instanceId,out var result,out _);
                if(level<5)Check(merged&&result.unitId==unit.unitId&&inv.GetOwnedUnits()[0].level==level+1&&inv.DiscoveredCount==0,"growth "+unit.unitId+" "+level);
                else if(unit.tier<3)Check(merged&&result.tier==unit.tier+1&&inv.GetOwnedUnits()[0].level==1&&inv.DiscoveredCount==1,"tier merge "+unit.unitId);
                else Check(!merged&&inv.Count==2,"T3 Lv5 cap "+unit.unitId);
            }
            foreach(var recipe in db.GetAllCraftRecipes()){
                var inv=new CraftInventory(db);inv.TryGrant(recipe.inputA,1,out _,5);inv.TryGrant(recipe.inputB,1,out _,5);var pair=inv.GetOwnedUnits();
                Check(inv.TryCraft(pair[0].instanceId,pair[1].instanceId,out var result,out _)&&result.unitId==recipe.resultId&&inv.GetOwnedUnits()[0].level==1,"135 Lv5 recipes");
            }
            var mixed=new CraftInventory(db);mixed.TryGrant("U1_10000",1,out _,1);mixed.TryGrant("U1_10000",1,out _,2);var mix=mixed.GetOwnedUnits();Check(!mixed.TryCraft(mix[0].instanceId,mix[1].instanceId,out _,out _)&&mixed.Count==2,"unequal level rejection");
            mixed.TryGrant("U1_01000",1,out _,1);Check(!mixed.TryCraft(mix[0].instanceId,mixed.GetOwnedUnits()[2].instanceId,out _,out _),"different type before max level");
            var legacy=new CraftSaveState{version=1,units=new[]{new CraftOwnedUnit("old","U1_10000",0)},discoveredPaths=new[]{"U1_10000|U1_10000"},discoveredUnitIds=null};
            var migrated=new CraftInventory(db);migrated.Restore(legacy);Check(migrated.Export().version==3&&migrated.GetOwnedUnits()[0].level==1&&migrated.IsUnitDiscovered("U2_20000"),"legacy migration and spent discovery recovery");
            Check(legacy.units[0].level==0,"migration does not mutate input");
            var bad=migrated.Export();bad.units[0].level=6;bool rejected=false;try{migrated.Restore(bad);}catch(InvalidDataException){rejected=true;}Check(rejected&&migrated.GetOwnedUnits()[0].level==1,"invalid level restore rollback");
            var failure=new CraftInventory(db,_=>throw new IOException("disk full"));var source=mixed.Export();failure.Restore(source);int n=failure.Count;Check(!failure.TryReward(100,out _)&&failure.Count==n&&failure.Gold==source.gold,"reward save rollback");
            var grow=new CraftInventory(db);grow.TryGrant("U1_10000",2,out _);failure.Restore(grow.Export());var fp=failure.GetOwnedUnits();Check(!failure.TryCraft(fp[0].instanceId,fp[1].instanceId,out _,out _)&&failure.Count==2,"growth save rollback");
            var store=new CraftSaveFile(temp);var disk=new CraftInventory(db,store.Write);disk.TryGrant("U1_10000",1,out _,4);disk.TryUpgradePermanent(false,out _);var reload=new CraftInventory(db);reload.Restore(store.Read());Check(reload.GetOwnedUnits()[0].level==4&&reload.LevelUpgrade==1&&reload.Gold==disk.Gold,"v2 level and economy disk roundtrip");
            var prod=new CraftInventory(db);prod.TryReward(100000,out _);for(int i=0;i<10;i++){Check(prod.TryUpgradePermanent(true,out _),"tier purchase");Check(prod.TryUpgradePermanent(false,out _),"level purchase");}
            Check(!prod.TryUpgradePermanent(false,out _),"permanent cap");Check(prod.TierProbabilities()[1]==0&&prod.TierProbabilities()[2]==0,"undiscovered tiers excluded");
            Check(Mathf.Abs(prod.LevelProbabilities().Sum()-1)<.0001f&&Mathf.Abs(prod.TierProbabilities().Sum()-1)<.0001f,"normalized probabilities");
            prod.TryGrant("U2_20000",1,out _);Check(prod.TryProduce(.999f,0,.999f,out _),"high tier/level production");var produced=prod.GetOwnedUnits().Last();Check(produced.unitId=="U2_20000"&&produced.level==5,"only discovered upper tier and Lv5 lottery");
            Check(!prod.TryProduce(float.NaN,0,0,out _),"invalid roll");
            // All 90 profiles and all 270 paths can instantiate and run without touching player data.
            foreach(var unit in db.GetAllUnits())for(int path=0;path<3;path++){
                var data=new GameContent();data.Rules["initialBattleCash"]=100000;data.Rules["totalWaves"]=1;
                var sim=new DefenseBattle(db,data);Check(sim.Deploy(unit.unitId,5,new Vector2(3,3),out _),"deploy profile "+unit.unitId);var t=sim.Towers[0];
                for(int step=0;step<5;step++)Check(sim.Upgrade(t,path,out _),"five upgrades "+unit.unitId);
                int secondary=(path+1)%3;Check(sim.Upgrade(t,secondary,out _)&&sim.Upgrade(t,secondary,out _)&&!sim.Upgrade(t,secondary,out _),"secondary cap runtime");
                sim.Start();for(int tick=0;tick<200;tick++)sim.Tick(.1f);Check(sim.Cash>=0&&!float.IsNaN(t.hp),"profile simulation");
            }
            var extra=new GameContent();extra.Rules["initialBattleCash"]=10000;var effects=new DefenseBattle(db,extra);
            effects.Deploy("U1_00001",5,new Vector2(3,3),out _);var mage=effects.Towers.Last();float originalRange=effects.AttackRange(mage);Check(effects.SupportRange(mage)==0,"offense-only range distinction");effects.Upgrade(mage,2,out _);Check(effects.AttackRange(mage)==originalRange,"damage path preserves attack range");
            effects.Deploy("U3_11011",5,new Vector2(4,3),out _);var judge=effects.Towers.Last();effects.Upgrade(judge,2,out _);effects.Deploy("U1_10000",1,new Vector2(4,4),out _);var injured=effects.Towers.Last();injured.hp-=100;float health=injured.hp;effects.Start();effects.Tick(.1f);Check(injured.hp>health,"new heal path introduces actual healing");
            var pc=new GameContent();pc.Rules["initialBattleCash"]=10000;var piercing=new DefenseBattle(db,pc);piercing.Deploy("U2_02000",5,new Vector2(3,3),out _);piercing.Upgrade(piercing.Towers[0],2,out _);piercing.Start();bool sawPierce=false;for(int tick=0;tick<120;tick++){piercing.Tick(.1f);sawPierce|=piercing.Shots.Any(s=>s.mode=="line_projectile");}Check(sawPierce,"pierce branch changes projectile geometry");
            var tc=new GameContent();tc.Rules["initialBattleCash"]=10000;var tempo=new DefenseBattle(db,tc);tempo.Deploy("U3_00202",5,new Vector2(3,3),out _);tempo.Upgrade(tempo.Towers[0],0,out _);tempo.Deploy("U1_01000",1,new Vector2(4,3),out _);tempo.Start();tempo.Tick(.1f);Check(tempo.Towers[1].hasteBuff>0,"passive support tempo path affects allies");
            var loss=new DefenseBattle(db,new GameContent());loss.Start();for(int i=0;i<20000&&!loss.Finished;i++)loss.Tick(.1f);Check(loss.Finished&&!loss.Won&&loss.Lives<=0,"loss lifecycle");
            var wc=new GameContent();wc.Rules["initialBattleCash"]=100000;wc.Rules["totalWaves"]=1;var win=new DefenseBattle(db,wc);
            for(int x=2;x<9;x++){win.Deploy("U1_00001",5,new Vector2(x,3),out _);var t=win.Towers.Last();for(int j=0;j<5;j++)win.Upgrade(t,0,out _);}
            win.Start();for(int i=0;i<10000&&!win.Finished;i++)win.Tick(.1f);Check(win.Finished&&win.Won&&win.Kills==7,"win lifecycle");
            File.WriteAllText("Library/PlayableProgressionVerification.txt","PASS: "+checks+" checks; 5-level growth, 135 max-level recipes, v1 migration/v2 persistence, atomic failure, discovery lottery, exhaustive trait caps, all 90 profiles/270 paths, win/loss.");Debug.Log("Playable progression verification PASS: "+checks);
        }catch(Exception ex){File.WriteAllText("Library/PlayableProgressionVerification.txt","FAIL: "+ex);Debug.LogException(ex);throw;}
        finally{UnityEngine.Object.DestroyImmediate(go);foreach(string path in new[]{temp,temp+".tmp",temp+".bak"})if(File.Exists(path))File.Delete(path);}
    }
}

