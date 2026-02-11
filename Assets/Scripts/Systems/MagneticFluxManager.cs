// ============================================================
// MagneticFlux.cs — 磁通量"面"机制
// 玩家在两个相邻格子之间放置一个"面"
// 其他玩家穿过时根据方向 +1/-1 计数
// 达到目标（玩家数×2）后奖励 20 mol
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Player;
using PhysicsFriends.UI;

namespace PhysicsFriends.Systems
{
    [Serializable]
    public class MagneticFluxSurface
    {
        public int ownerPlayerIndex;
        public int tileA;              // 面在 TileA 和 TileB 之间
        public int tileB;
        public int designatedDirection; // 1 = A→B 为正, -1 = B→A 为正
        public int currentCount;
        public int targetCount;        // 玩家数 × 2
        public bool isCompleted;

        /// <summary>玩家穿过面时调用</summary>
        /// <returns>true 如果面完成</returns>
        public bool OnPlayerCrosses(int fromTile, int toTile)
        {
            if (isCompleted) return false;

            if (fromTile == tileA && toTile == tileB)
                currentCount += designatedDirection;
            else if (fromTile == tileB && toTile == tileA)
                currentCount -= designatedDirection;
            else
                return false; // 没有穿过

            if (Mathf.Abs(currentCount) >= targetCount)
            {
                isCompleted = true;
                return true;
            }

            return false;
        }
    }

    public class MagneticFluxManager
    {
        private readonly List<MagneticFluxSurface> _surfaces = new();
        private readonly IUIProvider _ui;
        private int _playerCount;

        public MagneticFluxManager(IUIProvider ui, int playerCount)
        {
            _ui = ui;
            _playerCount = playerCount;
        }

        /// <summary>放置面</summary>
        public void PlaceSurface(int ownerIndex, int tileA, int tileB, int direction)
        {
            _surfaces.Add(new MagneticFluxSurface
            {
                ownerPlayerIndex = ownerIndex,
                tileA = tileA,
                tileB = tileB,
                designatedDirection = direction,
                currentCount = 0,
                targetCount = _playerCount * 2,
                isCompleted = false
            });
        }

        /// <summary>玩家移动时检查所有面</summary>
        /// <returns>奖励 mol 给面的拥有者的列表</returns>
        public List<(int ownerIndex, int reward)> OnPlayerMoved(int fromTile, int toTile)
        {
            var rewards = new List<(int, int)>();

            foreach (var surface in _surfaces)
            {
                if (surface.isCompleted) continue;

                if (surface.OnPlayerCrosses(fromTile, toTile))
                {
                    rewards.Add((surface.ownerPlayerIndex, 20));
                    Debug.Log($"[MagneticFlux] Surface between {surface.tileA}-{surface.tileB} " +
                              $"completed! Owner: player {surface.ownerPlayerIndex}");
                }
            }

            // 清理已完成的面
            _surfaces.RemoveAll(s => s.isCompleted);

            return rewards;
        }

        /// <summary>获取所有活跃的面（用于UI显示）</summary>
        public IReadOnlyList<MagneticFluxSurface> GetActiveSurfaces()
        {
            return _surfaces.Where(s => !s.isCompleted).ToList();
        }
    }
}
