// ============================================================
// TurnRecord.cs — 回合记录系统：每回合完整行为存档
// 记录每个回合中发生的所有事件，用于回放/调试/UI展示
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Cards;

namespace PhysicsFriends.Systems
{
    /// <summary>单条行为记录</summary>
    [Serializable]
    public class TurnAction
    {
        public TurnActionType type;     // 行为类型
        public string description;      // 可读描述
        public int sourcePlayer;        // 发起者玩家索引（-1=系统）
        public int targetPlayer;        // 目标玩家索引（-1=无）
        public int intValue;            // 通用整数值（mol变化/骰子点数/步数等）
        public string extraData;        // 附加数据（卡牌名/事件名等）

        public TurnAction(TurnActionType type, string desc, int source = -1,
            int target = -1, int value = 0, string extra = "")
        {
            this.type = type;
            this.description = desc;
            this.sourcePlayer = source;
            this.targetPlayer = target;
            this.intValue = value;
            this.extraData = extra;
        }
    }

    /// <summary>行为类型枚举</summary>
    public enum TurnActionType
    {
        // 回合流程
        TurnStart,              // 回合开始
        TurnSkipped,            // 回合被跳过
        TurnEnd,                // 回合结束

        // 骰子
        DiceRoll,               // 掷骰原始点数
        DiceReroll,             // 薛定谔重掷
        DiceModForce,           // 力修正
        DiceModAcceleration,    // 加速度修正
        DiceModSpring,          // 劲度系数钳制
        DiceModPressure,        // 压强无效化
        DiceFinalValue,         // 修正后最终点数

        // 步数
        StepCalculation,        // 沉重/轻盈步数计算
        MichelsonFixed,         // 迈克尔逊-莫雷固定步数

        // 移动
        MoveStep,               // 移动一格
        PassedStart,            // 经过起点
        StoppedByRoadblock,     // 被路障/电磁铁阻挡

        // 被动效果（经过起点触发）
        PassiveCurrentMol,      // 电流+mol
        PassiveVoltageCard,     // 电压+电流卡
        PassivePowerCard,       // 功率+能量卡
        PassiveAreaContainer,   // 面积容器翻倍
        PassiveVolumeContainer, // 体积容器翻倍
        PassiveCapacitor,       // 电容复制卡
        PassiveCharge,          // 电荷+2电流
        PassivePotential,       // 电势触发
        PassiveMoJing,          // 《墨经》+光照
        PassiveMagnetic,        // 磁感应强度方向奖惩

        // 落点效果
        LandTerritory,          // 踩到领地
        LandShop,               // 踩到商店
        LandSupply,             // 踩到补给
        LandReward,             // 踩到奖励格
        LandEvent,              // 踩到事件格

        // 商店
        ShopPurchase,           // 商店购买

        // 建筑
        BuildingToll,           // 建筑过路费
        BuildingPlace,          // 放置建筑

        // 事件牌
        EventTriggered,         // 事件牌触发
        EventDiceRoll,          // 事件中的掷骰
        EventEffect,            // 事件具体效果

        // 奖励牌
        RewardTriggered,        // 奖励牌触发
        RewardEffect,           // 奖励具体效果

        // 卡牌使用
        CardUsedActive,         // 使用主动卡
        CardUsedChoice,         // 使用抉择卡
        CardSynthesis,          // 合成卡牌
        CardConversion,         // 同量纲转换

        // 资源变化
        MolChange,              // mol变化
        CardGained,             // 获得卡牌
        CardLost,               // 失去卡牌

        // 创举
        AchievementDiceCheck,   // 创举掷骰检查
        AchievementCompleted,   // 创举达成
        AchievementReward,      // 创举奖励发放

        // 纪元
        EraAdvance,             // 纪元推进
        BoardExpand,            // 棋盘展开

        // 胜利
        VictoryWealth,          // 财富胜利
        VictoryAchievement,     // 创举胜利

        // 时间机器
        TimeMachineRollback,    // 时间机器回滚

        // 创新项目
        InnovationProject,      // 消耗3张同名牌

        // 其他
        PhaseTransition,        // 相变选择
        DirectionChange,        // 方向改变
        BuffApplied,            // buff/debuff施加
        BuffExpired,            // buff/debuff过期
    }

    /// <summary>
    /// 单个回合的完整记录
    /// </summary>
    [Serializable]
    public class TurnRecord
    {
        public int turnNumber;              // 第几个回合（全局递增）
        public int roundNumber;             // 第几轮
        public int playerIndex;             // 当前玩家索引
        public string playerName;           // 当前玩家名字
        public Era era;                     // 当前纪元

        // 回合开始时的快照数据
        public int startPosition;           // 起始位置
        public int startMol;                // 起始mol
        public int startHandCount;          // 起始手牌数
        public int startAchievementPoints;  // 起始创举分

        // 回合结束时的数据
        public int endPosition;             // 结束位置
        public int endMol;                  // 结束mol
        public int endHandCount;            // 结束手牌数
        public int endAchievementPoints;    // 结束创举分

