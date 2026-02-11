// ============================================================
// RewardEffectProcessor.cs — 奖励牌效果处理器：执行25种奖励牌效果
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Board;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 奖励牌效果处理器
    /// </summary>
    public class RewardEffectProcessor
    {
        private DiceSystem _diceSystem;     // 骰子系统引用
        private DeckManager _deckManager;   // 牌堆管理器引用

        public RewardEffectProcessor(DiceSystem dice, DeckManager deck)
        {
            _diceSystem = dice;
            _deckManager = deck;
        }

        /// <summary>处理奖励牌效果</summary>
        public void ProcessReward(RewardCardId rewardId, PlayerState player,
            List<PlayerState> allPlayers, BoardManager board)
        {
            var def = RewardCardDatabase.Get(rewardId);
            Debug.Log($"[奖励] {player.playerName} 获得奖励：{def.nameZH}");

            switch (rewardId)
            {
                case RewardCardId.Accelerator:       // #1 加速器
                    // 增加一次投骰次数（由TurnManager处理额外行动）
                    Debug.Log("[奖励] 加速器：增加一次投骰");
                    break;

                case RewardCardId.HeatEngine:        // #2 热机
                    int roll = _diceSystem.RollUnmodified();
                    player.mol += roll;              // 点数=获得的mol
                    Debug.Log($"[奖励] 热机掷骰{roll}：获得{roll}mol");
                    break;

                case RewardCardId.Electromagnet:     // #3 电磁铁
                    ProcessElectromagnet(player);
                    break;

                case RewardCardId.TreasureSmall:     // #4 宝箱
                    player.mol += 1;
                    break;

                case RewardCardId.TreasureMedium:    // #5 稀有宝箱
                    player.mol += 2;
                    break;

                case RewardCardId.TreasureLarge:     // #6 传奇宝箱
                    player.mol += 5;
                    break;

                case RewardCardId.Paper:             // #7 论文
                    int paperRoll = _diceSystem.RollUnmodified();
                    var randomCard = _deckManager.DrawRandomBasic(paperRoll);
                    player.GiveCard(randomCard);
                    Debug.Log($"[奖励] 论文：随机获得{CardDatabase.Get(randomCard)?.nameZH}");
                    break;

                case RewardCardId.TopPaper:          // #8 顶级论文
                    // TODO: 由UI让玩家选择基本物理量
                    Debug.Log("[奖励] 顶级论文：等待玩家选择基本物理量");
                    break;

                case RewardCardId.LegendaryPaper:    // #9 传奇论文
                    var nonBasic = _deckManager.DrawRandomNonBasic();
                    player.GiveCard(nonBasic);
                    Debug.Log($"[奖励] 传奇论文：获得{CardDatabase.Get(nonBasic)?.nameZH}");
                    break;

                case RewardCardId.AncientPaper:      // #10 古老论文
                    var fromDiscard = _deckManager.physicsDeck.DrawFromDiscard();
                    if (!fromDiscard.Equals(default(PhysicsCardId)))
                        player.GiveCard(fromDiscard);
                    break;

                case RewardCardId.Stopwatch:         // #11 秒表
                    player.GiveCard(PhysicsCardId.Time);
                    break;

                case RewardCardId.AtomicClock:       // #12 原子钟
                    player.GiveCard(PhysicsCardId.Time);
                    player.GiveCard(PhysicsCardId.Time);
                    break;

                case RewardCardId.Balance:           // #13 托盘天平
                    player.GiveCard(PhysicsCardId.Mass);
                    break;

                case RewardCardId.KilogramPrototype: // #14 千克原器
                    player.GiveCard(PhysicsCardId.Mass);
                    player.GiveCard(PhysicsCardId.Mass);
                    break;

                case RewardCardId.Thermometer:       // #15 温度计
                    player.GiveCard(PhysicsCardId.Temperature);
                    break;

                case RewardCardId.Battery:           // #16 干电池
                    player.GiveCard(PhysicsCardId.Current);
                    break;

                case RewardCardId.Generator:         // #17 发电机
                    ProcessGenerator(player);
                    break;

                case RewardCardId.LightBulb:         // #18 电灯泡
                    player.GiveCard(PhysicsCardId.LuminousIntensity);
                    break;

                case RewardCardId.Ruler:             // #19 刻度尺
                    player.GiveCard(PhysicsCardId.Length);
                    break;

                case RewardCardId.Microscope:        // #20 工具显微镜
                    player.GiveCard(PhysicsCardId.Length);
                    player.GiveCard(PhysicsCardId.Length);
                    break;

                case RewardCardId.Laboratory:        // #21 实验室
                    ProcessBuildingReward(player, BuildingType.Laboratory, board);
                    break;

                case RewardCardId.ResearchInstitute: // #22 研究所
                    ProcessBuildingReward(player, BuildingType.ResearchInstitute, board);
                    break;

                case RewardCardId.LargeCollider:     // #23 大型对撞机
                    ProcessBuildingReward(player, BuildingType.LargeCollider, board);
                    break;

                case RewardCardId.Square:            // #24 平方（选择并复制一张手牌中的物理量牌）
                    // TODO: 由UI让玩家选择要复制的手牌
                    // 简化：复制第一张非基本物理量牌，如果没有则复制第一张手牌
                    if (player.handCards.Count > 0 && !player.IsHandFull())
                    {
                        var cardToCopy = player.handCards.FirstOrDefault(c =>
                            !CardDatabase.IsBasicQuantity(c.cardId) && !c.isUsed);
                        if (cardToCopy == null)
                            cardToCopy = player.handCards.FirstOrDefault(c => !c.isUsed);
                        if (cardToCopy != null)
                        {
                            player.GiveCard(cardToCopy.cardId);
                            Debug.Log($"[奖励] 平方：复制了{CardDatabase.Get(cardToCopy.cardId)?.nameZH}");
                        }
                    }
                    break;
            }
        }

        /// <summary>#3 电磁铁：消耗电流激活，他人不能越过</summary>
        private void ProcessElectromagnet(PlayerState player)
        {
            // 检查是否有电流牌可以消耗
            var currentCard = player.handCards.FirstOrDefault(
                c => c.cardId == PhysicsCardId.Current && !c.isUsed);
            if (currentCard != null)
            {
                player.RemoveCard(currentCard);          // 消耗一张电流
                player.electromagnetTurns = 3;           // 持续3回合
                player.electromagnetPosition = player.position; // 放在当前位置
                Debug.Log("[奖励] 电磁铁已激活，持续3回合");
            }
            else
            {
                Debug.Log("[奖励] 电磁铁激活失败：没有电流牌");
            }
        }

        /// <summary>#17 发电机：消耗一张力学量，获得两张电流</summary>
        private void ProcessGenerator(PlayerState player)
        {
            // 查找一张力学量牌
            var mechCard = player.handCards.FirstOrDefault(c =>
            {
                var def = CardDatabase.Get(c.cardId);
                return def != null && def.branch == PhysicsBranch.Mechanics && !c.isUsed;
            });

            if (mechCard != null)
            {
                player.RemoveCard(mechCard);             // 消耗力学量牌
                player.GiveCard(PhysicsCardId.Current);  // +2电流
                player.GiveCard(PhysicsCardId.Current);
                Debug.Log($"[奖励] 发电机：消耗{CardDatabase.Get(mechCard.cardId)?.nameZH}，获得2张电流");
            }
            else
            {
                Debug.Log("[奖励] 发电机激活失败：没有力学量牌");
            }
        }

        /// <summary>建筑类奖励处理：在同色领地/商店建造建筑</summary>
        private void ProcessBuildingReward(PlayerState player, BuildingType type, BoardManager board)
        {
            // 查找该玩家颜色的所有格子
            var ownedTiles = board.tiles.Where(t =>
                (t.tileType == TileType.Territory || t.tileType == TileType.Shop) &&
                t.ownerColor == player.color &&
                t.buildings.Count == 0  // 没有已建建筑
            ).ToList();

            if (ownedTiles.Count > 0)
            {
                // TODO: 由UI让玩家选择在哪个格子建造
                // 简化：建在第一个可用的格子上
                var targetTile = ownedTiles[0];
                board.PlaceBuilding(type, targetTile.index, player.playerIndex);

                var building = new BuildingInstance(type, targetTile.index, player.playerIndex);
                player.buildings.Add(building);

                Debug.Log($"[奖励] 在格子{targetTile.index}建造了{type}");
            }
        }
    }
}
