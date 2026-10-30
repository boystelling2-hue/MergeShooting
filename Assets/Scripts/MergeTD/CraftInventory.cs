using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MergeTD
{
    [Serializable] public sealed class CraftOwnedUnit
    {
        public string instanceId, unitId;
        public CraftOwnedUnit(string instance, string unit) { instanceId = instance; unitId = unit; }
    }
    [Serializable] public sealed class CraftSaveState
    {
        public int version = 1;
        public CraftOwnedUnit[] units;
        public string[] discoveredPaths;
    }

    // Every owned copy has its own ID. A successful craft consumes exactly two copies.
    public sealed class CraftInventory
    {
        private readonly MergeGameDatabase database;
        private readonly Action<CraftSaveState> persist;
        private List<CraftOwnedUnit> units = new List<CraftOwnedUnit>();
        private HashSet<string> discovered = new HashSet<string>(StringComparer.Ordinal);
        public int Count => units.Count;
        public int DiscoveredCount => discovered.Count;
        public event Action Changed;

        public CraftInventory(MergeGameDatabase database, Action<CraftSaveState> persist = null)
        {
            if (database == null || !database.IsLoaded) throw new ArgumentException("A loaded database is required.");
            this.database = database; this.persist = persist;
        }

        public CraftOwnedUnit[] GetOwnedUnits() => Snapshot(units, discovered).units;
        public CraftSaveState Export() => Snapshot(units, discovered);
        public bool IsDiscovered(string a, string b) => a != null && b != null && discovered.Contains(PathKey(a,b));

        public void Restore(CraftSaveState state)
        {
            if (state == null || state.version != 1 || state.units == null || state.discoveredPaths == null)
                throw new InvalidDataException("합성 저장 파일의 형식이 올바르지 않습니다.");
            var next = new List<CraftOwnedUnit>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.units)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId) || !ids.Add(item.instanceId) || !Known(item.unitId))
                    throw new InvalidDataException("저장된 유닛 ID가 없거나 중복되었습니다.");
                next.Add(new CraftOwnedUnit(item.instanceId,item.unitId));
            }
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in state.discoveredPaths)
            {
                var pair = path == null ? new string[0] : path.Split('|');
                if (pair.Length != 2 || path != PathKey(pair[0],pair[1]) || !paths.Add(path) ||
                    !database.TryResolveCraftMerge(pair[0],pair[1],out _))
                    throw new InvalidDataException("저장된 제작법이 현재 DB와 일치하지 않습니다.");
            }
            units = next; discovered = paths;
            Changed?.Invoke();
        }

        public bool TryGrant(string unitId, int amount, out string message)
        {
            if (!Known(unitId) || amount < 1 || amount > 100) { message = "지급할 유닛과 수량이 올바르지 않습니다."; return false; }
            var next = new List<CraftOwnedUnit>(units);
            for (int i=0;i<amount;i++) next.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),unitId));
            if (!Commit(next,new HashSet<string>(discovered,StringComparer.Ordinal),out message)) return false;
            message = database.GetUnit(unitId).name + " " + amount + "개 지급";
            return true;
        }

        public bool TryCraft(string firstInstanceId, string secondInstanceId, out UnitDefinition result, out string message)
        {
            result = null;
            if (string.IsNullOrEmpty(firstInstanceId) || firstInstanceId == secondInstanceId)
            { message = "서로 다른 보유 개체 두 개를 선택하세요."; return false; }
            var first = units.Find(x=>x.instanceId==firstInstanceId);
            var second = units.Find(x=>x.instanceId==secondInstanceId);
            if (first == null || second == null) { message = "선택한 재료가 보관함에 없습니다."; return false; }
            if (!database.TryResolveCraftMerge(first.unitId,second.unitId,out var definition))
            { message = "DB에 없는 제작식입니다. 같은 제작 티어의 재료를 선택하세요."; return false; }
            var next = new List<CraftOwnedUnit>(units);
            next.Remove(first); next.Remove(second);
            next.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),definition.unitId));
            var paths = new HashSet<string>(discovered,StringComparer.Ordinal) { PathKey(first.unitId,second.unitId) };
            if (!Commit(next,paths,out message)) return false;
            result = definition;
            message = definition.name + " 제작 완료!";
            return true;
        }

        private bool Commit(List<CraftOwnedUnit> next, HashSet<string> paths, out string message)
        {
            // Save the entire next state first. A failed write cannot consume inventory in memory.
            try { persist?.Invoke(Snapshot(next,paths)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { message = "저장 실패: 재료를 소모하지 않았습니다. " + ex.Message; return false; }
            units = next; discovered = paths;
            message = "";
            Changed?.Invoke();
            return true;
        }
        private bool Known(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            try { database.GetUnit(id); return true; } catch (KeyNotFoundException) { return false; }
        }
        private static CraftSaveState Snapshot(List<CraftOwnedUnit> owned,HashSet<string> paths)
        {
            var copies = new CraftOwnedUnit[owned.Count];
            for(int i=0;i<owned.Count;i++) copies[i]=new CraftOwnedUnit(owned[i].instanceId,owned[i].unitId);
            var found = new List<string>(paths); found.Sort(StringComparer.Ordinal);
            return new CraftSaveState { units=copies, discoveredPaths=found.ToArray() };
        }
        private static string PathKey(string a,string b) => string.CompareOrdinal(a,b)<=0 ? a+"|"+b : b+"|"+a;
    }

    public sealed class CraftSaveFile
    {
        public string FilePath { get; }
        public bool Exists => File.Exists(FilePath);
        public CraftSaveFile(string path) { FilePath=path; }
        public CraftSaveState Read() => JsonUtility.FromJson<CraftSaveState>(File.ReadAllText(FilePath));
        public void Write(CraftSaveState state)
        {
            var directory=Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temp=FilePath+".tmp";
            File.WriteAllText(temp,JsonUtility.ToJson(state,true));
            if (File.Exists(FilePath)) File.Replace(temp,FilePath,FilePath+".bak");
            else File.Move(temp,FilePath);
        }
    }
}
