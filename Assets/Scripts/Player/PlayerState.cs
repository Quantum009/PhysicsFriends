// ============================================================
// PlayerState.cs — 玩家状态：运行时每个玩家的所有数据
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Cards;
using PhysicsFriends.Data;

namespace PhysicsFriends.Player
{
    /// <summary>
    /// 玩家的运行时状态，包含手牌、mol、位置、状态效果等
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        // === 基本信息 ===
        public int playerIndex;         // 玩家序号（0~3）
        public PlayerColor color;       // 玩家选择的颜色
        public Character character;     // 玩家选择的角色
        public string playerName;       // 玩家名字

        // === 资源 ===
        // 胜利条件检查回调：数值变化时自动触发
        public Action<PlayerState> OnVictoryCheck;

        private int _mol;
        /// <summary>物质的量（货币），变化时自动检查胜利条件</summary>
        public int mol
        {
            get => _mol;
            set { _mol = value; OnVictoryCheck?.Invoke(this); }
        }

        private int _achievementPoints;
        /// <summary>已获得的创举点数，变化时自动检查胜利条件</summary>
        public int achievementPoints
        {
            get => _achievementPoints;
            set { _achievementPoints = value; OnVictoryCheck?.Invoke(this); }
        }
        public List<CardInstance> handCards; // 手牌列表（物理量牌）
        public List<RewardCardId> rewardItems; // 持有的奖励物品（金皇冠等创举奖励道具）

        // === 位置与移动 ===
        public int position;            // 当前棋盘格子索引
        public MoveDirection moveDirection; // 行进方向（顺时针/逆时针）
        public int lastMoveSteps;       // 上一回合实际移动的步数（用于匀速检查）
        public List<int> recentMoveSteps; // 最近几回合的移动步数记录（用于相对论/牛顿力学）

        // === 跳过回合 ===
        public int stunTurns;           // 眩晕剩余回合数（动量撞晕）
        public int absoluteZeroTurns;   // 绝对零度剩余回合数
        public int roadblockSkipTurns;  // 路障跳过剩余回合数
        public bool phaseSkipNext;      // 相变-固态跳过下一回合标记

        // === 电势特殊行动 ===
        public bool potentialCharging;  // 电势充能中（经过起点后标记）
        public int potentialSkipTurns;  // 电势跳过回合数
        public int potentialExtraTurns; // 电势连续行动剩余回合数

        // === 迈克尔逊-莫雷实验 ===
        public int michelsonMorleyTurns; // 步数固定为6的剩余回合数

        // === 沉重/轻盈 ===
        public int heavyLayers;         // 沉重层数（来自各种效果的累计）
        public int lightLayers;         // 轻盈层数（来自各种效果的累计）
        public int densityHeavyTurns;   // 密度造成的沉重剩余回合
        public int densityLightTurns;   // 密度造成的轻盈剩余回合

        // === 相变状态 ===
        public PhaseState phaseState;   // 当前相变状态

        // === 电磁屏蔽 ===
        public int emShieldTurns;       // 电磁屏蔽剩余回合数

        // === 创举进度 ===
        // achievementPoints 已移至上方定义为属性
        public HashSet<AchievementId> completedAchievements; // 已完成的创举
        // 牛顿力学子任务进度
        public int newtonUniformTurns;  // 连续匀速回合数
        public bool newtonHasAllCards;  // 是否同时持有质量+加速度+力
        public bool newtonUsedForce;    // 是否用力抵消了他人的力
        // 相对论子任务进度
        public int relativityFastTurns; // 连续≥6格的回合数
        // 位移电流进度
        public bool hasCompletedDisplacementVector; // 是否已合成电位移矢量
        public bool hasCompletedDisplacementFlux;   // 是否已合成电位移通量

        // === 人物任务 ===
        public bool characterTaskCompleted; // 人物专属任务是否完成
        public int entropyUseCount;     // 薛定谔任务：使用熵的次数

