// ============================================================
// DiceSystem.cs — 骰子系统：掷骰、修正链、最终点数计算
// 包含完整的修正链处理（力、加速度、压强、劲度系数等）
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Data;

namespace PhysicsFriends.Systems
{
    /// <summary>骰子修正操作的记录</summary>
    [Serializable]
    public class DiceModification
    {
        public int playerIndex;         // 操作者的玩家索引
        public string source;           // 修正来源（"力"/"加速度"等）
        public int value;               // 修正值（正数=加，负数=减）
        public bool isNullified;        // 是否被压强无效化
        public int cardInstanceId;      // 对应的卡牌实例ID（用于压强定位）

        public DiceModification(int player, string src, int val, int cardId = -1)
        {
            this.playerIndex = player;
            this.source = src;
            this.value = val;
            this.isNullified = false;    // 初始未被无效化
            this.cardInstanceId = cardId;
        }
    }

    /// <summary>
    /// 骰子系统：处理掷骰和修正链
    /// </summary>
    public class DiceSystem
    {
        private System.Random _random;   // 随机数生成器

        public DiceSystem()
        {
            _random = new System.Random(); // 初始化随机数
        }

        /// <summary>投掷骰子，返回1~6的原始点数</summary>
        public int Roll()
        {
            return _random.Next(1, 7);   // 生成1到6的随机整数
        }

        /// <summary>投掷不受任何修正影响的骰子（创举掷骰、随机获取等）</summary>
        public int RollUnmodified()
        {
            return Roll();               // 不受修正的骰子就是普通掷骰
        }

