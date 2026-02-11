// ============================================================
// ScriptableObjectDefinitions.cs — 所有 ScriptableObject 类定义
// 用于将硬编码数据迁移到可视化编辑的 SO 资产
// ============================================================
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

namespace PhysicsFriends.Data.SO
{
    // ================================================================
    // 物理量牌 SO
    // ================================================================

    [CreateAssetMenu(fileName = "NewCard", menuName = "PhysicsFriends/Card Definition")]
    public class CardDefinitionSO : ScriptableObject
    {
        [Header("基本信息")]
        public PhysicsCardId cardId;
        public string cardName;          // "力", "能量"
        public string cardNameEn;        // "Force", "Energy"
        [TextArea] public string description;
        public Sprite cardArt;
        public CardCategory category;

        [Header("量纲 [m, s, kg, K, A, cd]")]
        public Dimension dimension;

        [Header("效果")]
        public CardEffectType effectType;  // Passive / Active / Choice
        public bool isBasicQuantity;       // 基本物理量？

        [Header("牌库")]
        public int startingCount;          // 初始牌堆数量
        public int totalCount;             // 全部数量（含发给玩家的）

        [Header("特殊")]
        public int containerCapacity;      // 容器上限（面积=2, 体积=5, 其余=0）
        public bool affectedByLight;       // 是否受光照强度影响
    }

    public enum CardCategory
    {
        BasicMechanics,    // 力学基本量（时间/长度/质量）
        Mechanics,         // 力学量
        Electromagnetics,  // 电磁学量
        Thermodynamics,    // 热学量
        Optics,            // 光学量
        Special            // 特殊量
    }

    // ================================================================
    // 事件牌 SO
    // ================================================================

    [CreateAssetMenu(fileName = "NewEvent", menuName = "PhysicsFriends/Event Card")]
    public class EventCardSO : ScriptableObject
    {
        public EventCardId eventId;
        public string eventName;
        [TextArea] public string description;
        public Sprite cardArt;
        public bool isNegative;            // 金皇冠可免疫
        public bool requiresDiceRoll;
        public int diceThreshold;
        public bool affectsAllPlayers;
    }

    // ================================================================
    // 奖励牌 SO
    // ================================================================

    [CreateAssetMenu(fileName = "NewReward", menuName = "PhysicsFriends/Reward Card")]
    public class RewardCardSO : ScriptableObject
    {
        public RewardCardId rewardId;
        public string rewardName;
        [TextArea] public string description;
        public Sprite cardArt;
        public bool requiresActivation;    // 需要额外资源激活？
        public bool isPermanent;           // 持久性奖励？
    }

    // ================================================================
    // 创举牌 SO
    // ================================================================

    [CreateAssetMenu(fileName = "NewAchievement", menuName = "PhysicsFriends/Achievement")]
    public class AchievementSO : ScriptableObject
    {
        public AchievementId achievementId;
        public string achievementName;
        [TextArea] public string description;
        public Era era;
        public int points;

        [Header("奖励")]
        public int molReward;
        public PhysicsCardId[] cardRewards;
        public RewardCardSO specialReward;
    }

    // ================================================================
    // 角色 SO
    // ================================================================

    [CreateAssetMenu(fileName = "NewCharacter", menuName = "PhysicsFriends/Character")]
    public class CharacterSO : ScriptableObject
    {
        public Character characterType;
        public string characterName;       // "牛顿"
        [TextArea] public string taskDescription;
        [TextArea] public string rewardDescription;
        public Sprite portrait;
        public Sprite pawnSprite;
    }

    // ================================================================
    // 游戏配置 SO
    // ================================================================

    [CreateAssetMenu(fileName = "GameConfig", menuName = "PhysicsFriends/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("快速模式")]
        public int fastWealthGoal = 30;
        public int fastAchievementGoal = 4;
        public int fastStartingMol = 5;

        [Header("标准模式")]
        public int standardWealthGoal = 60;
        public int standardAchievementGoal = 6;
        public int standardStartingMol = 10;

        [Header("慢速模式")]
        public int slowWealthGoal = 120;
        public int slowAchievementGoal = 8;
        public int slowStartingMol = 15;

        [Header("通用")]
        public int maxHandSize = 10;
        public int innerBoardTiles = 24;
        public int outerBoardTiles = 72;
        public int maxRoundsPerEra = 15;
        public int achievementsPerEra = 2;

        [Header("商店价格 (自然哲学/经典/现代)")]
        public int[] randomCardPrice = { 2, 3, 5 };
        public int[] chooseCardPrice = { 5, 6, 8 };

        public int GetWealthGoal(GameMode mode) => mode switch
        {
            GameMode.Fast => fastWealthGoal,
            GameMode.Standard => standardWealthGoal,
            GameMode.Slow => slowWealthGoal,
            _ => standardWealthGoal
        };

        public int GetAchievementGoal(GameMode mode) => mode switch
        {
            GameMode.Fast => fastAchievementGoal,
            GameMode.Standard => standardAchievementGoal,
            GameMode.Slow => slowAchievementGoal,
            _ => standardAchievementGoal
        };

        public int GetStartingMol(GameMode mode) => mode switch
        {
            GameMode.Fast => fastStartingMol,
            GameMode.Standard => standardStartingMol,
            GameMode.Slow => slowStartingMol,
            _ => standardStartingMol
        };
    }

    // ================================================================
    // 数据库注册表：运行时统一加载所有 SO
    // ================================================================

    [CreateAssetMenu(fileName = "CardRegistry", menuName = "PhysicsFriends/Card Registry")]
    public class CardRegistrySO : ScriptableObject
    {
        public CardDefinitionSO[] allCards;
        public EventCardSO[] allEvents;
        public RewardCardSO[] allRewards;
        public AchievementSO[] allAchievements;
        public CharacterSO[] allCharacters;
        public GameConfigSO gameConfig;
    }
}