        // === 创举奖励道具 ===
        public bool hasGoldenCrown;     // 金皇冠（免于一次不幸事件）
        public bool hasMengXiBiTan;     // 《梦溪笔谈》（磁感应强度增益翻倍）
        public bool hasMoJing;          // 《墨经》（每过起点+1光照强度）
        public bool hasPrincipia;       // 《自然哲学的数学原理》（力改为+2/+1/-1/-2）
        public bool hasHeatEngine;      // 热机（两张温度→一张能量）

        // === 电磁铁 ===
        public int electromagnetTurns;  // 电磁铁持续回合数
        public int electromagnetPosition; // 电磁铁放置位置（-1=未放置）

        // === 力使用记录 ===
        public bool forceUsedThisRound; // 本轮是否已使用"力"效果

        // === 建筑 ===
        public List<BuildingInstance> buildings; // 该玩家建造的建筑列表

        // === 路障/阻挡状态 ===
        public bool stoppedByRoadblock; // 本回合被路障/电磁铁阻挡

        // === 建筑经过收费 ===
        public List<int> pendingTollTiles; // 本回合经过的待收费建筑格

        // === 角色任务进度（新增）===
        public bool newtonStage1Done;     // 牛顿阶段1：连续3回合匀速
        public bool newtonStage2Done;     // 牛顿阶段2：使用过力修正自己
        public int consecutiveHighSpeedTurns; // 爱因斯坦：连续高速回合数
        public int skipTurns;             // 通用跳过回合数（动量撞晕等）
        public int entropyUsedCount;      // 熵使用次数（用于新CardEffectProcessor）
        public int currentTile { get => position; set => position = value; } // 别名

        // === 量子隧穿 ===
        public bool quantumTunnelingUsed; // 是否使用过量子隧穿（创举#14用）
        public bool superconductorTriggered; // 是否触发过超导事件（创举#15用）

        // === 牛顿力学子任务（另一组追踪）===
        public bool newtonTask_Inertia;      // 惯性定律完成（连续3回合匀速）
        public bool newtonTask_Acceleration; // 加速度定律完成（同时拥有质量+加速度+力）
        public bool newtonTask_Reaction;     // 作用反作用完成（用力抵消他人的力）

        // === 浮力定律创举追踪 ===
        public bool buoyancySynthesisCompleted; // 是否利用密度+加速度+长度合成了力

        // === 位移电流创举追踪 ===
        public bool displacementCurrentCompleted; // 是否用电位移通量合成了电流
        public bool singularityLostAll;           // 奇点事件中失去一切（创举11触发标记）

        // === 创举掷骰检查（Phase2通过的创举）===
        public HashSet<AchievementId> achievementDiceCheckPassed;

        // === 手牌上限 ===
        public const int MaxHandCards = 10;

