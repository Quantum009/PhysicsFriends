// ============================================================
// CardEffectProcessor.cs — 物理量牌主动/抉择效果处理器
// 处理速度(额外回合)、动量(撞晕)、压强(无效化)、力矩(旋转交换)等
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Board;
using PhysicsFriends.UI;

namespace PhysicsFriends.Systems
{
    public class CardEffectProcessor
    {
        private IUIProvider _ui;
        private BoardManager _board;
        private DeckManager _deck;

        // 外部注入的回调：用于设置回合上下文标志
        public Action OnExtraTurnGranted;

        /// <summary>无参构造（由 GameManager 早期创建，后续通过 Init 注入依赖）</summary>
        public CardEffectProcessor() { }

        /// <summary>完整构造</summary>
        public CardEffectProcessor(IUIProvider ui, BoardManager board, DeckManager deck)
        {
            _ui = ui;
            _board = board;
            _deck = deck;
        }

        /// <summary>延迟注入依赖（用于无参构造后补充）</summary>
        public void Init(IUIProvider ui, BoardManager board, DeckManager deck)
        {
            _ui = ui;
            _board = board;
            _deck = deck;
        }

        /// <summary>执行主动卡效果</summary>
        public IEnumerator ExecuteActiveEffect(PlayerState user, CardInstance card,
            List<PlayerState> allPlayers)
        {
            switch (card.cardId)
            {
                case PhysicsCardId.Velocity:
                    yield return ProcessSpeed(user);
                    break;
                case PhysicsCardId.Momentum:
                    yield return ProcessMomentum(user, allPlayers);
                    break;
                case PhysicsCardId.Resistance:
                    yield return ProcessResistance(user);
                    break;
                case PhysicsCardId.Torque:
                    yield return ProcessTorque(user, allPlayers);
                    break;
                case PhysicsCardId.MomentOfInertia:
                    yield return ProcessInertia(user, allPlayers);
                    break;
                case PhysicsCardId.AngularMomentum:
                    yield return ProcessAngularMomentum(user, allPlayers);
                    break;
                case PhysicsCardId.Entropy:
                    yield return ProcessEntropy(user, allPlayers);
                    break;
                case PhysicsCardId.CalorificValue:
                    yield return ProcessCalorificValue(user);
                    break;
                default:
                    Debug.LogWarning($"[CardEffect] 未实现的主动效果: {card.cardId}");
                    yield break;
            }

            // 消耗卡牌
            card.isUsed = true;
            user.RemoveCard(card);
        }

        /// <summary>执行抉择卡效果</summary>
        public IEnumerator ExecuteChoiceEffect(PlayerState user, CardInstance card,
            List<PlayerState> allPlayers)
        {
            switch (card.cardId)
            {
                case PhysicsCardId.Energy:
                case PhysicsCardId.Work:
                case PhysicsCardId.Heat:
                    yield return ProcessEnergyChoice(user);
                    break;
                case PhysicsCardId.Density:
                    yield return ProcessDensityChoice(user, allPlayers);
                    break;
                case PhysicsCardId.SpecificHeat:
                    yield return ProcessSpecificHeatChoice(user, allPlayers);
                    break;
                default:
                    Debug.LogWarning($"[CardEffect] 未实现的抉择效果: {card.cardId}");
                    yield break;
            }

            card.isUsed = true;
            user.RemoveCard(card);
        }

        // ================================================================
        // 主动效果实现
        // ================================================================

        /// <summary>速度：获得额外一回合</summary>
        private IEnumerator ProcessSpeed(PlayerState user)
        {
            OnExtraTurnGranted?.Invoke();
            _ui.SendNotification(new GameNotification(NotificationType.Info,
                "使用速度牌，获得额外一回合！", user));
            yield break;
        }

