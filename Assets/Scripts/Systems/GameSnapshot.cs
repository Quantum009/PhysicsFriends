// ============================================================
// GameSnapshot.cs — 游戏快照系统：用于"时间机器"事件的状态回滚
// 每个回合开始前深拷贝完整游戏状态
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using PhysicsFriends.Core;
using PhysicsFriends.Player;
using PhysicsFriends.Board;
using PhysicsFriends.Data;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 完整的游戏状态快照
    /// 在每个玩家回合开始前创建，用于时间机器回滚和存档系统
    /// </summary>
    [Serializable]
    public class GameSnapshot
    {
        // 所有玩家的状态快照
        public List<PlayerState> playerSnapshots;
        // 棋盘状态快照
        public BoardManager boardSnapshot;
        // 当前时代
        public Era currentEra;
        // 当前轮次
        public int currentRound;
        // 当前玩家索引
        public int currentPlayerIndex;
        // 活跃的创举列表
        public List<AchievementId> activeAchievements;
        // 已完成的创举集合
        public HashSet<AchievementId> globalCompletedAchievements;
        // 事件牌弃牌堆
        public List<EventCardId> eventDiscardPile;
        // 奖励牌弃牌堆
        public List<RewardCardId> rewardDiscardPile;
        // 物理量弃牌堆
        public List<PhysicsCardId> physicsDiscardPile;

        // === 存档元数据 ===
        public int turnNumber;              // 第几回合
        public string playerName;           // 当前回合的玩家名
        public System.DateTime timestamp;   // 存档时间戳

        /// <summary>
        /// 从当前游戏状态创建快照（深拷贝所有数据）
        /// </summary>
        public static GameSnapshot Create(
            List<PlayerState> players,
            BoardManager board,
            Era era,
            int round,
            int currentPlayer,
            List<AchievementId> activeAch,
            HashSet<AchievementId> completedAch,
            List<EventCardId> eventDiscard,
            List<RewardCardId> rewardDiscard,
            List<PhysicsCardId> physicsDiscard,
            int playerCount)
        {
            var snapshot = new GameSnapshot();

            // 深拷贝每个玩家的状态
            snapshot.playerSnapshots = players.Select(p => p.DeepCopy()).ToList();

            // 深拷贝棋盘
            snapshot.boardSnapshot = board.DeepCopy(playerCount);

            // 复制时代和轮次
            snapshot.currentEra = era;
            snapshot.currentRound = round;
            snapshot.currentPlayerIndex = currentPlayer;

            // 复制创举状态
            snapshot.activeAchievements = new List<AchievementId>(activeAch);
            snapshot.globalCompletedAchievements = new HashSet<AchievementId>(completedAch);

            // 复制弃牌堆
            snapshot.eventDiscardPile = new List<EventCardId>(eventDiscard);
            snapshot.rewardDiscardPile = new List<RewardCardId>(rewardDiscard);
            snapshot.physicsDiscardPile = new List<PhysicsCardId>(physicsDiscard);

            // 存档元数据
            snapshot.turnNumber = 0;
            snapshot.playerName = players.Count > 0 && currentPlayer < players.Count
                ? players[currentPlayer].playerName : "";
            snapshot.timestamp = System.DateTime.Now;

            return snapshot;
        }

        /// <summary>简化版Create（仅保存核心状态）</summary>
        public static GameSnapshot Create(
            List<PlayerState> players, BoardManager board, Era era)
        {
            var snapshot = new GameSnapshot();
            snapshot.playerSnapshots = players.Select(p => p.DeepCopy()).ToList();
            snapshot.boardSnapshot = board.DeepCopy(players.Count);
            snapshot.currentEra = era;
            snapshot.activeAchievements = new List<AchievementId>();
            snapshot.globalCompletedAchievements = new HashSet<AchievementId>();
            snapshot.eventDiscardPile = new List<EventCardId>();
            snapshot.rewardDiscardPile = new List<RewardCardId>();
            snapshot.physicsDiscardPile = new List<PhysicsCardId>();
            return snapshot;
        }

        /// <summary>获取纪元（兼容别名）</summary>
        public Era era => currentEra;
    }

    /// <summary>快照恢复扩展</summary>
    public static class SnapshotRestoreExtensions
    {
        /// <summary>从快照恢复玩家状态（与DeepCopy完全对齐）</summary>
        public static void RestoreTo(this PlayerState source, PlayerState target)
        {
            // === 资源 ===
            target.mol = source.mol;
            target.handCards = source.handCards.Select(c => c.DeepCopy()).ToList();
            target.rewardItems = new List<RewardCardId>(source.rewardItems);

            // === 位置与移动 ===
            target.position = source.position;
            target.moveDirection = source.moveDirection;
            target.lastMoveSteps = source.lastMoveSteps;
            target.recentMoveSteps = new List<int>(source.recentMoveSteps);

            // === 跳过回合 ===
            target.stunTurns = source.stunTurns;
            target.absoluteZeroTurns = source.absoluteZeroTurns;
            target.roadblockSkipTurns = source.roadblockSkipTurns;
            target.phaseSkipNext = source.phaseSkipNext;

            // === 电势特殊行动 ===
            target.potentialCharging = source.potentialCharging;
            target.potentialSkipTurns = source.potentialSkipTurns;
            target.potentialExtraTurns = source.potentialExtraTurns;

            // === 其他回合效果 ===
            target.michelsonMorleyTurns = source.michelsonMorleyTurns;
            target.heavyLayers = source.heavyLayers;
            target.lightLayers = source.lightLayers;
            target.densityHeavyTurns = source.densityHeavyTurns;
            target.densityLightTurns = source.densityLightTurns;
            target.phaseState = source.phaseState;
            target.emShieldTurns = source.emShieldTurns;

            // === 创举进度 ===
            target.achievementPoints = source.achievementPoints;
            target.completedAchievements = new HashSet<AchievementId>(source.completedAchievements);
            target.newtonUniformTurns = source.newtonUniformTurns;
            target.newtonHasAllCards = source.newtonHasAllCards;
            target.newtonUsedForce = source.newtonUsedForce;
            target.relativityFastTurns = source.relativityFastTurns;
            target.hasCompletedDisplacementVector = source.hasCompletedDisplacementVector;
            target.hasCompletedDisplacementFlux = source.hasCompletedDisplacementFlux;

            // === 人物任务 ===
            target.characterTaskCompleted = source.characterTaskCompleted;
            target.entropyUseCount = source.entropyUseCount;

            // === 创举奖励道具 ===
            target.hasGoldenCrown = source.hasGoldenCrown;
            target.hasMengXiBiTan = source.hasMengXiBiTan;
            target.hasMoJing = source.hasMoJing;
            target.hasPrincipia = source.hasPrincipia;
            target.hasHeatEngine = source.hasHeatEngine;

            // === 电磁铁 ===
            target.electromagnetTurns = source.electromagnetTurns;
            target.electromagnetPosition = source.electromagnetPosition;

            // === 力使用记录 ===
            target.forceUsedThisRound = source.forceUsedThisRound;

            // === 路障/阻挡状态 ===
            target.stoppedByRoadblock = source.stoppedByRoadblock;

            // === 量子隧穿/超导 ===
            target.quantumTunnelingUsed = source.quantumTunnelingUsed;
            target.superconductorTriggered = source.superconductorTriggered;

            // === 牛顿力学子任务 ===
            target.newtonTask_Inertia = source.newtonTask_Inertia;
            target.newtonTask_Acceleration = source.newtonTask_Acceleration;
            target.newtonTask_Reaction = source.newtonTask_Reaction;

            // === 浮力/位移电流/奇点 创举追踪 ===
            target.buoyancySynthesisCompleted = source.buoyancySynthesisCompleted;
            target.displacementCurrentCompleted = source.displacementCurrentCompleted;
            target.singularityLostAll = source.singularityLostAll;

            // === 创举掷骰检查 ===
            target.achievementDiceCheckPassed = new HashSet<AchievementId>(source.achievementDiceCheckPassed);

            // === 建筑 ===
            target.buildings = source.buildings.Select(b => b.DeepCopy()).ToList();
        }

        /// <summary>从快照恢复棋盘状态</summary>
        public static void RestoreTo(this BoardManager source, BoardManager target)
        {
            target.tiles = source.tiles.Select(t => t.DeepCopy()).ToList();
            target.isExpanded = source.isExpanded;
            target.currentRoadblockCount = source.currentRoadblockCount;
        }
    }

    /// <summary>
    /// 游戏存档管理器：每回合自动存档，支持回退到任意回合
    /// </summary>
    public class GameSaveManager
    {
        private List<GameSnapshot> _saveHistory = new List<GameSnapshot>();
        private int _maxSaves;

        /// <summary>创建存档管理器</summary>
        /// <param name="maxSaves">最大存档数量（0=不限制）</param>
        public GameSaveManager(int maxSaves = 0)
        {
            _maxSaves = maxSaves;
        }

        /// <summary>存档数量</summary>
        public int Count => _saveHistory.Count;

        /// <summary>所有存档的只读列表</summary>
        public IReadOnlyList<GameSnapshot> Saves => _saveHistory;

        /// <summary>
        /// 保存当前回合的快照
        /// </summary>
        public void SaveTurn(
            int turnNumber,
            List<PlayerState> players,
            BoardManager board,
            Era era,
            int round,
            int currentPlayer,
            List<AchievementId> activeAch,
            HashSet<AchievementId> completedAch,
            List<EventCardId> eventDiscard,
            List<RewardCardId> rewardDiscard,
            List<PhysicsCardId> physicsDiscard,
            int playerCount)
        {
            var snapshot = GameSnapshot.Create(
                players, board, era, round, currentPlayer,
                activeAch, completedAch,
                eventDiscard, rewardDiscard, physicsDiscard,
                playerCount);

            snapshot.turnNumber = turnNumber;
            snapshot.playerName = currentPlayer < players.Count
                ? players[currentPlayer].playerName : "";
            snapshot.timestamp = System.DateTime.Now;

            _saveHistory.Add(snapshot);

            // 如果超过最大存档数，移除最旧的
            if (_maxSaves > 0 && _saveHistory.Count > _maxSaves)
                _saveHistory.RemoveAt(0);
        }

        /// <summary>简化版存档（仅核心状态）</summary>
        public void SaveTurn(int turnNumber, List<PlayerState> players,
            BoardManager board, Era era, int currentPlayer)
        {
            var snapshot = GameSnapshot.Create(players, board, era);
            snapshot.turnNumber = turnNumber;
            snapshot.currentPlayerIndex = currentPlayer;
            snapshot.playerName = currentPlayer < players.Count
                ? players[currentPlayer].playerName : "";
            snapshot.timestamp = System.DateTime.Now;
            _saveHistory.Add(snapshot);

            if (_maxSaves > 0 && _saveHistory.Count > _maxSaves)
                _saveHistory.RemoveAt(0);
        }

        /// <summary>获取指定回合的存档</summary>
        public GameSnapshot GetSave(int turnNumber)
        {
            return _saveHistory.FirstOrDefault(s => s.turnNumber == turnNumber);
        }

        /// <summary>获取最新的存档</summary>
        public GameSnapshot GetLatestSave()
        {
            return _saveHistory.Count > 0 ? _saveHistory[_saveHistory.Count - 1] : null;
        }

        /// <summary>获取上一回合的存档（时间机器用）</summary>
        public GameSnapshot GetPreviousSave()
        {
            return _saveHistory.Count >= 2 ? _saveHistory[_saveHistory.Count - 2] : null;
        }

        /// <summary>回退到指定回合（删除之后的所有存档）</summary>
        public GameSnapshot RollbackToTurn(int turnNumber)
        {
            var save = GetSave(turnNumber);
            if (save == null) return null;

            int idx = _saveHistory.IndexOf(save);
            // 删除该存档之后的所有记录
            if (idx + 1 < _saveHistory.Count)
                _saveHistory.RemoveRange(idx + 1, _saveHistory.Count - idx - 1);

            return save;
        }

        /// <summary>回退到最近的存档（删除当前最新）</summary>
        public GameSnapshot RollbackOne()
        {
            if (_saveHistory.Count < 2) return null;
            _saveHistory.RemoveAt(_saveHistory.Count - 1);
            return _saveHistory[_saveHistory.Count - 1];
        }

        /// <summary>清空所有存档</summary>
        public void Clear()
        {
            _saveHistory.Clear();
        }

        /// <summary>获取存档摘要列表（用于UI显示）</summary>
        public List<string> GetSaveSummaries()
        {
            return _saveHistory.Select(s =>
                $"回合{s.turnNumber} [{s.currentEra}] {s.playerName} " +
                $"({s.timestamp:HH:mm:ss})").ToList();
        }
    }
}