        /// <summary>
        /// 执行完整的骰子修正链
        /// 顺序：掷骰者的下一位 → ... → 掷骰者自己
        /// </summary>
        /// <param name="rawDice">原始点数</param>
        /// <param name="rollerIndex">掷骰者索引</param>
        /// <param name="players">所有玩家</param>
        /// <param name="playerCount">玩家数量</param>
        /// <param name="modifications">输出：修正记录列表</param>
        /// <returns>最终点数</returns>
        public int ProcessModificationChain(
            int rawDice,
            int rollerIndex,
            List<PlayerState> players,
            int playerCount,
            out List<DiceModification> modifications)
        {
            modifications = new List<DiceModification>(); // 初始化修正记录
            int currentValue = rawDice;                   // 当前点数从原始值开始

            // === 步骤4a：其他玩家按序使用"力" ===
            // 顺序是掷骰者的下一位开始，顺序循环直到掷骰者前一位
            for (int i = 1; i < playerCount; i++)
            {
                int modPlayerIdx = (rollerIndex + i) % playerCount; // 当前修正玩家
                var modPlayer = players[modPlayerIdx];

                // 检查该玩家是否有可用的"力"卡（每轮限一次）
                if (!modPlayer.forceUsedThisRound)
                {
                    var forceCards = FindForceCards(modPlayer); // 查找力卡
                    foreach (var forceCard in forceCards)
                    {
                        if (forceCard.isUsed || forceCard.isDisabled) continue; // 跳过已用/禁用的

                        // 检查是否拥有《自然哲学的数学原理》
                        bool hasPrincipia = modPlayer.hasPrincipia;

                        // 让玩家选择修正值（这里用回调/事件机制，简化为AI逻辑）
                        // hasPrincipia: 可选+2/+1/-1/-2
                        // 普通：可选+1/-1
                        int modValue = GetForceModificationChoice(modPlayer, hasPrincipia);

                        if (modValue != 0) // 玩家选择了使用力
                        {
                            var mod = new DiceModification(modPlayerIdx, "力", modValue, forceCard.instanceId);
                            modifications.Add(mod);          // 记录修正
                            currentValue += modValue;        // 应用修正
                            modPlayer.forceUsedThisRound = true; // 标记本轮已使用力
                            break; // 每轮每人只能用一次力
                        }
                    }
                }
            }

            // === 步骤4b：掷骰者同时使用自己的"力"和"加速度" ===
            var roller = players[rollerIndex];

            // 掷骰者使用力
            if (!roller.forceUsedThisRound)
            {
                var forceCards = FindForceCards(roller);
                foreach (var fc in forceCards)
                {
                    if (fc.isUsed || fc.isDisabled) continue;
                    bool hasPrincipia = roller.hasPrincipia;
                    int modVal = GetForceModificationChoice(roller, hasPrincipia);
                    if (modVal != 0)
                    {
                        var mod = new DiceModification(rollerIndex, "力(自己)", modVal, fc.instanceId);
                        modifications.Add(mod);
                        currentValue += modVal;
                        roller.forceUsedThisRound = true;

                        // === 创举7-③：判断是否用力抵消了其他玩家的力 ===
                        // 如果其他玩家用了力改变点数，而掷骰者用力反向修正，算作用反作用
                        bool opponentUsedForce = modifications.Any(m =>
                            m.playerIndex != rollerIndex &&
                            m.source == "力" &&
                            !m.isNullified);
                        if (opponentUsedForce)
                        {
                            roller.newtonTask_Reaction = true;
                            Debug.Log("[骰子] 牛顿力学③达成：用力抵消了其他玩家的力");
                        }

                        break;
                    }
                }
            }

            // 掷骰者使用加速度（只能对自己的骰子使用）
            var accelCards = FindAccelerationCards(roller);
            foreach (var ac in accelCards)
            {
                if (ac.isUsed || ac.isDisabled) continue;
                int accelVal = GetAccelerationChoice(roller); // 选择+1或-1
                if (accelVal != 0)
                {
                    var mod = new DiceModification(rollerIndex, "加速度", accelVal, ac.instanceId);
                    modifications.Add(mod);
                    currentValue += accelVal;
                    // 加速度是被动效果，不消耗，但每轮限一次使用
                    break;
                }
            }

            // === 步骤4c：劲度系数效果 ===
            // 检查是否有人激活了劲度系数（主动效果）
            // 劲度系数：若>4则=4，若<3则=3
            if (IsSpringConstantActive(players, rollerIndex))
            {
                if (currentValue > 4)
                {
                    modifications.Add(new DiceModification(-1, "劲度系数", 4 - currentValue));
                    currentValue = 4;        // 大于4则设为4
                }
                else if (currentValue < 3)
                {
                    modifications.Add(new DiceModification(-1, "劲度系数", 3 - currentValue));
                    currentValue = 3;        // 小于3则设为3
                }
            }

            // === 步骤4d：最终点数不能小于0 ===
            if (currentValue < 0)
            {
                currentValue = 0;            // 下限为0
            }

            return currentValue;             // 返回最终修正后的点数
        }

        /// <summary>处理压强无效化：在修正链中，任何人可以用压强无效一个修正</summary>
        public int ApplyPressureNullification(
            int currentValue,
            List<DiceModification> modifications,
            int targetModIndex,
            int pressureUserIndex,
            List<PlayerState> players)
        {
            if (targetModIndex < 0 || targetModIndex >= modifications.Count)
                return currentValue;         // 索引越界则不操作

            var targetMod = modifications[targetModIndex];
            if (targetMod.isNullified) return currentValue; // 已经被无效化了

            // 检查使用压强的玩家是否有压强卡
            var pressureUser = players[pressureUserIndex];
            var pressureCard = pressureUser.handCards.Find(
                c => c.cardId == PhysicsCardId.Pressure && !c.isUsed && !c.isDisabled);

            if (pressureCard == null) return currentValue; // 没有可用的压强

            // 执行压强无效化
            targetMod.isNullified = true;    // 标记该修正被无效化
            pressureCard.isUsed = true;      // 消耗压强卡（主动效果，用后消耗）
            currentValue -= targetMod.value; // 撤回该修正的效果

            Debug.Log($"[骰子] 玩家{pressureUserIndex}使用压强无效化了修正：{targetMod.source} ({targetMod.value})");

            return currentValue;             // 返回新的当前值
        }