        /// <summary>动量：与同格玩家碰撞，对方眩晕3回合</summary>
        private IEnumerator ProcessMomentum(PlayerState user, List<PlayerState> allPlayers)
        {
            // 找同格的其他玩家
            var samePos = allPlayers.Where(p =>
                p.playerIndex != user.playerIndex &&
                p.currentTile == user.currentTile).ToList();

            if (samePos.Count == 0)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "同一格上没有其他玩家", user));
                yield break;
            }

            // 选择目标
            int targetIdx;
            if (samePos.Count == 1)
            {
                targetIdx = samePos[0].playerIndex;
            }
            else
            {
                var options = samePos.Select(p =>
                    new ChoiceOption(p.playerIndex.ToString(), p.playerName)).ToList();
                var cb = _ui.ShowChoice(new ChoiceRequest
                {
                    player = user,
                    title = "动量碰撞",
                    message = "选择碰撞目标",
                    options = options,
                    allowCancel = true
                });
                yield return new WaitUntil(() => cb.IsReady);
                if (string.IsNullOrEmpty(cb.Result)) yield break;
                targetIdx = int.Parse(cb.Result);
            }

            var target = allPlayers[targetIdx];
            target.skipTurns += 3;
            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"撞晕了{target.playerName}！眩晕3回合", user));
        }

        /// <summary>电阻：在任意格子放置路障</summary>
        private IEnumerator ProcessResistance(PlayerState user)
        {
            var tileCb = _ui.SelectTile(new TileSelectRequest
            {
                player = user,
                title = "放置路障：选择一个格子"
            });
            yield return new WaitUntil(() => tileCb.IsReady);

            if (tileCb.Result.selectedTileIndex < 0) yield break;

            int tileIdx = tileCb.Result.selectedTileIndex;
            _board.PlaceRoadblock(tileIdx);

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"在格子{tileIdx}放置了路障", user));
        }

        /// <summary>力矩：旋转交换——与一名玩家交换位置</summary>
        private IEnumerator ProcessTorque(PlayerState user, List<PlayerState> allPlayers)
        {
            var others = allPlayers.Where(p => p.playerIndex != user.playerIndex).ToList();
            var options = others.Select(p =>
                new ChoiceOption(p.playerIndex.ToString(), p.playerName)).ToList();

            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "力矩：旋转交换",
                message = "选择与谁交换位置",
                options = options,
                allowCancel = true
            });
            yield return new WaitUntil(() => cb.IsReady);

            if (string.IsNullOrEmpty(cb.Result)) yield break;
            int targetIdx = int.Parse(cb.Result);

            var target = allPlayers[targetIdx];
            int tempTile = user.currentTile;
            user.currentTile = target.currentTile;
            target.currentTile = tempTile;

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"与{target.playerName}交换了位置", user));
        }

        /// <summary>转动惯量：调转某玩家方向</summary>
        private IEnumerator ProcessInertia(PlayerState user, List<PlayerState> allPlayers)
        {
            var options = allPlayers.Select(p =>
                new ChoiceOption(p.playerIndex.ToString(), p.playerName)).ToList();

            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "转动惯量",
                message = "选择调转方向的目标",
                options = options,
                allowCancel = true
            });
            yield return new WaitUntil(() => cb.IsReady);

            if (string.IsNullOrEmpty(cb.Result)) yield break;
            int targetIdx = int.Parse(cb.Result);

            var target = allPlayers[targetIdx];
            target.moveDirection = target.moveDirection == MoveDirection.Clockwise
                ? MoveDirection.CounterClockwise : MoveDirection.Clockwise;

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"调转了{target.playerName}的方向", user));
        }

        /// <summary>角动量：调转自己方向+额外回合</summary>
        private IEnumerator ProcessAngularMomentum(PlayerState user, List<PlayerState> allPlayers)
        {
            user.moveDirection = user.moveDirection == MoveDirection.Clockwise
                ? MoveDirection.CounterClockwise : MoveDirection.Clockwise;
            OnExtraTurnGranted?.Invoke();
            _ui.SendNotification(new GameNotification(NotificationType.Info,
                "调转方向并获得额外回合！", user));
            yield break;
        }

        /// <summary>熵：翻开5张事件牌，可选择其中一张执行</summary>
        private IEnumerator ProcessEntropy(PlayerState user, List<PlayerState> allPlayers)
        {
            var events = new List<EventCardId>();
            for (int i = 0; i < 5; i++)
            {
                var e = _deck.DrawEvent();
                events.Add(e);
            }

            // 显示5张事件让玩家选择
            var options = events.Select(e =>
            {
                var def = EventCardDatabase.Get(e);
                return new ChoiceOption(((int)e).ToString(),
                    def?.nameZH ?? e.ToString(),
                    def?.descriptionZH ?? "");
            }).ToList();
            options.Add(new ChoiceOption("none", "不执行任何事件"));

            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "熵：事件选择",
                message = "翻开了5张事件牌，选择一张执行（或跳过）",
                options = options,
                allowCancel = false
            });
            yield return new WaitUntil(() => cb.IsReady);

            user.entropyUsedCount++;

            if (cb.Result != "none")
            {
                int eventId = int.Parse(cb.Result);
                // 交由 EventEffectProcessor 处理
                Debug.Log($"[CardEffect] 熵选择执行事件 {eventId}");
                // 具体执行由 TurnManagerAsync 调用 EventEffectProcessor
            }
        }

        /// <summary>热值：燃烧N张手牌获得N² mol</summary>
        private IEnumerator ProcessCalorificValue(PlayerState user)
        {
            // 选择要燃烧的卡（至少1张，不含热值自身——已被标记used）
            var unusedCards = user.handCards.Where(c => !c.isUsed).ToList();
            if (unusedCards.Count == 0)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "没有可燃烧的手牌", user));
                yield break;
            }

            var selectCb = _ui.SelectCards(new CardSelectRequest
            {
                player = user,
                title = "热值：燃烧",
                message = "选择要燃烧的手牌（获得 N² mol）",
                minSelect = 1,
                maxSelect = unusedCards.Count,
                filter = c => !c.isUsed
            });
            yield return new WaitUntil(() => selectCb.IsReady);

            if (selectCb.Result.cancelled) yield break;

            int burnCount = selectCb.Result.selectedCards.Count;
            foreach (var c in selectCb.Result.selectedCards)
                user.RemoveCard(c);

            int reward = burnCount * burnCount;
            user.mol += reward;

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"燃烧{burnCount}张牌，获得{reward}mol！", user));
        }

        // ================================================================
        // 抉择效果实现
        // ================================================================

        /// <summary>能量/功/热量：6mol 或 1张基本物理量</summary>
        private IEnumerator ProcessEnergyChoice(PlayerState user)
        {
            int lightBonus = GetLightIntensityBonus(user);
            int molReward = 6 + lightBonus;

            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "能量抉择",
                message = "选择奖励方式",
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("mol", $"获得 {molReward} mol"),
                    new ChoiceOption("card", "任选1张基本物理量牌")
                },
                allowCancel = false
            });
            yield return new WaitUntil(() => cb.IsReady);

            if (cb.Result == "mol")
            {
                user.mol += molReward;

                // 爱因斯坦能力：能量量纲牌奖励翻倍
                if (user.characterTaskCompleted &&
                    user.character == Character.Einstein)
                {
                    user.mol += molReward;
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"爱因斯坦能力：能量奖励翻倍！总共获得{molReward * 2}mol", user));
                }
            }
            else
            {
                yield return ChooseBasicCard(user);
            }
        }

        /// <summary>密度：给目标施加沉重或轻盈</summary>
        private IEnumerator ProcessDensityChoice(PlayerState user, List<PlayerState> allPlayers)
        {
            // 选择目标
            var targetOptions = allPlayers.Select(p =>
                new ChoiceOption(p.playerIndex.ToString(), p.playerName)).ToList();

            var targetCb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "密度：选择目标",
                message = "选择施加效果的玩家",
                options = targetOptions,
                allowCancel = true
            });
            yield return new WaitUntil(() => targetCb.IsReady);
            if (string.IsNullOrEmpty(targetCb.Result)) yield break;

            int targetIdx = int.Parse(targetCb.Result);
            var target = allPlayers[targetIdx];

            // 选择沉重/轻盈
            var effectCb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "密度：选择效果",
                message = $"对{target.playerName}施加什么？",
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("heavy", "沉重（步数减半，3回合）"),
                    new ChoiceOption("light", "轻盈（步数翻倍，3回合）")
                },
                allowCancel = false
            });
            yield return new WaitUntil(() => effectCb.IsReady);

            if (effectCb.Result == "heavy")
                target.heavyLayers += 3;
            else
                target.lightLayers += 3;

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"对{target.playerName}施加了{(effectCb.Result == "heavy" ? "沉重" : "轻盈")}效果", user));
        }

        /// <summary>比热容：吸收/释放 - 获得mol或给其他玩家mol</summary>
        private IEnumerator ProcessSpecificHeatChoice(PlayerState user, List<PlayerState> allPlayers)
        {
            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "比热容",
                message = "选择效果",
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("absorb", "吸热：获得3mol"),
                    new ChoiceOption("release", "放热：给所有其他玩家各1mol")
                },
                allowCancel = false
            });
            yield return new WaitUntil(() => cb.IsReady);

            if (cb.Result == "absorb")
            {
                user.mol += 3;
            }
            else
            {
                foreach (var p in allPlayers)
                {
                    if (p.playerIndex != user.playerIndex)
                        p.mol += 1;
                }
            }
        }

        // ================================================================
        // 工具
        // ================================================================

        private IEnumerator ChooseBasicCard(PlayerState user)
        {
            var basicOptions = new List<ChoiceOption>
            {
                new ChoiceOption(((int)PhysicsCardId.Time).ToString(), "时间"),
                new ChoiceOption(((int)PhysicsCardId.Length).ToString(), "长度"),
                new ChoiceOption(((int)PhysicsCardId.Mass).ToString(), "质量"),
                new ChoiceOption(((int)PhysicsCardId.Current).ToString(), "电流"),
                new ChoiceOption(((int)PhysicsCardId.Temperature).ToString(), "温度"),
                new ChoiceOption(((int)PhysicsCardId.LuminousIntensity).ToString(), "光照强度"),
            };

            var cb = _ui.ShowChoice(new ChoiceRequest
            {
                player = user,
                title = "选择基本物理量",
                message = "选择一张",
                options = basicOptions,
                allowCancel = false
            });
            yield return new WaitUntil(() => cb.IsReady);

            var cardId = (PhysicsCardId)int.Parse(cb.Result);
            user.GiveCard(cardId);
        }

        private int GetLightIntensityBonus(PlayerState player)
        {
            int lightCount = player.handCards.Count(c =>
                c.cardId == PhysicsCardId.LuminousIntensity && !c.isUsed);
            bool hasPlanck = player.handCards.Any(c =>
                c.cardId == PhysicsCardId.PlanckConstant && !c.isUsed);
            return hasPlanck ? lightCount * 2 : lightCount;
        }
    }
}