        /// <summary>构造函数：初始化所有字段</summary>
        public PlayerState(int index, PlayerColor color, Character character, string name)
        {
            this.playerIndex = index;
            this.color = color;
            this.character = character;
            this.playerName = name;

            mol = 0;                                // 初始mol在Setup时设置
            handCards = new List<CardInstance>();    // 手牌初始为空
            rewardItems = new List<RewardCardId>(); // 奖励物品初始为空
            position = 0;                           // 起点位置
            moveDirection = MoveDirection.Clockwise; // 默认顺时针
            lastMoveSteps = 0;                      // 无历史移动
            recentMoveSteps = new List<int>();       // 空的移动记录

            // 跳过回合全部归零
            stunTurns = 0;
            absoluteZeroTurns = 0;
            roadblockSkipTurns = 0;
            phaseSkipNext = false;

            // 电势
            potentialCharging = false;
            potentialSkipTurns = 0;
            potentialExtraTurns = 0;

            // 迈克尔逊-莫雷
            michelsonMorleyTurns = 0;

            // 沉重/轻盈
            heavyLayers = 0;
            lightLayers = 0;
            densityHeavyTurns = 0;
            densityLightTurns = 0;

            // 相变
            phaseState = PhaseState.None;

            // 电磁屏蔽
            emShieldTurns = 0;

            // 创举
            achievementPoints = 0;
            completedAchievements = new HashSet<AchievementId>();
            newtonUniformTurns = 0;
            newtonHasAllCards = false;
            newtonUsedForce = false;
            relativityFastTurns = 0;
            hasCompletedDisplacementVector = false;
            hasCompletedDisplacementFlux = false;

            // 人物任务
            characterTaskCompleted = false;
            entropyUseCount = 0;
            entropyUsedCount = 0;
            newtonStage1Done = false;
            newtonStage2Done = false;
            consecutiveHighSpeedTurns = 0;
            skipTurns = 0;
            pendingTollTiles = new List<int>();

            // 创举奖励道具
            hasGoldenCrown = false;
            hasMengXiBiTan = false;
            hasMoJing = false;
            hasPrincipia = false;
            hasHeatEngine = false;

            // 电磁铁
            electromagnetTurns = 0;
            electromagnetPosition = -1;

            // 力使用
            forceUsedThisRound = false;

            // 路障/阻挡
            stoppedByRoadblock = false;

            // 量子隧穿
            quantumTunnelingUsed = false;
            superconductorTriggered = false;

            // 牛顿力学子任务
            newtonTask_Inertia = false;
            newtonTask_Acceleration = false;
            newtonTask_Reaction = false;

            // 浮力定律创举追踪
            buoyancySynthesisCompleted = false;

            // 位移电流创举追踪
            displacementCurrentCompleted = false;
            singularityLostAll = false;

            // 创举掷骰检查
            achievementDiceCheckPassed = new HashSet<AchievementId>();

            // 建筑
            buildings = new List<BuildingInstance>();
        }

        // ====== 辅助方法 ======

        /// <summary>检查是否拥有指定卡牌</summary>
        public bool HasCard(PhysicsCardId cardId)
        {
            return handCards.Any(c => c.cardId == cardId && !c.isUsed);
        }

        /// <summary>计算玩家拥有指定卡牌ID的数量</summary>
        public int CountCards(PhysicsCardId cardId)
        {
            return handCards.Count(c => c.cardId == cardId && !c.isUsed);
        }

        /// <summary>计算玩家拥有的光照强度牌数量</summary>
        public int LuminousIntensityCount()
        {
            return CountCards(PhysicsCardId.LuminousIntensity);
        }

        /// <summary>计算光照强度增益值（考虑普朗克常量）</summary>
        public int CalculateLightBonus()
        {
            int lightCount = LuminousIntensityCount();    // 光照强度牌数量
            if (lightCount == 0) return 0;                // 没有光照则无增益
            bool hasPlanck = CountCards(PhysicsCardId.PlanckConstant) > 0; // 是否有普朗克常量
            return lightCount * (hasPlanck ? 2 : 1);      // 有普朗克则增益翻倍
        }

        /// <summary>手牌是否已满（10张上限）</summary>
        public bool IsHandFull()
        {
            return handCards.Count >= MaxHandCards;
        }

        /// <summary>给予玩家一张卡牌（如果手牌已满且不在允许合成的回合，弃掉新牌）</summary>
        public CardInstance GiveCard(PhysicsCardId cardId)
        {
            if (handCards.Count >= MaxHandCards)
            {
                // 手牌已满，新卡牌被丢弃
                Debug.Log($"[手牌] {playerName} 手牌已满({MaxHandCards}张)，{CardDatabase.Get(cardId)?.nameZH}被丢弃");
                return null;
            }
            var card = new CardInstance(cardId);  // 创建新的卡牌实例
            handCards.Add(card);                  // 添加到手牌
            return card;                          // 返回创建的实例
        }

