using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MergeTD
{
    // The runtime reads the authored JSON; battle instances never modify definition data.
    public sealed class GameContent
    {
        public readonly JObject Rules;
        private readonly Dictionary<string,JObject> profiles = new Dictionary<string,JObject>();
        private readonly Dictionary<string,JObject> paths = new Dictionary<string,JObject>();
        private static JObject Read(string name)
        {
            var asset=Resources.Load<TextAsset>(name);
            if(asset==null) throw new InvalidOperationException("Missing runtime database: "+name);
            return JObject.Parse(asset.text);
        }
        public GameContent()
        {
            Rules=Read("progression_rules");
            foreach(JObject unit in Read("combat_profiles")["units"]) profiles.Add((string)unit["unitId"],unit);
            foreach(JObject unit in Read("tower_paths")["units"]) paths.Add((string)unit["unitId"],unit);
            if(MaxLevel!=5 || Rules["levelCaps"].Count()!=5 || profiles.Count!=90 || paths.Count!=90 || !profiles.Keys.All(paths.ContainsKey))
                throw new InvalidOperationException("Invalid runtime game database.");
            foreach(var unit in paths.Values)
                if(unit["paths"].Count()!=3 || unit["paths"].Any(p=>p["stages"].Count()!=5)) throw new InvalidOperationException("Invalid upgrade paths.");
        }
        public int MaxLevel => (int)Rules["maxLevel"];
        public JObject Profile(string id) => profiles[id];
        public JObject Path(string id,int path) => (JObject)paths[id]["paths"][path];
        public int Cap(int level) => level>=1 && level<=MaxLevel ? (int)Rules["levelCaps"][level-1] : 0;
        public bool ValidPaths(int level,int[] stages)
        {
            if(level<1 || level>MaxLevel || stages==null || stages.Length!=3) return false;
            int used=0,advanced=0;
            foreach(int n in stages) { if(n<0 || n>Cap(level)) return false; if(n>0)used++;if(n>(int)Rules["secondaryPathCap"])advanced++; }
            return used<=(int)Rules["maxPurchasedPaths"] && advanced<=1;
        }
        public bool CanUpgrade(int level,int[] stages,int path,out string reason)
        {
            reason="";
            if(!ValidPaths(level,stages) || path<0 || path>2) { reason="특성 상태가 올바르지 않습니다";return false; }
            if(stages[path]>=Cap(level)) { reason="유닛 Lv."+level+"의 경로 상한입니다";return false; }
            var next=(int[])stages.Clone();next[path]++;
            if(!ValidPaths(level,next)) { reason="두 경로만 투자 가능 · 보조 경로는 2단계까지";return false; }
            return true;
        }
        public int UpgradeCost(int tier,int nextStage) => (int)Rules["upgradeCosts"][nextStage-1]*tier;
        public static float Number(JToken token,string key,float fallback=0) => token?[key]?.Type==JTokenType.Null ? fallback : (float?)(token?[key]) ?? fallback;
        public static string Text(JToken token,string key,string fallback="") => (string)token?[key] ?? fallback;
        public static bool Flag(JToken token,string key) => (bool?)(token?[key]) ?? false;
        public static string EffectDescription(string kind,int stage)
        {
            string core;
            switch(kind)
            {
                case "area":core="범위 피해 +15%, 폭발 반경 +8%";break;
                case "dot":core="적중 시 공격력의 12% 지속 피해(3초), 연소 효율 증가";break;
                case "burst":case "single":core="주 목표 피해 +20%";break;
                case "heal":core="주변 아군 소형 회복, 회복량 +18%";break;
                case "regen":core="주변 부상 아군 재생, 회복 효율 +15%";break;
                case "shield":core="주변 아군 보호막, 용량 +18%";break;
                case "aura":core="주변 아군 공격 지원 +4%";break;
                case "tempo":core="공격 및 능력 발동 속도 +12%";break;
                case "guard":core="주변 아군 피해 감소 +3%";break;
                case "multi":core="보조 타격 대상 +1 (보조 피해 30%)";break;
                case "reach":core="공격·지원 사거리 +10%";break;
                case "tank":core="최대 체력 +18%, 자신 피해 감소 +3%";break;
                case "counter":core="직접 피격 시 공격력 15% 반격(최소 간격 0.5초)";break;
                case "control":core="적중 시 둔화 강화(상한 적용)";break;
                case "mark":core="표식 대상의 받는 직접 피해 +4%";break;
                case "cleanse":core="주기 정화 및 정화 대상 소형 보호막";break;
                case "weakness":core="약점 직접 공격 피해 +12%";break;
                case "pierce":core="직선 관통 최대 대상 +1";break;
                case "chain":core="공격 시 보조 연쇄 대상 +1 (보조 피해 30%)";break;
                case "link":core="지원 대상 +1, 지원 효율 +8%";break;
                default:throw new InvalidOperationException("Unknown path archetype: "+kind);
            }
            return "누적 "+stage+"단계 · "+core+" / 단계"+(stage==5?"\n완성: 해당 경로 효과 1.5배":"");
        }
    }
}

