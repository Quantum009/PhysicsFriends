// ============================================================
// EventEffectProcessor.cs — 事件牌效果处理器：执行26种事件牌效果
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 事件效果处理器：负责执行每张事件牌的具体效果
    /// 需要与GameManager交互以访问全局状态
    /// </summary>
    public class EventEffectProcessor
    {
        private DiceSystem _diceSystem;         // 骰子系统引用
        private DeckManager _deckManager;       // 牌堆管理器引用
        private Action<GameSnapshot> _rollbackAction; // 时间机器回滚回调

        public EventEffectProcessor(DiceSystem dice, DeckManager deck)
        {
            _diceSystem = dice;
            _deckManager = deck;
        }

        /// <summary>设置时间机器回滚回调</summary>
        public void SetRollbackAction(Action<GameSnapshot> action)
        {
            _rollbackAction = action;
        }

        /// <summary>
        /// 处理事件牌效果
        /// </summary>
        /// <param name="eventId">事件牌ID</param>
        /// <param name="player">触发事件的玩家</param>
        /// <param name="allPlayers">所有玩家</param>
        /// <param name="snapshot">当前回合的快照（时间机器用）</param>
        /// <returns>是否需要回滚（时间机器事件）</returns>
        public bool ProcessEvent(EventCardId eventId, PlayerState player,
            List<PlayerState> allPlayers, GameSnapshot snapshot = null)
        {
            // 检查金皇冠：免于一次不幸事件
            var eventDef = EventCardDatabase.Get(eventId);
            if (eventDef.isNegative && player.hasGoldenCrown)
            {
                player.hasGoldenCrown = false;   // 消耗金皇冠
                Debug.Log($"[事件] {player.playerName} 使用金皇冠免于不幸事件：{eventDef.nameZH}");
                return false;
            }

            Debug.Log($"[事件] {player.playerName} 触发事件：{eventDef.nameZH}");

            switch (eventId)
            {
                case EventCardId.Singularity:       // #1 奇点
                    return ProcessSingularity(player);

                case EventCardId.HalfLife:           // #2 半衰期
                    ProcessHalfLife(allPlayers);
                    return false;

                case EventCardId.Annihilation:       // #3 湮灭
                    ProcessAnnihilation(player);
                    return false;

                case EventCardId.GrandUnification:   // #4 大一统理论
                    ProcessGrandUnification(player);
                    return false;

                case EventCardId.AbsoluteZero:       // #5 绝对零度
                    player.absoluteZeroTurns = 3;    // 跳过3回合
                    return false;

                case EventCardId.Superconductor:     // #6 超导
                    ProcessSuperconductor(allPlayers);
                    player.superconductorTriggered = true; // 创举15：超导现象
                    return false;

                case EventCardId.TimeMachine:        // #7 时间机器
                    if (snapshot != null && _rollbackAction != null)
                    {
                        _rollbackAction(snapshot);   // 触发回滚
                        return true;                 // 标记需要回滚
                    }
                    return false;

                case EventCardId.EMShield:           // #8 电磁屏蔽
                    player.emShieldTurns = 2;        // 本回合和下回合
                    return false;

                case EventCardId.QuantumTunneling:   // #9 量子隧穿
                    // 传送到任意位置（由UI处理选择位置）
                    // 不算经过起点，不触发地块效果
                    player.quantumTunnelingUsed = true; // 创举14：量子力学
                    Debug.Log("[事件] 量子隧穿：等待玩家选择传送位置");
                    return false;

                case EventCardId.FeynmanBet:         // #10 费曼的赌注
                    ProcessFeynmanBet(player);
                    return false;

                case EventCardId.MillikanOilDrop:    // #11 密立根油滴实验
                    ProcessMillikanOilDrop(player);
                    return false;

                case EventCardId.MichelsonMorley:    // #12 迈克尔逊-莫雷实验
                    player.michelsonMorleyTurns = 3; // 3回合步数固定为6
                    return false;

                case EventCardId.NewtonApple:        // #13 牛顿的苹果
                    if (player.character == Character.Newton)
                    {
                        player.GiveCard(PhysicsCardId.Mass);         // +1质量
                        player.GiveCard(PhysicsCardId.Acceleration); // +1加速度
                    }
                    return false;

                case EventCardId.EntropyIncrease:    // #14 熵增
                    // 再抽两张事件牌（递归处理）
                    for (int i = 0; i < 2; i++)
                    {
                        var nextEvent = _deckManager.DrawEvent();
                        ProcessEvent(nextEvent, player, allPlayers, snapshot);
                    }
                    return false;

                case EventCardId.EnergyConservation: // #15 能量守恒
                    ProcessEnergyConservation(allPlayers);
                    return false;

                case EventCardId.SchrodingerCat:     // #16 薛定谔的猫
                    ProcessSchrodingerCat(player);
                    return false;

                case EventCardId.EinsteinMiracleYear: // #17 爱因斯坦奇迹年
                    if (player.character == Character.Einstein)
                    {
                        // 随机获得两张非基本物理量
                        for (int i = 0; i < 2; i++)
                        {
                            var cardId = _deckManager.DrawRandomNonBasic();
                            player.GiveCard(cardId);
                        }
                    }
                    return false;

                case EventCardId.Collapse:           // #18 坍缩
                    ProcessCollapse(player);
                    return false;

                case EventCardId.BlackHole:          // #19 黑洞
                    ProcessBlackHole(player);
                    return false;

                case EventCardId.PhaseTransition:    // #20 相变
                    // 由UI处理选择（固态/液态/气态）
                    Debug.Log("[事件] 相变：等待玩家选择形态");
                    return false;

                case EventCardId.ResearchFunding:    // #21 基金项目
                    player.mol += 10;                // +10mol
                    return false;

                case EventCardId.AcademicPlagiarism: // #22 学术剽窃
                    // 由UI处理选择（掠夺哪个玩家的哪张牌）
                    Debug.Log("[事件] 学术剽窃：等待玩家选择掠夺目标");
                    return false;

                case EventCardId.EMInduction:        // #23 电磁感应
                    if (player.HasNonCurrentElectrical() && player.HasMagnetic())
                    {
                        player.mol += 20;            // 满足条件+20mol
                    }
                    return false;

                case EventCardId.Wormhole:           // #24 虫洞
                    player.position = 0;             // 传送到起点
                    player.mol = player.mol / 2;     // mol减半
                    // 不算经过起点
                    return false;

                case EventCardId.FranckHertz:        // #25 弗兰克-赫兹实验
                    ProcessFranckHertz(player);
                    return false;

                case EventCardId.NuclearReactor:     // #26 核反应堆
                    ProcessNuclearReactor(player);
                    return false;

                default:
                    Debug.LogWarning($"[事件] 未实现的事件：{eventId}");
                    return false;
            }
        }

        // === 各事件的具体实现 ===

        /// <summary>#1 奇点：掷骰<5失去所有手牌</summary>
        private bool ProcessSingularity(PlayerState player)
        {
            int roll = _diceSystem.RollUnmodified(); // 掷骰（不受修正）
            Debug.Log($"[事件] 奇点掷骰：{roll}");
            if (roll < 5)
            {
                Debug.Log($"[事件] 奇点生效！{player.playerName}失去所有手牌");
                player.handCards.Clear();             // 清空所有手牌
                // 标记"在奇点事件中失去一切"（创举11：宇宙大爆炸）
                player.singularityLostAll = true;
                return false;
            }
            return false;
        }

        /// <summary>#2 半衰期：所有玩家mol减半（四舍五入）</summary>
        private void ProcessHalfLife(List<PlayerState> allPlayers)
        {
            foreach (var p in allPlayers)
            {
                p.mol = (int)Math.Round(p.mol / 2.0); // 四舍五入
                Debug.Log($"[事件] 半衰期：{p.playerName}的mol变为{p.mol}");
            }
        }

        /// <summary>#3 湮灭：掷骰<5失去所有物质的量牌</summary>
        private void ProcessAnnihilation(PlayerState player)
        {
            int roll = _diceSystem.RollUnmodified();
            if (roll < 5)
            {
                player.mol = 0;                       // 失去所有mol
                Debug.Log($"[事件] 湮灭生效！{player.playerName}的mol归零");
            }
        }

        /// <summary>#4 大一统理论：获得所有基本物理量各一张</summary>
        private void ProcessGrandUnification(PlayerState player)
        {
            player.GiveCard(PhysicsCardId.Time);              // +1时间
            player.GiveCard(PhysicsCardId.Length);             // +1长度
            player.GiveCard(PhysicsCardId.Mass);               // +1质量
            player.GiveCard(PhysicsCardId.Current);            // +1电流
            player.GiveCard(PhysicsCardId.Temperature);        // +1温度
            player.GiveCard(PhysicsCardId.LuminousIntensity);  // +1光照强度
        }

        /// <summary>#6 超导：摧毁全场电阻、电阻率和路障</summary>
        private void ProcessSuperconductor(List<PlayerState> allPlayers)
        {
            foreach (var p in allPlayers)
            {
                // 移除所有电阻和电阻率卡牌
                p.handCards.RemoveAll(c =>
                    c.cardId == PhysicsCardId.Resistance ||
                    c.cardId == PhysicsCardId.Resistivity);
            }
            // 路障的移除需要通过BoardManager处理
            Debug.Log("[事件] 超导：所有电阻、电阻率和路障被摧毁");
        }

        /// <summary>#10 费曼的赌注：猜奇偶</summary>
        private void ProcessFeynmanBet(PlayerState player)
        {
            // TODO: 由UI获取玩家的猜测
            // 简化：这里假设猜偶数
            bool guessOdd = false;
            int roll = _diceSystem.RollUnmodified();
            bool isOdd = roll % 2 != 0;

            if (guessOdd == isOdd)
            {
                player.mol += 10;                    // 猜对+10mol
                Debug.Log($"[事件] 费曼赌注：猜对！+10mol");
            }
            else
            {
                player.mol = Math.Max(0, player.mol - 10); // 猜错-10mol（不低于0）
                Debug.Log($"[事件] 费曼赌注：猜错！-10mol");
            }
        }

        /// <summary>#11 密立根油滴实验：获得质量数量的电流（上限3）</summary>
        private void ProcessMillikanOilDrop(PlayerState player)
        {
            int massCount = player.CountCards(PhysicsCardId.Mass); // 统计质量牌数
            int gain = Math.Min(massCount, 3);                     // 上限3张
            for (int i = 0; i < gain; i++)
            {
                player.GiveCard(PhysicsCardId.Current);            // 获得电流
            }
            Debug.Log($"[事件] 密立根油滴：获得{gain}张电流");
        }

        /// <summary>#15 能量守恒：所有玩家mol汇总平均分配</summary>
        private void ProcessEnergyConservation(List<PlayerState> allPlayers)
        {
            int total = allPlayers.Sum(p => p.mol);    // 汇总所有mol
            int average = total / allPlayers.Count;     // 整数除法（余数舍弃）
            foreach (var p in allPlayers)
            {
                p.mol = average;                        // 平均分配
            }
            Debug.Log($"[事件] 能量守恒：总计{total}mol，每人分得{average}mol");
        }

        /// <summary>#16 薛定谔的猫：掷骰决定丢弃类型</summary>
        private void ProcessSchrodingerCat(PlayerState player)
        {
            int roll = _diceSystem.RollUnmodified();
            if (roll <= 3)
            {
                // 丢弃一张非基本物理量牌（由UI选择哪一张）
                var nonBasic = player.handCards.FirstOrDefault(
                    c => !CardDatabase.IsBasicQuantity(c.cardId) && !c.isUsed);
                if (nonBasic != null)
                {
                    player.RemoveCard(nonBasic);
                    Debug.Log($"[事件] 薛定谔的猫({roll})：丢弃非基本物理量");
                }
            }
            else
            {
                // 丢弃1mol
                player.mol = Math.Max(0, player.mol - 1);
                Debug.Log($"[事件] 薛定谔的猫({roll})：丢弃1mol");
            }
        }

        /// <summary>#18 坍缩：掷骰<5失去所有基本物理量牌</summary>
        private void ProcessCollapse(PlayerState player)
        {
            int roll = _diceSystem.RollUnmodified();
            if (roll < 5)
            {
                player.handCards.RemoveAll(c => CardDatabase.IsBasicQuantity(c.cardId));
                Debug.Log("[事件] 坍缩生效！失去所有基本物理量牌");
            }
        }

        /// <summary>#19 黑洞：掷骰<5失去所有非基本物理量牌</summary>
        private void ProcessBlackHole(PlayerState player)
        {
            int roll = _diceSystem.RollUnmodified();
            if (roll < 5)
            {
                player.handCards.RemoveAll(c => !CardDatabase.IsBasicQuantity(c.cardId));
                Debug.Log("[事件] 黑洞生效！失去所有非基本物理量牌");
            }
        }

        /// <summary>
        /// #20 相变处理（在玩家选择后调用）
        /// </summary>
        public void ApplyPhaseTransition(PlayerState player, PhaseState choice)
        {
            player.phaseState = choice;
            switch (choice)
            {
                case PhaseState.Solid:                  // 固态
                    player.phaseSkipNext = true;        // 跳过下一回合
                    player.mol += 8;                    // +8mol
                    Debug.Log("[事件] 相变-固态：跳过下回合，+8mol");
                    break;

                case PhaseState.Liquid:                 // 液态
                    // 调转方向
                    player.moveDirection = player.moveDirection == MoveDirection.Clockwise
                        ? MoveDirection.CounterClockwise
                        : MoveDirection.Clockwise;
                    player.phaseState = PhaseState.None; // 液态是一次性效果
                    Debug.Log("[事件] 相变-液态：调转方向");
                    break;

                case PhaseState.Gas:                    // 气态
                    // 始终轻盈+不触发地块效果，直到下次经过起点
                    Debug.Log("[事件] 相变-气态：轻盈+不触发效果，直到过起点");
                    break;
            }
        }

        /// <summary>#25 弗兰克-赫兹实验：连投3次骰子</summary>
        private void ProcessFranckHertz(PlayerState player)
        {
            int[] rolls = new int[3];
            for (int i = 0; i < 3; i++)
            {
                rolls[i] = _diceSystem.RollUnmodified(); // 不受修正
            }
            Debug.Log($"[事件] 弗兰克-赫兹：{rolls[0]},{rolls[1]},{rolls[2]}");

            // 检查是否严格递增或严格递减
            bool increasing = rolls[0] < rolls[1] && rolls[1] < rolls[2];
            bool decreasing = rolls[0] > rolls[1] && rolls[1] > rolls[2];

            if (increasing || decreasing)
            {
                player.mol += 20;                    // 成功+20mol
                Debug.Log("[事件] 弗兰克-赫兹：成功！+20mol");
            }
            else
            {
                player.mol = Math.Max(0, player.mol - 5); // 失败-5mol
                Debug.Log("[事件] 弗兰克-赫兹：失败！-5mol");
            }
        }

        /// <summary>#26 核反应堆：翻倍赌博机制</summary>
        private void ProcessNuclearReactor(PlayerState player)
        {
            int reward = 1;                          // 初始奖励1mol
            Debug.Log("[事件] 核反应堆：初始奖励1mol");

            // 玩家可以持续掷骰（这里简化为自动掷骰直到停止或失败）
            // TODO: 由UI控制是否继续
            bool continueRolling = true;

            while (continueRolling)
            {
                int roll = _diceSystem.RollUnmodified();
                if (roll >= 1 && roll <= 4)
                {
                    reward *= 2;                     // 1~4：奖励翻倍
                    Debug.Log($"[事件] 核反应堆掷骰{roll}：奖励翻倍至{reward}mol");
                    // TODO: 询问UI是否继续
                    continueRolling = false;         // 简化：只掷一次
                }
                else
                {
                    // 5~6：失去奖励和所有mol
                    reward = 0;
                    player.mol = 0;
                    Debug.Log($"[事件] 核反应堆掷骰{roll}：失败！失去所有mol");
                    continueRolling = false;
                }
            }

            player.mol += reward;                    // 结算奖励
        }
    }
}