        /// <summary>查找玩家手中所有可用的"力"卡</summary>
        private List<CardInstance> FindForceCards(PlayerState player)
        {
            return player.handCards.FindAll(
                c => c.cardId == PhysicsCardId.Force && !c.isUsed && !c.isDisabled);
        }

        /// <summary>查找玩家手中所有可用的"加速度"卡</summary>
        private List<CardInstance> FindAccelerationCards(PlayerState player)
        {
            return player.handCards.FindAll(
                c => c.cardId == PhysicsCardId.Acceleration && !c.isUsed && !c.isDisabled);
        }

        /// <summary>
        /// 获取力的修正选择
        /// 实际游戏中应由UI让玩家选择，这里提供接口
        /// </summary>
        private int GetForceModificationChoice(PlayerState player, bool hasPrincipia)
        {
            // TODO: 替换为UI交互回调
            // hasPrincipia时可选: +2, +1, -1, -2
            // 普通时可选: +1, -1, 0(不使用)
            return 0; // 默认不使用（等待UI输入）
        }

        /// <summary>获取加速度的修正选择（+1或-1）</summary>
        private int GetAccelerationChoice(PlayerState player)
        {
            // TODO: 替换为UI交互回调
            return 0; // 默认不使用
        }

        /// <summary>检查是否有激活的劲度系数效果</summary>
        private bool IsSpringConstantActive(List<PlayerState> players, int rollerIndex)
        {
            // 劲度系数是主动效果，需要检查是否有人在本次掷骰中激活了它
            foreach (var player in players)
            {
                var springCards = player.handCards.FindAll(
                    c => c.cardId == PhysicsCardId.SpringConstant && !c.isUsed && !c.isDisabled);
                if (springCards.Count > 0)
                    return true; // 简化：检查是否有可用的劲度系数卡
            }
            return false;
        }

        /// <summary>
        /// 计算最终移动步数（考虑沉重/轻盈）
        /// 沉重：步数 = 点数/2（向下取整）
        /// 轻盈：步数 = 点数×2
        /// 正常：步数 = 点数
        /// </summary>
        public int CalculateSteps(int diceValue, PlayerState player)
        {
            // 统计沉重和轻盈层数
            int heavy = CalculateHeavyLayers(player);
            int light = CalculateLightLayers(player);

            // 沉重与轻盈互相抵消
            int netEffect = light - heavy;

            if (netEffect > 0) // 净轻盈
            {
                return diceValue * 2;        // 轻盈：步数翻倍
            }
            else if (netEffect < 0) // 净沉重
            {
                return diceValue / 2;        // 沉重：步数减半（向下取整）
            }
            else // 完全抵消
            {
                return diceValue;            // 正常步数
            }
        }

        /// <summary>计算玩家当前的沉重层数</summary>
        private int CalculateHeavyLayers(PlayerState player)
        {
            int layers = 0;

            // 密度造成的沉重
            if (player.densityHeavyTurns > 0) layers++;

            // 电阻率的被动沉重
            // （电阻率是指定其他玩家沉重，不是自己）

            // 电场强度：逆方向沉重
            // （需要根据当前行进方向判断，简化处理）

            return layers + player.heavyLayers; // 加上其他来源的沉重
        }

        /// <summary>计算玩家当前的轻盈层数</summary>
        private int CalculateLightLayers(PlayerState player)
        {
            int layers = 0;

            // 密度造成的轻盈
            if (player.densityLightTurns > 0) layers++;

            // 相变-气态：始终轻盈
            if (player.phaseState == PhaseState.Gas) layers++;

            // 电场强度：顺方向轻盈
            // （需要根据当前行进方向判断，简化处理）

            return layers + player.lightLayers; // 加上其他来源的轻盈
        }
    }
}