        /// <summary>强制给予卡牌（忽略手牌上限，合成等场景中间过程用）</summary>
        public CardInstance ForceGiveCard(PhysicsCardId cardId)
        {
            var card = new CardInstance(cardId);
            handCards.Add(card);
            return card;
        }

        /// <summary>移除一张指定的卡牌实例</summary>
        public bool RemoveCard(CardInstance card)
        {
            return handCards.Remove(card); // 从手牌列表移除
        }

        /// <summary>移除一张指定类型的卡牌（移除第一张匹配的）</summary>
        public CardInstance RemoveCardById(PhysicsCardId cardId)
        {
            var card = handCards.FirstOrDefault(c => c.cardId == cardId);
            if (card != null)
                handCards.Remove(card); // 找到并移除
            return card;                // 返回被移除的卡牌（可能为null）
        }

        /// <summary>检查是否需要跳过本回合，返回跳过原因，null表示不跳过</summary>
        public SkipReason? CheckSkip()
        {
            if (stunTurns > 0)              // 检查眩晕
            {
                stunTurns--;                // 减少剩余回合
                return SkipReason.Stun;
            }
            if (absoluteZeroTurns > 0)      // 检查绝对零度
            {
                absoluteZeroTurns--;
                return SkipReason.AbsoluteZero;
            }
            if (potentialSkipTurns > 0)     // 检查电势跳过
            {
                potentialSkipTurns--;
                return SkipReason.Potential;
            }
            if (phaseSkipNext)              // 检查相变-固态
            {
                phaseSkipNext = false;      // 只跳过一次
                return SkipReason.PhaseSolid;
            }
            if (roadblockSkipTurns > 0)     // 检查路障
            {
                roadblockSkipTurns--;
                return SkipReason.Roadblock;
            }
            return null;                    // 不需要跳过
        }

        /// <summary>检查玩家是否拥有任何电学量（不含电流）</summary>
        public bool HasNonCurrentElectrical()
        {
            return handCards.Any(c =>
                CardDatabase.IsElectrical(c.cardId) &&
                c.cardId != PhysicsCardId.Current &&
                !c.isUsed);
        }

        /// <summary>检查玩家是否拥有任何磁学量</summary>
        public bool HasMagnetic()
        {
            return handCards.Any(c => CardDatabase.IsMagnetic(c.cardId) && !c.isUsed);
        }

        /// <summary>获取所有能量量纲的卡牌数量（功/能量/热量/力矩 — 它们有相同的量纲 m²·s⁻²·kg）</summary>
        public int CountEnergyCards()
        {
            return handCards.Count(c =>
                (c.cardId == PhysicsCardId.Work ||
                 c.cardId == PhysicsCardId.Energy ||
                 c.cardId == PhysicsCardId.Heat ||
                 c.cardId == PhysicsCardId.Torque) &&
                !c.isUsed);
        }

