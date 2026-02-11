// ============================================================
// TradeManager.cs — 交易系统：玩家之间交换卡牌和mol
// 仅在他人商店格触发，双方需确认
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
using PhysicsFriends.UI;

namespace PhysicsFriends.Systems
{
    /// <summary>交易提案</summary>
    public class TradeOffer
    {
        public int initiatorIndex;
        public int targetIndex;
        public List<CardInstance> offeredCards = new();
        public int offeredMol;
        public List<PhysicsCardId> requestedCardIds = new();
        public int requestedMol;
    }

    public class TradeManager
    {
        private readonly IUIProvider _ui;

        public TradeManager(IUIProvider ui)
        {
            _ui = ui;
        }

        /// <summary>发起交易流程</summary>
        public IEnumerator ExecuteTradeAsync(PlayerState initiator, PlayerState target)
        {
            // ---- 第1步：发起方选择要给出的卡和mol ----
            var tradeCb = _ui.ShowTrade(new TradeRequest
            {
                buyer = initiator,
                seller = target
            });
            yield return new WaitUntil(() => tradeCb.IsReady);

            if (tradeCb.Result.cancelled)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "交易取消", initiator));
                yield break;
            }

            var offer = tradeCb.Result;

            // ---- 第2步：验证发起方资源足够 ----
            if (offer.offeredMol > initiator.mol)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "mol不足，交易失败", initiator));
                yield break;
            }

            foreach (var card in offer.offeredCards)
            {
                if (!initiator.handCards.Contains(card))
                {
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        "手牌不匹配，交易失败", initiator));
                    yield break;
                }
            }

            // ---- 第3步：询问目标方是否接受 ----
            string offerDesc = BuildOfferDescription(offer, initiator);
            var confirmCb = _ui.ShowChoice(new ChoiceRequest
            {
                player = target,
                title = $"{initiator.playerName}发起交易",
                message = offerDesc,
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("accept", "接受"),
                    new ChoiceOption("reject", "拒绝")
                },
                allowCancel = false
            });
            yield return new WaitUntil(() => confirmCb.IsReady);

            if (confirmCb.Result != "accept")
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    $"{target.playerName}拒绝了交易", initiator));
                yield break;
            }

            // ---- 第4步：验证目标方资源足够 ----
            if (offer.requestedMol > target.mol)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "对方mol不足，交易失败", initiator));
                yield break;
            }

            // ---- 第5步：执行交易 ----
            ExecuteSwap(initiator, target, offer);

            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"交易成功！", initiator));
            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"与{initiator.playerName}的交易成功！", target));

            Debug.Log($"[Trade] Player {initiator.playerIndex} ↔ Player {target.playerIndex} completed");
        }

        private void ExecuteSwap(PlayerState initiator, PlayerState target, TradeResponse offer)
        {
            // 发起方 → 目标方
            foreach (var card in offer.offeredCards)
            {
                initiator.RemoveCard(card);
                target.GiveCard(card.cardId);
            }
            if (offer.offeredMol > 0)
            {
                initiator.mol -= offer.offeredMol;
                target.mol += offer.offeredMol;
            }

            // 目标方 → 发起方
            foreach (var reqId in offer.requestedCardIds)
            {
                var card = target.handCards.FirstOrDefault(c => c.cardId == reqId && !c.isUsed);
                if (card != null)
                {
                    target.RemoveCard(card);
                    initiator.GiveCard(reqId);
                }
            }
            if (offer.requestedMol > 0)
            {
                target.mol -= offer.requestedMol;
                initiator.mol += offer.requestedMol;
            }
        }

        private string BuildOfferDescription(TradeResponse offer, PlayerState initiator)
        {
            var parts = new List<string>();
            parts.Add($"【{initiator.playerName}提出交易】\n");

            if (offer.offeredCards.Count > 0 || offer.offeredMol > 0)
            {
                parts.Add("给你：");
                foreach (var c in offer.offeredCards)
                    parts.Add($"  · {CardDatabase.Get(c.cardId)?.nameZH}");
                if (offer.offeredMol > 0)
                    parts.Add($"  · {offer.offeredMol} mol");
            }

            if (offer.requestedCardIds.Count > 0 || offer.requestedMol > 0)
            {
                parts.Add("\n索要：");
                foreach (var id in offer.requestedCardIds)
                    parts.Add($"  · {CardDatabase.Get(id)?.nameZH}");
                if (offer.requestedMol > 0)
                    parts.Add($"  · {offer.requestedMol} mol");
            }

            return string.Join("\n", parts);
        }
    }
}
