using System;
using System.Collections.Generic;
using UnityEngine;

namespace MergeTD
{
    // Loads definitions only. Inventory changes and combat effects belong to game systems.
    public sealed class MergeGameDatabase : MonoBehaviour
    {
        [SerializeField] private TextAsset gameDataJson;
        private MergeGameData data;
        private Dictionary<string, UnitDefinition> units;
        private Dictionary<string, MonsterDefinition> monsters;
        private Dictionary<string, RecipeDefinition> recipes;
        private static readonly HashSet<string> Elements = new HashSet<string>
        { "물리", "화염", "냉기", "번개", "신성", "암흑", "자연" };
        private static readonly HashSet<string> Themes = new HashSet<string>
        { "궁정", "신성", "비전", "자연", "공학", "암영", "북방", "용맹" };

        public bool IsLoaded { get { return data != null; } }
        public int UnitCount { get { EnsureLoaded(); return units.Count; } }

        private void Awake() { Reload(); }

        [ContextMenu("Reload Game Data")]
        public void Reload()
        {
            if (gameDataJson == null)
                throw new InvalidOperationException("Game Data Json 필드에 game_data.json을 연결하세요.");
            LoadJson(gameDataJson.text);
        }

        public void LoadJson(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON 데이터가 비어 있습니다.");
            MergeGameData next = JsonUtility.FromJson<MergeGameData>(json);
            Require(next != null && next.schemaVersion == 2, "지원하지 않는 데이터 버전");
            Require(next.units != null && next.units.Length > 0, "유닛 목록 누락");
            Require(next.recipes != null && next.monsters != null && next.synergies != null && next.builds != null, "목록 필드 누락");
            Require(next.rules != null && next.rules.schemaVersion == 2, "규칙 누락");
            Require(!Single.IsNaN(next.rules.weaknessMultiplier) && !Single.IsInfinity(next.rules.weaknessMultiplier) && next.rules.weaknessMultiplier >= 1f, "약점 배율 오류");
            Require(next.rules.synergyCountMode == "distinctUnitId" && next.rules.themeStackMode == "highestThreshold", "시너지 규칙 불일치");
            Require(next.rules.allDamagingHitsUseUnitElement, "이 로더의 피해 속성 규칙 불일치");
            Require(next.rules.maxDeckUnitTypes == 5, "출전 덱 최대 5종 필요");
            Require(next.rules.resistanceMultiplier == 0.5f, "강인성 배율은 0.5");
            Require(!next.rules.personalUnitLevelsEnabled, "개별 영구 레벨은 사용하지 않음");
            Require(next.rules.synergySource == "unconfigured" || next.rules.synergySource == "deck" || next.rules.synergySource == "deployed", "시너지 기준 오류");
            Require(next.battleMerge != null && next.summoning != null && next.battleLimits != null, "전투 규칙 구조 누락");
            Require(!next.battleMerge.configured && !next.summoning.configured && !next.battleLimits.configured, "전투 규칙 실행기는 아직 구현되지 않음");
            Require(next.equipment != null && next.sharedGrowth != null && next.combatStats != null && next.skills != null && next.stages != null, "예약 테이블 누락");
            Require(next.equipment.Length == 0 && next.sharedGrowth.Length == 0 && next.combatStats.Length == 0 && next.skills.Length == 0 && next.stages.Length == 0, "예약 테이블은 실행기 구현 전까지 비워 두세요");
            var nextUnits = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            var compositions = new HashSet<string>(StringComparer.Ordinal);
            var skills = new HashSet<string>(StringComparer.Ordinal);
            foreach (UnitDefinition unit in next.units)
            {
                Require(unit != null && !String.IsNullOrWhiteSpace(unit.unitId) && !String.IsNullOrWhiteSpace(unit.name), "유닛 ID/이름 누락");
                Require(!nextUnits.ContainsKey(unit.unitId), "중복 유닛 ID: " + unit.unitId);
                int[] c = unit.GetComposition();
                int total = 0;
                foreach (int n in c) { Require(n >= 0, "음수 재료 수"); total += n; }
                int tier = total == 1 ? 1 : total == 2 ? 2 : total == 4 ? 3 : 0;
                Require(tier > 0 && tier == unit.tier, "유닛 티어 불일치: " + unit.unitId);
                string key = String.Join("_", c);
                Require(key == unit.compositionKey && compositions.Add(key), "유닛 구성 중복/불일치");
                Require(unit.unitId == "U" + tier + "_" + String.Concat(c), "유닛 ID 구성 불일치");
                Require(unit.themeTags != null && unit.themeTags.Length == 2 && unit.themeTags[0] != unit.themeTags[1], "테마 2개 필요");
                foreach (string tag in unit.themeTags) Require(Themes.Contains(tag), "알 수 없는 테마: " + tag);
                Require(Elements.Contains(unit.attackElement), "공격속성 오류");
                Require(!String.IsNullOrWhiteSpace(unit.skillId) && skills.Add(unit.skillId), "능력 ID 누락/중복");
                nextUnits.Add(unit.unitId, unit);
            }
            var nextRecipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            var shopOutputs = new HashSet<string>(StringComparer.Ordinal);
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecipeDefinition recipe in next.recipes)
            {
                Require(recipe != null && !String.IsNullOrWhiteSpace(recipe.recipeId) && recipeIds.Add(recipe.recipeId), "제작식 ID 누락/중복");
                Require(recipe.inputA != null && recipe.inputB != null && recipe.resultId != null, "제작식 재료/결과 ID 누락");
                Require(nextUnits.ContainsKey(recipe.inputA) && nextUnits.ContainsKey(recipe.inputB) && nextUnits.ContainsKey(recipe.resultId), "제작식 유닛 참조 오류");
                UnitDefinition a = nextUnits[recipe.inputA], b = nextUnits[recipe.inputB], result = nextUnits[recipe.resultId];
                Require(a.tier == b.tier && a.tier < 3 && result.tier == a.tier + 1 && recipe.resultTier == result.tier, "제작식 티어 오류");
                int[] ca = a.GetComposition(), cb = b.GetComposition(), cr = result.GetComposition();
                for (int i = 0; i < 5; i++) Require(ca[i] + cb[i] == cr[i], "합성 구성 불일치: " + recipe.recipeId);
                string pair = PairKey(a.unitId, b.unitId);
                Require(!nextRecipes.ContainsKey(pair), "중복 합성 쌍");
                nextRecipes.Add(pair, recipe);
                if (recipe.shopHint) Require(shopOutputs.Add(result.unitId), "중복 상점 대표 경로");
            }
            foreach (UnitDefinition a in next.units)
            {
                if (a.tier >= 3) continue;
                foreach (UnitDefinition b in next.units)
                    if (a.tier == b.tier) Require(nextRecipes.ContainsKey(PairKey(a.unitId,b.unitId)), "합성 경로 누락");
            }
            foreach (UnitDefinition u in next.units)
                if (u.tier > 1) Require(shopOutputs.Contains(u.unitId), "상점 대표 경로 누락");
            var nextMonsters = new Dictionary<string, MonsterDefinition>(StringComparer.Ordinal);
            foreach (MonsterDefinition monster in next.monsters)
            {
                Require(monster != null && !String.IsNullOrWhiteSpace(monster.monsterId) && !nextMonsters.ContainsKey(monster.monsterId), "몬스터 ID 누락/중복");
                Require(monster.weakElements != null && monster.weakElements.Length > 0, "몬스터 약점 누락");
                var weak = new HashSet<string>(StringComparer.Ordinal);
                foreach (string element in monster.weakElements) Require(Elements.Contains(element) && weak.Add(element), "몬스터 약점 오류/중복");
                Require(monster.resistElements != null, "강인성 목록 누락");
                var resist = new HashSet<string>(StringComparer.Ordinal);
                foreach (string element in monster.resistElements)
                    Require(Elements.Contains(element) && resist.Add(element) && !weak.Contains(element), "강인성 오류/중복 또는 약점과 충돌");
                nextMonsters.Add(monster.monsterId, monster);
            }
            var tags = new HashSet<string>(StringComparer.Ordinal);
            foreach (ThemeSynergyDefinition synergy in next.synergies)
            {
                Require(synergy != null && Themes.Contains(synergy.tag) && tags.Add(synergy.tag), "시너지 키워드 오류/중복");
                Require(synergy.thresholds != null && synergy.thresholds.Length == 2 && synergy.thresholds[0] == 2 && synergy.thresholds[1] == 4, "시너지 기준 오류");
                Require(!Single.IsNaN(synergy.value2) && !Single.IsInfinity(synergy.value2) && synergy.value2 >= 0 && !Single.IsNaN(synergy.value4) && !Single.IsInfinity(synergy.value4) && synergy.value4 >= 0, "시너지 배율 오류");
            }
            Require(tags.SetEquals(Themes), "시너지 키워드 누락");
            var buildIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuildDefinition build in next.builds)
            {
                Require(build != null && !String.IsNullOrWhiteSpace(build.buildId) && buildIds.Add(build.buildId), "빌드 ID 누락/중복");
                Require(build.unitIds != null && build.unitIds.Length >= 1 && build.unitIds.Length <= next.rules.maxDeckUnitTypes && new HashSet<string>(build.unitIds).Count == build.unitIds.Length, "편성 인원 불일치");
                foreach (string id in build.unitIds) Require(id != null && nextUnits.ContainsKey(id), "빌드 유닛 참조 오류");
            }
            // Commit only after validation; failed reloads retain the previous valid database.
            units = nextUnits; recipes = nextRecipes; monsters = nextMonsters; data = next;
            Debug.Log("MergeTD 데이터 로드: 유닛 " + units.Count + ", 합성식 " + recipes.Count, this);
        }

        public UnitDefinition GetUnit(string unitId)
        {
            EnsureLoaded();
            return units[unitId];
        }

        public List<UnitDefinition> GetAllUnits()
        {
            EnsureLoaded();
            var result = new List<UnitDefinition>(units.Values);
            result.Sort((a, b) => String.CompareOrdinal(a.unitId, b.unitId));
            return result;
        }

        public List<RecipeDefinition> GetAllCraftRecipes()
        { EnsureLoaded(); return new List<RecipeDefinition>(recipes.Values); }

        // Lookup only. Does not spend inventory items or unlock discoveries.
        [Obsolete("Use TryResolveCraftMerge for permanent crafting; battle star merging is separate.")]
        public bool TryResolveMerge(string firstId, string secondId, out UnitDefinition result)
        { return TryResolveCraftMerge(firstId, secondId, out result); }

        public bool TryResolveCraftMerge(string firstId, string secondId, out UnitDefinition result)
        {
            EnsureLoaded(); result = null;
            if (firstId == null || secondId == null || !units.ContainsKey(firstId) || !units.ContainsKey(secondId)) return false;
            RecipeDefinition recipe;
            if (!recipes.TryGetValue(PairKey(firstId,secondId), out recipe)) return false;
            result = units[recipe.resultId]; return true;
        }

        // Call for damage only, never for healing or shield generation.
        [Obsolete("Use GetElementDamageMultiplier; this method now includes resistance too.")]
        public float GetWeaknessMultiplier(string unitId, string monsterId)
        { return GetElementDamageMultiplier(unitId, monsterId); }

        public float GetElementDamageMultiplier(string unitId, string monsterId)
        {
            EnsureLoaded();
            string element = units[unitId].attackElement;
            foreach (string weak in monsters[monsterId].weakElements)
                if (String.Equals(element, weak, StringComparison.Ordinal)) return data.rules.weaknessMultiplier;
            foreach (string resist in monsters[monsterId].resistElements)
                if (String.Equals(element, resist, StringComparison.Ordinal)) return data.rules.resistanceMultiplier;
            return 1f;
        }

        public bool IsValidDeck(IEnumerable<string> unitIds)
        {
            EnsureLoaded();
            if (unitIds == null) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in unitIds)
                if (id == null || !units.ContainsKey(id) || !seen.Add(id) || seen.Count > data.rules.maxDeckUnitTypes) return false;
            return seen.Count > 0;
        }

        // Caller supplies deck and deployed IDs separately. Never silently choose the pending design rule.
        public List<ActiveThemeSynergy> GetBattleSynergies(IEnumerable<string> deckIds, IEnumerable<string> deployedIds)
        {
            EnsureLoaded();
            var deck = deckIds == null ? new List<string>() : new List<string>(deckIds);
            Require(IsValidDeck(deck), "출전 덱은 중복 없는 1~5종 필요");
            if (data.rules.synergySource == "deck") return GetActiveSynergies(deck);
            Require(data.rules.synergySource == "deployed", "시너지 적용 기준 미설정: deck/deployed 결정 필요");
            Require(deployedIds != null, "배치 목록 누락");
            var deployed = new List<string>(deployedIds);
            var allowed = new HashSet<string>(deck);
            foreach (string id in deployed) Require(id != null && allowed.Contains(id), "덱 밖의 유닛 배치 불가");
            return GetActiveSynergies(deployed);
        }

        public Dictionary<string,int> GetThemeCounts(IEnumerable<string> formationIds)
        {
            EnsureLoaded();
            if (formationIds == null) throw new ArgumentNullException(nameof(formationIds));
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var result = new Dictionary<string,int>(StringComparer.Ordinal);
            foreach (string id in formationIds)
            {
                Require(id != null && units.ContainsKey(id), "편성 유닛 ID 오류");
                if (!unique.Add(id)) continue;
                foreach (string tag in units[id].themeTags)
                {
                    int current; result.TryGetValue(tag,out current); result[tag] = current + 1;
                }
            }
            return result;
        }

        public List<ActiveThemeSynergy> GetActiveSynergies(IEnumerable<string> formationIds)
        {
            var counts = GetThemeCounts(formationIds);
            var result = new List<ActiveThemeSynergy>();
            foreach (ThemeSynergyDefinition synergy in data.synergies)
            {
                int count; counts.TryGetValue(synergy.tag,out count);
                if (count < 2) continue;
                bool upper = count >= 4;
                result.Add(new ActiveThemeSynergy { tag=synergy.tag, effectId=synergy.effectId, uniqueUnitCount=count, threshold=upper?4:2, value=upper?synergy.value4:synergy.value2 });
            }
            return result;
        }

        private static string PairKey(string a, string b)
        {
            return String.CompareOrdinal(a,b) <= 0 ? a + "|" + b : b + "|" + a;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private void EnsureLoaded()
        {
            if (data == null) throw new InvalidOperationException("데이터를 먼저 로드하세요. 다른 스크립트의 Awake에서는 실행 순서를 보장하세요.");
        }
    }
}