        /// <summary>深拷贝玩家状态（用于时间机器快照）</summary>
        public PlayerState DeepCopy()
        {
            var copy = new PlayerState(playerIndex, color, character, playerName);

            // 复制资源
            copy._mol = this._mol;
            copy.handCards = this.handCards.Select(c => c.DeepCopy()).ToList(); // 深拷贝每张手牌
            copy.rewardItems = new List<RewardCardId>(this.rewardItems);

            // 复制位置信息
            copy.position = this.position;
            copy.moveDirection = this.moveDirection;
            copy.lastMoveSteps = this.lastMoveSteps;
            copy.recentMoveSteps = new List<int>(this.recentMoveSteps);

            // 复制跳过状态
            copy.stunTurns = this.stunTurns;
            copy.absoluteZeroTurns = this.absoluteZeroTurns;
            copy.roadblockSkipTurns = this.roadblockSkipTurns;
            copy.phaseSkipNext = this.phaseSkipNext;

            // 复制电势状态
            copy.potentialCharging = this.potentialCharging;
            copy.potentialSkipTurns = this.potentialSkipTurns;
            copy.potentialExtraTurns = this.potentialExtraTurns;

            // 复制其他回合效果
            copy.michelsonMorleyTurns = this.michelsonMorleyTurns;
            copy.heavyLayers = this.heavyLayers;
            copy.lightLayers = this.lightLayers;
            copy.densityHeavyTurns = this.densityHeavyTurns;
            copy.densityLightTurns = this.densityLightTurns;
            copy.phaseState = this.phaseState;
            copy.emShieldTurns = this.emShieldTurns;

            // 复制创举进度
            copy._achievementPoints = this._achievementPoints;
            copy.completedAchievements = new HashSet<AchievementId>(this.completedAchievements);
            copy.newtonUniformTurns = this.newtonUniformTurns;
            copy.newtonHasAllCards = this.newtonHasAllCards;
            copy.newtonUsedForce = this.newtonUsedForce;
            copy.relativityFastTurns = this.relativityFastTurns;
            copy.hasCompletedDisplacementVector = this.hasCompletedDisplacementVector;
            copy.hasCompletedDisplacementFlux = this.hasCompletedDisplacementFlux;

            // 复制人物任务
            copy.characterTaskCompleted = this.characterTaskCompleted;
            copy.entropyUseCount = this.entropyUseCount;

            // 复制创举奖励道具
            copy.hasGoldenCrown = this.hasGoldenCrown;
            copy.hasMengXiBiTan = this.hasMengXiBiTan;
            copy.hasMoJing = this.hasMoJing;
            copy.hasPrincipia = this.hasPrincipia;
            copy.hasHeatEngine = this.hasHeatEngine;

            // 复制电磁铁
            copy.electromagnetTurns = this.electromagnetTurns;
            copy.electromagnetPosition = this.electromagnetPosition;
            copy.forceUsedThisRound = this.forceUsedThisRound;
            copy.stoppedByRoadblock = this.stoppedByRoadblock;
            copy.quantumTunnelingUsed = this.quantumTunnelingUsed;
            copy.superconductorTriggered = this.superconductorTriggered;
            copy.newtonTask_Inertia = this.newtonTask_Inertia;
            copy.newtonTask_Acceleration = this.newtonTask_Acceleration;
            copy.newtonTask_Reaction = this.newtonTask_Reaction;
            copy.buoyancySynthesisCompleted = this.buoyancySynthesisCompleted;
            copy.displacementCurrentCompleted = this.displacementCurrentCompleted;
            copy.singularityLostAll = this.singularityLostAll;
            copy.achievementDiceCheckPassed = new HashSet<AchievementId>(this.achievementDiceCheckPassed);

            // 深拷贝建筑
            copy.buildings = this.buildings.Select(b => b.DeepCopy()).ToList();

            return copy;
        }
    }

    /// <summary>建筑实例</summary>
    [Serializable]
    public class BuildingInstance
    {
        public BuildingType type;       // 建筑类型
        public int tileIndex;           // 所在格子索引
        public int ownerIndex;          // 建造者玩家索引

        public BuildingInstance(BuildingType type, int tileIndex, int ownerIndex)
        {
            this.type = type;
            this.tileIndex = tileIndex;
            this.ownerIndex = ownerIndex;
        }

        /// <summary>获取经过此建筑需要支付的mol数</summary>
        public int GetToll()
        {
            switch (type)
            {
                case BuildingType.Laboratory: return 1;         // 实验室收1mol
                case BuildingType.ResearchInstitute: return 2;  // 研究所收2mol
                case BuildingType.LargeCollider: return 5;      // 大型对撞机收5mol
                default: return 0;                              // 天文台不收mol（交卡牌）
            }
        }

        /// <summary>深拷贝</summary>
        public BuildingInstance DeepCopy()
        {
            return new BuildingInstance(type, tileIndex, ownerIndex);
        }
    }
}
