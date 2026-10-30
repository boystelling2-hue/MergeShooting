using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using MergeTD;

[InitializeOnLoad]
public static class BattleMergeVerification
{
    static BattleMergeVerification() { EditorApplication.delayCall += Run; }
    [MenuItem("Tools/MergeTD/Verify Battle Merge")]
    public static void Run()
    {
        try
        {
            var board = new BattleMergeBoard(new[] { "knight", "archer" }, 4, 3);
            int changes = 0; board.Changed += () => changes++;
            Check(board.TrySpawn("knight", out int a, out _), "first spawn");
            Check(board.TrySpawn("knight", out int b, out _), "second spawn");
            var oldA = board.Get(a); var oldB = board.Get(b);
            Check(oldA.InstanceId != oldB.InstanceId, "unique instance IDs");
            Check(!board.TryMoveOrMerge(a,a,out _) && changes==2 && board.Count==2, "self merge unchanged");
            Check(board.TryMoveOrMerge(a,b,out _) && board.Count==1 && board.Get(a)==null && board.Get(b).Star==2, "merge consumes one");
            Check(board.Get(b).InstanceId==oldB.InstanceId && oldB.Star==1, "target identity and immutable snapshot");
            int changedAfterMerge=changes;
            Check(!board.TryMoveOrMerge(a,b,out _) && changes==changedAfterMerge, "duplicate command cannot merge twice");
            board.TrySpawn("archer",out a,out _);
            Check(!board.TryMoveOrMerge(a,b,out _) && board.Count==2, "different units rejected");
            board.TrySpawn("knight",out int c,out _);
            Check(!board.TryMoveOrMerge(c,b,out _), "different stars rejected");
            board.TrySpawn("knight",out int d,out _);
            Check(!board.TrySpawn("knight",out _,out _) && board.Count==4, "full board rejects spawn");
            Check(board.TryMoveOrMerge(c,d,out _), "merge on full board");
            Check(board.TryMoveOrMerge(d,b,out _) && board.Get(b).Star==3, "second promotion");
            Check(!board.TrySpawn("mage",out _,out _), "off-deck summon rejected");
            Check(!board.TryMoveOrMerge(-1,b,out _) && !board.TryMoveOrMerge(b,4,out _), "bounds rejected");
            Check(board.TryMoveOrMerge(b,c,out _) && board.Get(c).Star==3, "move preserves star");
            board.Reset(); Check(board.Count==0 && board.Get(c)==null, "new round resets board");
            var cap = new BattleMergeBoard(new[] { "knight" },4,2);
            cap.TrySpawn("knight",out a,out _); cap.TrySpawn("knight",out b,out _);cap.TryMoveOrMerge(a,b,out _);
            cap.TrySpawn("knight",out c,out _);cap.TrySpawn("knight",out d,out _);cap.TryMoveOrMerge(c,d,out _);
            Check(!cap.CanMerge(b,d) && !cap.TryMoveOrMerge(b,d,out _) && cap.Count==2, "maximum star rejects merge without loss");
            File.WriteAllText("Library/BattleMergeVerification.txt", "PASS: spawn, unique IDs, atomic merge, stale command, mismatched types/stars, full board, max star, bounds, move, round reset.");
            Debug.Log("Battle merge verification PASS");
        }
        catch(Exception ex)
        {
            File.WriteAllText("Library/BattleMergeVerification.txt", "FAIL: "+ex);
            Debug.LogException(ex);
        }
    }
    private static void Check(bool condition,string label)
    { if(!condition) throw new Exception("Battle merge check failed: "+label); }
}
