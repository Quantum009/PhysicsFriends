// ============================================================
// BuildingManager.cs — 建筑系统：放置、收费、天文台效果
// 建筑类型：实验室(1mol) / 研究所(2mol) / 对撞机(5mol) / 天文台(交卡)
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Board;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.UI;

namespace PhysicsFriends.Systems
{
    /// <summary>格子上的建筑数据</summary>
    [Serializable]
    public class BuildingData
    {
        public int tileIndex;
        public BuildingType type;
        public int ownerPlayerIndex;

        public int GetToll()
        {
            return type switch
            {
                BuildingType.Laboratory => 1,
                BuildingType.ResearchInstitute  => 2,
                BuildingType.LargeCollider   => 5,
                BuildingType.Observatory => 0, // 天文台不收mol
                _ => 0
            };
        }
    }

    public class BuildingManager
    {
        private readonly List<BuildingData> _buildings = new();
        private readonly BoardManager _board;
        private readonly IUIProvider _ui;

        public BuildingManager(BoardManager board, IUIProvider ui)
        {
            _board = board;
            _ui = ui;
        }

        public IReadOnlyList<BuildingData> AllBuildings => _buildings;

        // ================================================================
        // 建造
        // ================================================================

        /// <summary>放置建筑（由奖励牌触发）</summary>
        public IEnumerator PlaceBuildingAsync(PlayerState player, BuildingType type)
        {
            // 获取玩家可建造的格子（自己颜色的领地格）
            var validTiles = GetBuildableTiles(player);
            if (validTiles.Count == 0)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "没有可建造的领地格", player));
                yield break;
            }

            // 让玩家选择格子
            var validSet = new HashSet<int>(validTiles);
            var tileCb = _ui.SelectTile(new TileSelectRequest
            {
                player = player,
                title = $"建造{GetBuildingName(type)}：选择一个你的领地格",
                filter = idx => validSet.Contains(idx)
            });
            yield return WaitFor(tileCb);

            if (tileCb.Result.selectedTileIndex < 0) yield break;

            int tileIdx = tileCb.Result.selectedTileIndex;

            // 检查是否已有建筑（升级逻辑）
            var existing = _buildings.FirstOrDefault(b => b.tileIndex == tileIdx);
            if (existing != null)
            {
                // 替换（旧建筑消失）
                _buildings.Remove(existing);
            }

            var building = new BuildingData
            {
                tileIndex = tileIdx,
                type = type,
                ownerPlayerIndex = player.playerIndex
            };
            _buildings.Add(building);

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"在格子{tileIdx}建造了{GetBuildingName(type)}", player));

            Debug.Log($"[Building] Player {player.playerIndex} built {type} on tile {tileIdx}");
        }

        // ================================================================
        // 收费（经过时触发）
        // ================================================================

        /// <summary>处理玩家经过建筑格时的收费</summary>
        public IEnumerator ProcessBuildingToll(PlayerState passerBy, int tileIndex,
            List<PlayerState> allPlayers)
        {
            var building = _buildings.FirstOrDefault(b => b.tileIndex == tileIndex);
            if (building == null) yield break;

            // 自己的建筑不收费
            if (building.ownerPlayerIndex == passerBy.playerIndex) yield break;

            var owner = allPlayers[building.ownerPlayerIndex];

            if (building.type == BuildingType.Observatory)
            {
                // 天文台：上交一张手牌给建筑主人
                yield return ProcessObservatoryToll(passerBy, owner);
            }
            else
            {
                int toll = building.GetToll();
                int actualPay = Math.Min(toll, passerBy.mol);

                passerBy.mol -= actualPay;

                // 银行垫付：即使玩家不够，建筑主人照拿全额
                owner.mol += toll;

                string msg = actualPay < toll
                    ? $"经过{GetBuildingName(building.type)}，支付{actualPay}mol（银行垫付{toll - actualPay}）"
                    : $"经过{GetBuildingName(building.type)}，支付{toll}mol给{owner.playerName}";

                _ui.SendNotification(new GameNotification(NotificationType.MolChange, msg, passerBy));

                Debug.Log($"[Building] Player {passerBy.playerIndex} pays {toll}mol " +
                          $"to player {owner.playerIndex} ({building.type})");
            }
        }

        private IEnumerator ProcessObservatoryToll(PlayerState passerBy, PlayerState owner)
        {
            var unusedCards = passerBy.handCards.Where(c => !c.isUsed).ToList();
            if (unusedCards.Count == 0)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "没有手牌可以上交", passerBy));
                yield break;
            }

            // 让经过者选择一张牌上交
            var selectCb = _ui.SelectCards(new CardSelectRequest
            {
                player = passerBy,
                title = "天文台",
                message = $"经过{owner.playerName}的天文台，请选择一张手牌上交",
                minSelect = 1,
                maxSelect = 1,
                filter = c => !c.isUsed
            });
            yield return WaitFor(selectCb);

            if (!selectCb.Result.cancelled && selectCb.Result.selectedCards.Count > 0)
            {
                var card = selectCb.Result.selectedCards[0];
                passerBy.RemoveCard(card);

                // 给建筑主人
                owner.GiveCard(card.cardId);

                _ui.SendNotification(new GameNotification(NotificationType.CardLost,
                    $"上交了{CardDatabase.Get(card.cardId)?.nameZH}给{owner.playerName}", passerBy));
            }
        }

        // ================================================================
        // 查询
        // ================================================================

        public BuildingData GetBuildingAt(int tileIndex)
        {
            return _buildings.FirstOrDefault(b => b.tileIndex == tileIndex);
        }

        public List<BuildingData> GetPlayerBuildings(int playerIndex)
        {
            return _buildings.Where(b => b.ownerPlayerIndex == playerIndex).ToList();
        }

        private List<int> GetBuildableTiles(PlayerState player)
        {
            var result = new List<int>();
            int boardSize = _board.TotalTiles;

            for (int i = 0; i < boardSize; i++)
            {
                var tile = _board.GetTile(i);
                if (tile == null) continue;
                // 只能建在自己颜色的领地格上，且尚未有建筑
                if (tile.tileType == TileType.Territory &&
                    tile.ownerColor == player.color &&
                    !_buildings.Any(b => b.tileIndex == i))
                {
                    result.Add(i);
                }
            }
            return result;
        }

        public static string GetBuildingName(BuildingType type)
        {
            return type switch
            {
                BuildingType.Laboratory  => "实验室",
                BuildingType.ResearchInstitute   => "研究所",
                BuildingType.LargeCollider    => "大型对撞机",
                BuildingType.Observatory => "天文台",
                _ => "未知建筑"
            };
        }

        // ================================================================
        // 序列化（存档用）
        // ================================================================

        public List<BuildingData> GetBuildingSnapshot()
        {
            return _buildings.Select(b => new BuildingData
            {
                tileIndex = b.tileIndex,
                type = b.type,
                ownerPlayerIndex = b.ownerPlayerIndex
            }).ToList();
        }

        public void RestoreFromSnapshot(List<BuildingData> snapshot)
        {
            _buildings.Clear();
            _buildings.AddRange(snapshot);
        }

        // 协程等待辅助
        private static object WaitFor<T>(UICallback<T> cb)
        {
            return new WaitUntil(() => cb.IsReady);
        }
    }
}
