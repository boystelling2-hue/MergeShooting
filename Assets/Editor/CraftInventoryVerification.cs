using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using MergeTD;

[InitializeOnLoad]
public static class CraftInventoryVerification
{
    static CraftInventoryVerification() { EditorApplication.delayCall += Run; }
    [MenuItem("Tools/MergeTD/Verify Main Crafting")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var asset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/game_data.json");if(asset==null) return;
        var go=new GameObject("Craft tests (temporary)");go.SetActive(false);
        string temp=Path.Combine(Path.GetTempPath(),"MergeTD-craft-test-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var db=go.AddComponent<MergeGameDatabase>();db.LoadJson(asset.text);
            int checkedRecipes=0;
            foreach(var recipe in db.GetAllCraftRecipes())
            {
                var inv=new CraftInventory(db);
                Check(inv.TryGrant(recipe.inputA,1,out _) && inv.TryGrant(recipe.inputB,1,out _),"grant materials");
                var before=inv.GetOwnedUnits();
                Check(inv.TryCraft(before[0].instanceId,before[1].instanceId,out var result,out _),"recipe execution");
                Check(result.unitId==recipe.resultId && inv.Count==1 && inv.GetOwnedUnits()[0].unitId==recipe.resultId,"DB result and material consumption");
                Check(inv.IsDiscovered(recipe.inputB,recipe.inputA) && inv.DiscoveredCount==1,"discovery independent of input order");
                Check(!inv.TryCraft(before[0].instanceId,before[1].instanceId,out _,out _) && inv.Count==1,"stale/repeated command cannot duplicate output");
                checkedRecipes++;
            }
            Check(checkedRecipes==135,"all 135 recipes");
            var inventory=new CraftInventory(db);
            inventory.TryGrant("U1_10000",1,out _);var one=inventory.GetOwnedUnits()[0];
            Check(!inventory.TryCraft(one.instanceId,one.instanceId,out _,out _) && inventory.Count==1,"same copy cannot be used twice");
            inventory.TryGrant("U2_20000",1,out _);
            Check(!inventory.TryCraft(one.instanceId,inventory.GetOwnedUnits()[1].instanceId,out _,out _) && inventory.Count==2,"mixed tiers rejected");
            Check(!inventory.TryGrant("missing",1,out _) && !inventory.TryGrant("U1_10000",0,out _),"invalid grants rejected");
            var snapshot=inventory.Export();snapshot.units[0].unitId="modified";
            Check(inventory.GetOwnedUnits()[0].unitId=="U1_10000","snapshot does not expose inventory state");

            var store=new CraftSaveFile(temp);var persisted=new CraftInventory(db,store.Write);
            persisted.TryGrant("U1_10000",2,out _);var pair=persisted.GetOwnedUnits();
            Check(persisted.TryCraft(pair[0].instanceId,pair[1].instanceId,out _,out _),"persistent craft");
            var restored=new CraftInventory(db);restored.Restore(store.Read());
            Check(restored.Count==1 && restored.DiscoveredCount==1 && restored.GetOwnedUnits()[0].unitId=="U2_20000","disk round trip");
            var broken=new CraftSaveState { units=new[]{one,one},discoveredPaths=new string[0] };
            bool rejected=false;try { restored.Restore(broken); } catch(InvalidDataException) { rejected=true; }
            Check(rejected && restored.Count==1 && restored.DiscoveredCount==1,"invalid load preserves state");
            var failure=new CraftInventory(db,_=>throw new IOException("simulated full disk"));
            failure.Restore(new CraftSaveState { units=new[]{new CraftOwnedUnit("a","U1_10000"),new CraftOwnedUnit("b","U1_10000")},discoveredPaths=new string[0] });
            Check(!failure.TryCraft("a","b",out _,out _) && failure.Count==2 && failure.DiscoveredCount==0,"failed save cannot consume ingredients or discover recipe");
            File.WriteAllText("Library/MainCraftingVerification.txt","PASS: all 135 DB recipes, two-copy consumption, same-copy/invalid/stale rejection, discovery, snapshot isolation, disk persistence, invalid restore rollback, failed-save rollback.");
            Debug.Log("Main crafting verification PASS: 135 recipes");
        }
        catch(Exception ex)
        { File.WriteAllText("Library/MainCraftingVerification.txt","FAIL: "+ex);Debug.LogException(ex); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            foreach(var path in new[]{temp,temp+".bak",temp+".tmp"}) if(File.Exists(path)) File.Delete(path);
        }
    }
    private static void Check(bool ok,string label) { if(!ok) throw new Exception("Crafting verification failed: "+label); }
}
