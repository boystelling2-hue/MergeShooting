using System;

namespace MergeTD
{
    [Serializable]
    public sealed class MergeGameData
    {
        public int schemaVersion;
        public UnitDefinition[] units;
        public RecipeDefinition[] recipes;
        public ThemeSynergyDefinition[] synergies;
        public MonsterDefinition[] monsters;
        public BuildDefinition[] builds;
        public GameRules rules;
        public BattleMergeRules battleMerge;
        public SummoningRules summoning;
        public BattleLimits battleLimits;
        public EquipmentDefinition[] equipment;
        public SharedGrowthDefinition[] sharedGrowth;
        public CombatStatsDefinition[] combatStats;
        public SkillDefinition[] skills;
        public StageDefinition[] stages;
        public string[] pendingDecisions;
    }

    [Serializable]
    public sealed class UnitDefinition
    {
        public string unitId, name, role, compositionKey;
        public int tier, knight, archer, buffer, healer, mage;
        public string[] themeTags;
        public string attackElement, skillId, skillName, skillDescription;
        public string limitation, buildHint, balanceStatus;

        public int[] GetComposition()
        {
            return new[] { knight, archer, buffer, healer, mage };
        }
    }

    [Serializable]
    public sealed class RecipeDefinition
    {
        public string recipeId, inputA, inputB, resultId;
        public int resultTier;
        public bool shopHint;
    }

    [Serializable]
    public sealed class ThemeSynergyDefinition
    {
        public string tag, name, effectId, description;
        public int[] thresholds;
        public float value2, value4;
    }

    [Serializable]
    public sealed class MonsterDefinition
    {
        public string monsterId, name, rank, theme, behavior, buildHint;
        public string[] weakElements, resistElements;
        public string resistanceStatus;
    }

    [Serializable]
    public sealed class BuildDefinition
    {
        public string buildId, name, plan, strongAgainst, weakAgainst, note;
        public string[] unitIds;
    }

    [Serializable]
    public sealed class GameRules
    {
        public int schemaVersion, maxDeckUnitTypes;
        public float weaknessMultiplier, resistanceMultiplier;
        public bool personalUnitLevelsEnabled;
        public string synergySource;
        public string synergyCountMode, themeStackMode, sameSkillStackMode;
        public bool allDamagingHitsUseUnitElement;
    }


    // Reserved design tables. Empty until balancing and gameplay execution are implemented.
    [Serializable] public sealed class BattleMergeRules
    { public bool configured; public int maxStar, copiesRequired; public float[] starMultipliers; }
    [Serializable] public sealed class SummoningRules
    { public bool configured; public int cost; public UnitWeight[] unitWeights; }
    [Serializable] public sealed class UnitWeight { public string unitId; public float weight; }
    [Serializable] public sealed class BattleLimits
    { public bool configured; public int maxDeployedInstances, equipmentSlotsPerUnit; }
    [Serializable] public sealed class EquipmentDefinition
    { public string equipmentId, type, effectId; public int grade; public float value; }
    [Serializable] public sealed class SharedGrowthDefinition
    { public string growthId, stat; public int level; public double cost; public float value; }
    [Serializable] public sealed class CombatStatsDefinition
    { public string entityId; public float hp, attack, defense, attackSpeed, range, moveSpeed; }
    [Serializable] public sealed class SkillDefinition
    { public string skillId, trigger, target, effectId; public float coefficient, duration, cooldown; }
    [Serializable] public sealed class StageDefinition
    { public string stageId, theme; public WaveDefinition[] waves; public double reward; }
    [Serializable] public sealed class WaveDefinition
    { public string monsterId; public int count; public float spawnInterval; }

    // Mutable per-run state, never stored back into the unit-definition database.
    [Serializable] public sealed class BattleUnitState
    { public string instanceId, unitId; public int star; public float currentHp; public string[] equipmentInstanceIds; }
    [Serializable] public sealed class PlayerProgress
    { public string[] ownedUnitIds, discoveredRecipeIds; public GrowthProgress[] sharedGrowth; }
    [Serializable] public sealed class GrowthProgress { public string growthId; public int level; }

    public sealed class ActiveThemeSynergy
    {
        public string tag, effectId;
        public int uniqueUnitCount, threshold;
        public float value;
    }
}
