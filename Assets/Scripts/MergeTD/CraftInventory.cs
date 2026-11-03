using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MergeTD
{
    [Serializable] public sealed class CraftOwnedUnit
    {
        public string instanceId, unitId;
        public int level=1;
        public int slot=-1;
        public CraftOwnedUnit(string instance,string unit,int level=1,int slot=-1) { instanceId=instance;unitId=unit;this.level=level;this.slot=slot; }
    }
    [Serializable] public sealed class CraftSaveState
    {
        public int version=2;
        public CraftOwnedUnit[] units;
        public string[] discoveredPaths;
        public string[] discoveredUnitIds;
        public int gold=300,levelUpgrade,tierUpgrade;
    }
    public sealed class CraftInventory
    {
        private readonly MergeGameDatabase database;
        private readonly Action<CraftSaveState> persist;
        public GameContent Content { get; }
        private CraftSaveState state;
        public int Count=>state.units.Length;
        public int SlotCount=>state.units.Length==0?0:state.units.Max(u=>u.slot)+1;
        public int DiscoveredCount=>state.discoveredPaths.Length;
        public int DiscoveredUnitCount=>state.discoveredUnitIds.Length;
        public int Gold=>state.gold;
        public int LevelUpgrade=>state.levelUpgrade;
        public int TierUpgrade=>state.tierUpgrade;
        public event Action Changed;
        public CraftInventory(MergeGameDatabase database,Action<CraftSaveState> persist=null)
        {
            if(database==null || !database.IsLoaded) throw new ArgumentException("A loaded database is required.");
            this.database=database;this.persist=persist;Content=new GameContent();
            state=new CraftSaveState {version=3,units=new CraftOwnedUnit[0],discoveredPaths=new string[0],discoveredUnitIds=database.GetAllUnits().Where(u=>u.tier==1).Select(u=>u.unitId).ToArray(),gold=(int)Content.Rules["initialGold"]};
        }
        public CraftOwnedUnit[] GetOwnedUnits()=>Export().units;
        public CraftSaveState Export()=>Copy(state);
        public bool IsDiscovered(string a,string b)=>a!=null&&b!=null&&Array.IndexOf(state.discoveredPaths,PathKey(a,b))>=0;
        public bool IsUnitDiscovered(string id)=>Array.IndexOf(state.discoveredUnitIds,id)>=0;
        public void Restore(CraftSaveState saved)
        {
            if(saved==null || (saved.version!=1&&saved.version!=2&&saved.version!=3) || saved.units==null || saved.discoveredPaths==null)
                throw new InvalidDataException("합성 저장 파일 형식이 올바르지 않습니다.");
            var next=Copy(saved);var ids=new HashSet<string>();var positions=new HashSet<int>();var found=new HashSet<string>();
            foreach(var u in database.GetAllUnits()) if(u.tier==1) found.Add(u.unitId);
            foreach(var item in next.units)
            {
                if(item==null||string.IsNullOrWhiteSpace(item.instanceId)||!ids.Add(item.instanceId)||!Known(item.unitId)) throw new InvalidDataException("저장 개체 ID 오류");
                if(saved.version==1) item.level=1;
                if(saved.version<3) item.slot=positions.Count;
                if(item.slot<0||item.slot>99999||!positions.Add(item.slot)) throw new InvalidDataException("저장 칸 위치 오류");
                if(item.level<1||item.level>Content.MaxLevel) throw new InvalidDataException("저장 유닛 레벨은 1~5여야 합니다");
                found.Add(item.unitId);
            }
            var paths=new HashSet<string>();
            foreach(string path in next.discoveredPaths)
            {
                var pair=path==null?new string[0]:path.Split('|');
                if(pair.Length!=2||path!=PathKey(pair[0],pair[1])||!paths.Add(path)||!database.TryResolveCraftMerge(pair[0],pair[1],out var result)) throw new InvalidDataException("제작 경로 오류");
                found.Add(result.unitId);found.Add(pair[0]);found.Add(pair[1]);
            }
            if(saved.version>=2)
            {
                if(next.gold<0||next.levelUpgrade<0||next.levelUpgrade>MaxPermanent||next.tierUpgrade<0||next.tierUpgrade>MaxPermanent) throw new InvalidDataException("영구 성장 저장 오류");
                if(saved.discoveredUnitIds==null) throw new InvalidDataException("발견 도감 누락");
                foreach(var id in saved.discoveredUnitIds) { if(!Known(id)) throw new InvalidDataException("발견 유닛 오류");found.Add(id); }
            }
            else { next.gold=(int)Content.Rules["initialGold"];next.levelUpgrade=next.tierUpgrade=0; }
            next.version=3;next.discoveredUnitIds=found.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            state=next;Changed?.Invoke();
        }
        public bool TryGrant(string unitId,int amount,out string message,int level=1)
        {
            if(!Known(unitId)||amount<1||amount>100||level<1||level>Content.MaxLevel) {message="지급 유닛·수량·레벨 오류";return false;}
            var next=Export();var list=new List<CraftOwnedUnit>(next.units);
            var occupied=new HashSet<int>(list.Select(u=>u.slot));int free=0;
            for(int i=0;i<amount;i++){while(occupied.Contains(free))free++;if(free>99999){message="보관함이 가득 찼습니다";return false;}list.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),unitId,level,free));occupied.Add(free);}
            next.units=list.ToArray();AddDiscovery(next,unitId);
            if(!Commit(next,out message))return false;message=database.GetUnit(unitId).name+" Lv."+level+" ×"+amount+" 지급";return true;
        }
        public bool CanCraft(string firstId,string secondId,out UnitDefinition result,out int resultLevel,out string message)
        {
            result=null;resultLevel=0;
            if(string.IsNullOrEmpty(firstId)||firstId==secondId){message="서로 다른 개체 두 개가 필요합니다";return false;}
            var a=Array.Find(state.units,x=>x.instanceId==firstId);var b=Array.Find(state.units,x=>x.instanceId==secondId);
            if(a==null||b==null){message="재료가 보관함에 없습니다";return false;}
            if(a.level!=b.level){message="같은 레벨끼리만 합칠 수 있습니다";return false;}
            if(a.level<Content.MaxLevel)
            {
                if(a.unitId!=b.unitId){message="Lv.5 미만에서는 같은 종류끼리 성장합니다";return false;}
                result=database.GetUnit(a.unitId);resultLevel=a.level+1;message="Lv."+resultLevel+" 성장";return true;
            }
            if(!database.TryResolveCraftMerge(a.unitId,b.unitId,out result)){message="Lv.5 재료의 상위 티어 제작식이 없습니다";return false;}
            resultLevel=1;message="상위 티어 합성";return true;
        }
        public bool TryCraft(string firstId,string secondId,out UnitDefinition result,out string message)
        {
            if(!CanCraft(firstId,secondId,out var definition,out int level,out message)){result=null;return false;}
            var next=Export();var a=Array.Find(next.units,x=>x.instanceId==firstId);var b=Array.Find(next.units,x=>x.instanceId==secondId);
            var list=new List<CraftOwnedUnit>(next.units);list.Remove(a);list.Remove(b);list.Add(new CraftOwnedUnit(Guid.NewGuid().ToString("N"),definition.unitId,level,b.slot));next.units=list.ToArray();
            if(a.level==Content.MaxLevel) next.discoveredPaths=next.discoveredPaths.Concat(new[]{PathKey(a.unitId,b.unitId)}).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            AddDiscovery(next,definition.unitId);
            if(!Commit(next,out message)){result=null;return false;}
            result=definition;message=definition.name+" T"+definition.tier+" Lv."+level+" · "+(a.level==Content.MaxLevel?"합성 완료":"성장 완료");return true;
        }
        private int MaxPermanent=>(int)Content.Rules["maxPermanentLevel"];
        public bool TryMove(string instanceId,int slot,out string message)
        {
            var next=Export();var item=Array.Find(next.units,u=>u.instanceId==instanceId);
            if(item==null||slot<0||slot>99999){message="이동할 유닛 또는 칸이 올바르지 않습니다";return false;}
            if(item.slot==slot){message="같은 칸입니다";return false;}
            if(next.units.Any(u=>u.slot==slot)){message="빈 칸으로 이동하세요";return false;}
            item.slot=slot;if(!Commit(next,out message))return false;message="유닛 이동 완료";return true;
        }
        public bool TrySort(out string message)
        {
            var next=Export();next.units=next.units.OrderByDescending(u=>database.GetUnit(u.unitId).tier)
                .ThenBy(u=>u.unitId,StringComparer.Ordinal).ThenByDescending(u=>u.level).ThenBy(u=>u.slot).ToArray();
            for(int i=0;i<next.units.Length;i++)next.units[i].slot=i;
            if(!Commit(next,out message))return false;message="정렬 완료 · 티어 높은 순 → 같은 종류 → 레벨 높은 순";return true;
        }
        public bool TryDiscard(string instanceId,out string message)
        {
            var next=Export();var item=Array.Find(next.units,u=>u.instanceId==instanceId);
            if(item==null){message="버릴 유닛이 보관함에 없습니다";return false;}
            next.units=next.units.Where(u=>u.instanceId!=instanceId).ToArray();
            if(!Commit(next,out message))return false;message=database.GetUnit(item.unitId).name+" Lv."+item.level+" 삭제 완료";return true;
        }
        public int UpgradeCost(bool tier)=>((tier?TierUpgrade:LevelUpgrade)>=MaxPermanent)?0:(int)Content.Rules["permanentBaseCost"]*((tier?TierUpgrade:LevelUpgrade)+1);
        public bool TryUpgradePermanent(bool tier,out string message)
        {
            int cost=UpgradeCost(tier);
            if(cost==0){message="영구 업그레이드 최대 단계입니다";return false;}
            if(Gold<cost){message="골드가 부족합니다";return false;}
            var next=Export();next.gold-=cost;if(tier)next.tierUpgrade++;else next.levelUpgrade++;
            if(!Commit(next,out message))return false;message="영구 업그레이드 완료";return true;
        }
        public bool TryReward(int amount,out string message)
        {
            if(amount<0||amount>1000000||Gold>int.MaxValue-amount){message="보상 오류";return false;}
            var next=Export();next.gold+=amount;return Commit(next,out message);
        }
        public float[] TierProbabilities()
        {
            var available=new bool[3];available[0]=true;
            foreach(string id in state.discoveredUnitIds) available[database.GetUnit(id).tier-1]=true;
            float p2=TierUpgrade*(float)Content.Rules["tier2ChancePerUpgrade"],p3=TierUpgrade*(float)Content.Rules["tier3ChancePerUpgrade"];
            var p=new[]{1-p2-p3,available[1]?p2:0,available[2]?p3:0};float sum=p.Sum();for(int i=0;i<3;i++)p[i]/=sum;return p;
        }
        public float[] LevelProbabilities()
        {
            float mix=LevelUpgrade/(float)MaxPermanent;var p=new float[Content.MaxLevel];
            for(int i=0;i<p.Length;i++)p[i]=(i==0?1-mix:0)+mix*(float)Content.Rules["maxStartLevelProbabilities"][i];return p;
        }
        public bool TryProduce(float tierRoll,float unitRoll,float levelRoll,out string message)
        {
            if(!RollValid(tierRoll)||!RollValid(unitRoll)||!RollValid(levelRoll)){message="제작 추첨 값 오류";return false;}
            int tier=Pick(TierProbabilities(),tierRoll)+1,level=Pick(LevelProbabilities(),levelRoll)+1;
            var pool=state.discoveredUnitIds.Select(database.GetUnit).Where(u=>u.tier==tier).OrderBy(u=>u.unitId,StringComparer.Ordinal).ToArray();
            if(pool.Length==0){message="제작 후보가 없습니다";return false;}
            return TryGrant(pool[Math.Min(pool.Length-1,(int)(unitRoll*pool.Length))].unitId,1,out message,level);
        }
        private static bool RollValid(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=0&&n<1;
        private static int Pick(float[] probabilities,float roll) {float sum=0;for(int i=0;i<probabilities.Length;i++){sum+=probabilities[i];if(roll<sum)return i;}return probabilities.Length-1;}
        private static void AddDiscovery(CraftSaveState next,string id)=>next.discoveredUnitIds=next.discoveredUnitIds.Concat(new[]{id}).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        private bool Commit(CraftSaveState next,out string message)
        {
            try{persist?.Invoke(Copy(next));}catch(Exception ex)when(ex is IOException||ex is UnauthorizedAccessException){message="저장 실패 · 상태를 변경하지 않았습니다. "+ex.Message;return false;}
            state=next;message="";Changed?.Invoke();return true;
        }
        private bool Known(string id) {if(string.IsNullOrWhiteSpace(id))return false;try{database.GetUnit(id);return true;}catch(KeyNotFoundException){return false;}}
        private static CraftSaveState Copy(CraftSaveState s)=>new CraftSaveState {version=s.version,units=s.units==null?null:s.units.Select(u=>u==null?null:new CraftOwnedUnit(u.instanceId,u.unitId,u.level,u.slot)).ToArray(),discoveredPaths=s.discoveredPaths==null?null:(string[])s.discoveredPaths.Clone(),discoveredUnitIds=s.discoveredUnitIds==null?null:(string[])s.discoveredUnitIds.Clone(),gold=s.gold,levelUpgrade=s.levelUpgrade,tierUpgrade=s.tierUpgrade};
        private static string PathKey(string a,string b)=>string.CompareOrdinal(a,b)<=0?a+"|"+b:b+"|"+a;
    }
    public sealed class CraftSaveFile
    {
        public string FilePath {get;}
        public bool Exists=>File.Exists(FilePath);
        public CraftSaveFile(string path){FilePath=path;}
        public CraftSaveState Read()=>JsonUtility.FromJson<CraftSaveState>(File.ReadAllText(FilePath));
        public void Write(CraftSaveState state)
        {
            var directory=Path.GetDirectoryName(FilePath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            string temp=FilePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(state,true));
            if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak");else File.Move(temp,FilePath);
        }
    }
}
