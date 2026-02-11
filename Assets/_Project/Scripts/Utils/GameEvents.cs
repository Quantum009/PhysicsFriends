// ============================================================
// GameEvents.cs — 全局事件总线：各系统通过事件解耦通信
// 所有事件由 Host 端触发，客户端通过 NetworkVariable/ClientRpc 间接驱动
// ============================================================
using System;
using System.Collections.Generic;
using PhysicsFriends.Cards;
using PhysicsFriends.Data;

namespace PhysicsFriends.Core
{
    public static class GameEvents
    {
        // ---- 回合流程 ----
        public static event Action<int> OnTurnStarted;            // playerIndex
        public static event Action<int> OnTurnEnded;              // playerIndex
        public static event Action<TurnPhase> OnPhaseChanged;     // 回合阶段变化

        // ---- 骰子 ----
        public static event Action<int, int> OnDiceRolled;        // playerIndex, rawValue
        public static event Action<int, int> OnDiceFinalResolved; // playerIndex, finalValue

        // ---- 移动 ----
        public static event Action<int, int, int> OnPlayerMoved;  // playerIndex, fromTile, toTile
        public static event Action<int> OnPlayerPassedStart;      // playerIndex

        // ---- 卡牌 ----
        public static event Action<int, CardInstance> OnCardGained;   // playerIndex, card
        public static event Action<int, CardInstance> OnCardLost;     // playerIndex, card
        public static event Action<int, CardInstance> OnCardUsed;     // playerIndex, card
        public static event Action<int, PhysicsCardId> OnSynthesisCompleted; // playerIndex, outputId

        // ---- 经济 ----
        public static event Action<int, int, int> OnMolChanged;   // playerIndex, oldVal, newVal

        // ---- 事件/奖励 ----
        public static event Action<int, EventCardId> OnEventTriggered;   // playerIndex, eventId
        public static event Action<int, RewardCardId> OnRewardTriggered; // playerIndex, rewardId

        // ---- 创举/时代 ----
        public static event Action<int, AchievementId> OnAchievementCompleted; // playerIndex, achId
        public static event Action<Era> OnEraChanged;
        public static event Action OnBoardExpanded;

        // ---- 胜利 ----
        public static event Action<int, VictoryType> OnVictory;   // playerIndex, victoryType

        // ---- 网络 ----
        public static event Action<int> OnPlayerConnected;        // playerIndex
        public static event Action<int> OnPlayerDisconnected;     // playerIndex

        // ================================================================
        // 触发方法（Host 端调用）
        // ================================================================

        public static void FireTurnStarted(int idx) => OnTurnStarted?.Invoke(idx);
        public static void FireTurnEnded(int idx) => OnTurnEnded?.Invoke(idx);
        public static void FirePhaseChanged(TurnPhase phase) => OnPhaseChanged?.Invoke(phase);

        public static void FireDiceRolled(int idx, int val) => OnDiceRolled?.Invoke(idx, val);
        public static void FireDiceFinalResolved(int idx, int val) => OnDiceFinalResolved?.Invoke(idx, val);

        public static void FirePlayerMoved(int idx, int from, int to) => OnPlayerMoved?.Invoke(idx, from, to);
        public static void FirePlayerPassedStart(int idx) => OnPlayerPassedStart?.Invoke(idx);

        public static void FireCardGained(int idx, CardInstance c) => OnCardGained?.Invoke(idx, c);
        public static void FireCardLost(int idx, CardInstance c) => OnCardLost?.Invoke(idx, c);
        public static void FireSynthesisCompleted(int idx, PhysicsCardId id) => OnSynthesisCompleted?.Invoke(idx, id);

        public static void FireMolChanged(int idx, int o, int n) => OnMolChanged?.Invoke(idx, o, n);

        public static void FireEventTriggered(int idx, EventCardId e) => OnEventTriggered?.Invoke(idx, e);
        public static void FireRewardTriggered(int idx, RewardCardId r) => OnRewardTriggered?.Invoke(idx, r);

        public static void FireAchievementCompleted(int idx, AchievementId a) => OnAchievementCompleted?.Invoke(idx, a);
        public static void FireEraChanged(Era era) => OnEraChanged?.Invoke(era);
        public static void FireBoardExpanded() => OnBoardExpanded?.Invoke();

        public static void FireVictory(int idx, VictoryType vt) => OnVictory?.Invoke(idx, vt);

        public static void FirePlayerConnected(int idx) => OnPlayerConnected?.Invoke(idx);
        public static void FirePlayerDisconnected(int idx) => OnPlayerDisconnected?.Invoke(idx);

        /// <summary>清除所有监听（场景切换时调用）</summary>
        public static void ClearAll()
        {
            OnTurnStarted = null; OnTurnEnded = null; OnPhaseChanged = null;
            OnDiceRolled = null; OnDiceFinalResolved = null;
            OnPlayerMoved = null; OnPlayerPassedStart = null;
            OnCardGained = null; OnCardLost = null; OnCardUsed = null; OnSynthesisCompleted = null;
            OnMolChanged = null;
            OnEventTriggered = null; OnRewardTriggered = null;
            OnAchievementCompleted = null; OnEraChanged = null; OnBoardExpanded = null;
            OnVictory = null;
            OnPlayerConnected = null; OnPlayerDisconnected = null;
        }
    }
}
