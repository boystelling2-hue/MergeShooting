using System;
using System.IO;
using System.Linq;
using MergeTD;
using UnityEditor;
using UnityEngine;

public static class InventoryPlacementVerification
{
    private static int checks;
    private static void Check(bool condition,string label){checks++;if(!condition)throw new Exception(label);}
    [MenuItem("Tools/MergeTD/Verify Inventory Placement")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Run in Edit mode.");
        checks=0;var go=new GameObject("Placement verification");go.SetActive(false);
        string temp=Path.Combine(Path.GetTempPath(),"merge-placement-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var db=go.AddComponent<MergeGameDatabase>();db.LoadJson(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/game_data.json").text);
            var file=new CraftSaveFile(temp);var inv=new CraftInventory(db,file.Write);
            inv.TryGrant("U1_10000",3,out _);var original=inv.GetOwnedUnits();
            Check(inv.TryMove(original[1].instanceId,28,out _),"move across pages");
            Check(inv.TryCraft(original[0].instanceId,original[1].instanceId,out _,out _),"growth across pages");
            var grown=inv.GetOwnedUnits().Single(u=>u.level==2);
            Check(grown.slot==28&&inv.GetOwnedUnits().Single(u=>u.instanceId==original[2].instanceId).slot==2,"result stays at target; unrelated position stable");
            Check(inv.SlotCount==29&&inv.Count==2,"holes keep last occupied page");
            inv.TryGrant("U1_10000",1,out _);Check(inv.GetOwnedUnits().Last().slot==0,"production fills first empty slot");
            var restored=new CraftInventory(db);restored.Restore(file.Read());
            Check(JsonUtility.ToJson(inv.Export())==JsonUtility.ToJson(restored.Export()),"disk roundtrip preserves holes and positions");
            Check(!inv.TryMove(grown.instanceId,0,out _)&&!inv.TryMove(grown.instanceId,-1,out _)&&!inv.TryMove("missing",8,out _),"invalid moves rejected");
            var state=inv.Export();state.units[0].slot=state.units[1].slot;bool rejected=false;
            try{restored.Restore(state);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&restored.SlotCount==29,"duplicate positions reject restore atomically");
            foreach(int version in new[]{1,2})
            {
                var legacy=new CraftSaveState{version=version,units=new[]{new CraftOwnedUnit("old-a","U1_10000",1),new CraftOwnedUnit("old-b","U1_01000",1)},discoveredPaths=new string[0],discoveredUnitIds=new string[0]};
                var migrated=new CraftInventory(db);migrated.Restore(legacy);
                Check(migrated.Export().version==3&&migrated.GetOwnedUnits().Select(u=>u.slot).SequenceEqual(new[]{0,1}),"legacy migration "+version);
                Check(legacy.units[0].slot==-1,"migration preserves source "+version);
            }
            inv.TryGrant("U2_20000",2,out _,5);var high=inv.GetOwnedUnits().Where(u=>u.unitId=="U2_20000").ToArray();
            inv.TryMove(high[1].instanceId,49,out _);Check(inv.TryCraft(high[0].instanceId,high[1].instanceId,out _,out _),"tier recipe");
            var upper=inv.GetOwnedUnits().Single(u=>db.GetUnit(u.unitId).tier==3);
            Check(upper.slot==49,"tier result at destination");
            int discoveries=inv.DiscoveredCount,found=inv.DiscoveredUnitCount,gold=inv.Gold;
            Check(inv.TryDiscard(upper.instanceId,out _)&&!inv.TryDiscard(upper.instanceId,out _),"discard one instance once");
            Check(inv.DiscoveredCount==discoveries&&inv.DiscoveredUnitCount==found&&inv.Gold==gold,"discard retains discovery and economy");
            Check(inv.GetOwnedUnits().Any(u=>u.slot==28),"discard does not compact other units");
            inv.TryGrant("U2_20000",1,out _,1);Check(inv.TrySort(out _),"sort succeeds");
            var sorted=inv.GetOwnedUnits();Check(sorted.Select(u=>u.slot).SequenceEqual(Enumerable.Range(0,sorted.Length))&&db.GetUnit(sorted[0].unitId).tier==2,"sort packs slots and high tiers first");
            var same=sorted.Where(u=>u.unitId=="U1_10000").ToArray();Check(same[0].level==2,"same type high level first");
            string firstSort=JsonUtility.ToJson(inv.Export());inv.TrySort(out _);Check(firstSort==JsonUtility.ToJson(inv.Export()),"sort stable");
            bool fail=false;var atomic=new CraftInventory(db,_=>{if(fail)throw new IOException("simulated failure");});atomic.Restore(inv.Export());
            string before=JsonUtility.ToJson(atomic.Export());var units=atomic.GetOwnedUnits();fail=true;
            Check(!atomic.TryMove(units[0].instanceId,60,out _)&&!atomic.TryDiscard(units[0].instanceId,out _)&&!atomic.TrySort(out _),"failed writes reject move, discard, sort");
            Check(before==JsonUtility.ToJson(atomic.Export()),"failed writes preserve entire inventory");
            File.WriteAllText("Library/InventoryPlacementVerification.txt","PASS: "+checks+" checks; destination growth/tier merge, sparse positions, first-hole production, v1/v2 migration, v3 disk roundtrip, sort, deletion/discovery, save failure rollback.");
            Debug.Log("Inventory placement verification PASS: "+checks);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            foreach(string path in new[]{temp,temp+".tmp",temp+".bak"})if(File.Exists(path))File.Delete(path);
        }
    }
}
