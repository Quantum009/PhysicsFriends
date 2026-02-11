// ============================================================
// VictoryChecker.cs — 胜利判定系统
// 两种胜利条件：财富胜利（mol达标）/ 创举胜利（点数达标）
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Player;
using PhysicsFriends.Data;

namespace PhysicsFriends.Systems
{
    public class VictoryChecker
    {
        private readonly GameConfig _config;
        private readonly GameMode _mode;

        public bool HasWinner { get; private set; }
        public int WinnerIndex { get; private set; } = -1;
        public VictoryType WinType { get; private set; }

        public VictoryChecker(GameConfig config, GameMode mode)
        {
            _config = config;
            _mode = mode;
        }

        /// <summary>检查指定玩家是否满足胜利条件</summary>
        /// <returns>true 如果该玩家获胜</returns>
        public bool CheckVictory(PlayerState player)
        {
            if (HasWinner) return false; // 已有赢家

            // ---- 财富胜利 ----
            if (player.mol >= _config.wealthVictoryMol)
            {
                HasWinner = true;
                WinnerIndex = player.playerIndex;
                WinType = VictoryType.Wealth;
                Debug.Log($"[Victory] Player {player.playerIndex} wins by WEALTH " +
                          $"({player.mol} >= {_config.wealthVictoryMol})");
                return true;
            }

            // ---- 创举胜利 ----
            if (player.achievementPoints >= _config.achievementVictoryPts)
            {
                HasWinner = true;
                WinnerIndex = player.playerIndex;
                WinType = VictoryType.Achievement;
                Debug.Log($"[Victory] Player {player.playerIndex} wins by ACHIEVEMENT " +
                          $"({player.achievementPoints} >= {_config.achievementVictoryPts})");
                return true;
            }

            return false;
        }

        /// <summary>检查所有玩家</summary>
        public bool CheckAllPlayers(List<PlayerState> players)
        {
            foreach (var p in players)
            {
                if (CheckVictory(p)) return true;
            }
            return false;
        }

        /// <summary>获取所有玩家的排名（用于游戏结束界面）</summary>
        public List<PlayerRanking> GetRankings(List<PlayerState> players)
        {
            return players.Select(p => new PlayerRanking
            {
                playerIndex = p.playerIndex,
                playerName = p.playerName,
                mol = p.mol,
                achievementPoints = p.achievementPoints,
                totalCards = p.handCards.Count,
                // 综合分数：mol + 创举点×10
                score = p.mol + p.achievementPoints * 10
            })
            .OrderByDescending(r => r.score)
            .ToList();
        }

        /// <summary>重置（新局）</summary>
        public void Reset()
        {
            HasWinner = false;
            WinnerIndex = -1;
        }
    }

    public class PlayerRanking
    {
        public int playerIndex;
        public string playerName;
        public int mol;
        public int achievementPoints;
        public int totalCards;
        public int score;
    }
}