        // 骰子数据
        public int rawDiceRoll;             // 原始骰子点数
        public int modifiedDiceRoll;        // 修正后点数
        public int actualSteps;             // 实际移动步数

        // 行为日志
        public List<TurnAction> actions;    // 按时间顺序的行为列表

        // 结果标记
        public bool wasSkipped;             // 是否被跳过
        public bool wasRollback;            // 是否被时间机器回滚
        public bool triggeredVictory;       // 是否触发了胜利

        public TurnRecord(int turnNum, int roundNum, int playerIdx, string name, Era era)
        {
            this.turnNumber = turnNum;
            this.roundNumber = roundNum;
            this.playerIndex = playerIdx;
            this.playerName = name;
            this.era = era;
            this.actions = new List<TurnAction>();
        }

        /// <summary>添加一条行为记录</summary>
        public void Log(TurnActionType type, string desc, int source = -1,
            int target = -1, int value = 0, string extra = "")
        {
            actions.Add(new TurnAction(type, desc, source, target, value, extra));
        }

        /// <summary>生成可读的回合摘要</summary>
        public string ToSummary()
        {
            var lines = new List<string>();
            lines.Add($"=== 回合{turnNumber} | 轮{roundNumber} | {playerName} | {era} ===");
            lines.Add($"位置: {startPosition}→{endPosition} | mol: {startMol}→{endMol} | 手牌: {startHandCount}→{endHandCount} | 创举: {startAchievementPoints}→{endAchievementPoints}");

            if (wasSkipped)
                lines.Add("[跳过回合]");
            else if (wasRollback)
                lines.Add("[时间机器回滚]");
            else
                lines.Add($"骰子: {rawDiceRoll}→{modifiedDiceRoll} | 步数: {actualSteps}");

            foreach (var action in actions)
            {
                lines.Add($"  [{action.type}] {action.description}");
            }

            if (triggeredVictory)
                lines.Add("★ 触发胜利！");

            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// 游戏全局记录管理器
    /// </summary>
    [Serializable]
    public class GameRecorder
    {
        public List<TurnRecord> allTurns;       // 所有回合记录
        public TurnRecord currentTurn;          // 当前正在记录的回合

        public GameRecorder()
        {
            allTurns = new List<TurnRecord>();
        }

        /// <summary>开始记录新回合</summary>
        public TurnRecord BeginTurn(int turnNum, int roundNum, int playerIdx,
            string playerName, Era era, int position, int mol, int handCount, int achPts)
        {
            currentTurn = new TurnRecord(turnNum, roundNum, playerIdx, playerName, era);
            currentTurn.startPosition = position;
            currentTurn.startMol = mol;
            currentTurn.startHandCount = handCount;
            currentTurn.startAchievementPoints = achPts;
            return currentTurn;
        }

        /// <summary>结束当前回合记录</summary>
        public void EndTurn(int position, int mol, int handCount, int achPts)
        {
            if (currentTurn == null) return;
            currentTurn.endPosition = position;
            currentTurn.endMol = mol;
            currentTurn.endHandCount = handCount;
            currentTurn.endAchievementPoints = achPts;
            allTurns.Add(currentTurn);
            currentTurn = null;
        }

        /// <summary>快捷记录到当前回合</summary>
        public void Log(TurnActionType type, string desc, int source = -1,
            int target = -1, int value = 0, string extra = "")
        {
            currentTurn?.Log(type, desc, source, target, value, extra);
        }

        /// <summary>获取指定玩家的所有回合记录</summary>
        public List<TurnRecord> GetPlayerTurns(int playerIndex)
        {
            return allTurns.Where(t => t.playerIndex == playerIndex).ToList();
        }

        /// <summary>获取指定轮次的所有回合记录</summary>
        public List<TurnRecord> GetRoundTurns(int roundNumber)
        {
            return allTurns.Where(t => t.roundNumber == roundNumber).ToList();
        }

        /// <summary>获取最近N个回合</summary>
        public List<TurnRecord> GetRecentTurns(int count)
        {
            return allTurns.Skip(Math.Max(0, allTurns.Count - count)).ToList();
        }

        /// <summary>导出全部记录为可读文本</summary>
        public string ExportFullLog()
        {
            return string.Join("\n\n", allTurns.Select(t => t.ToSummary()));
        }

        /// <summary>统计某玩家的mol变化曲线</summary>
        public List<(int turn, int mol)> GetMolHistory(int playerIndex)
        {
            return allTurns
                .Where(t => t.playerIndex == playerIndex)
                .Select(t => (t.turnNumber, t.endMol))
                .ToList();
        }

        /// <summary>统计某玩家的步数历史</summary>
        public List<(int turn, int steps)> GetStepsHistory(int playerIndex)
        {
            return allTurns
                .Where(t => t.playerIndex == playerIndex && !t.wasSkipped)
                .Select(t => (t.turnNumber, t.actualSteps))
                .ToList();
        }
    }
}
