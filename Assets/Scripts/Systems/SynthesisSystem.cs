// ============================================================
// SynthesisSystem.cs — 合成系统：基于量纲法则的卡牌合成
// 每张卡的量纲可以取正（相乘）或取负（相除），共2^n种组合
// 如果合成目标是合成材料之一，也要将其排除
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Cards;
using PhysicsFriends.Player;

namespace PhysicsFriends.Systems
{
    /// <summary>合成结果</summary>
    public class SynthesisResult
    {
        public bool success;                        // 合成是否成功
        public List<PhysicsCardId> possibleOutputs; // 可能产出的卡牌ID列表
        public PhysicsCardId chosenOutput;           // 玩家选择的产出
        public PhysicsCardId targetId;               // UI用：推荐合成目标
        public List<PhysicsCardId> materialIds;      // UI用：所需材料的卡牌ID列表
        public Dimension resultDimension;            // 合成后的量纲
        public string errorMessage;                 // 错误信息（如果失败）

        public SynthesisResult()
        {
            success = false;
            possibleOutputs = new List<PhysicsCardId>();
            materialIds = new List<PhysicsCardId>();
            errorMessage = "";
        }
    }

    /// <summary>
    /// 合成系统：处理物理量卡牌的合成逻辑
    /// 
    /// 核心合成规则（来自规则书补充）：
    /// 将选中的卡牌进行遍历：其量纲(六维数组)可以全部取正(相乘),也可以全部取负(相除),
    /// 最后将这些数据带符号地相加。若选择了n张牌,则一共有2^n种组合方式。
    /// 检查所有可能的结果是否在物理量表格内。若在,则列出作为可以合成的目标。
    /// 如果合成目标是合成材料之一,也要将其排除。
    /// </summary>
    public class SynthesisSystem
    {
        /// <summary>简化版合成：接受PhysicsCardId列表（可选指定目标）</summary>
        public SynthesisResult TrySynthesize(List<PhysicsCardId> cardIds,
            PhysicsCardId targetId = default(PhysicsCardId))
        {
            var instances = cardIds.Select(id => new CardInstance(id)).ToList();
            var result = TrySynthesize(instances);

            // 如果指定了targetId且合成成功，过滤结果只保留目标
            if (targetId != default(PhysicsCardId) && result.success &&
                result.possibleOutputs.Contains(targetId))
            {
                result.possibleOutputs = new List<PhysicsCardId> { targetId };
                result.chosenOutput = targetId;
            }

            return result;
        }

        /// <summary>
        /// 尝试合成：遍历2^n种符号组合，检查每种组合的量纲是否匹配已知物理量
        /// </summary>
        public SynthesisResult TrySynthesize(List<CardInstance> selectedCards)
        {
            var result = new SynthesisResult();

            if (selectedCards.Count < 2)
            {
                result.errorMessage = "合成至少需要2张物理量牌";
                return result;
            }

            // 收集所有输入卡牌的ID（用于排除合成目标是材料之一的情况）
            var inputCardIds = new HashSet<PhysicsCardId>(selectedCards.Select(c => c.cardId));

            // 获取每张卡牌的量纲
            var dimensions = new List<Dimension>();
            for (int i = 0; i < selectedCards.Count; i++)
            {
                var def = CardDatabase.Get(selectedCards[i].cardId);
                if (def == null)
                {
                    result.errorMessage = $"未找到卡牌定义：{selectedCards[i].cardId}";
                    return result;
                }
                dimensions.Add(def.dimension);
            }

            int n = selectedCards.Count;
            int totalCombinations = 1 << n; // 2^n 种组合
            var allValidOutputs = new HashSet<PhysicsCardId>();

            // 遍历所有 2^n 种符号组合
            for (int mask = 0; mask < totalCombinations; mask++)
            {
                Dimension totalDim = new Dimension(0, 0, 0, 0, 0, 0);
                for (int i = 0; i < n; i++)
                {
                    // bit=1: 取正(相乘), bit=0: 取负(相除)
                    if ((mask & (1 << i)) != 0)
                        totalDim = totalDim + dimensions[i];
                    else
                        totalDim = totalDim + (-dimensions[i]);
                }

                if (totalDim.IsZero()) continue;

                var matches = CardDatabase.FindByDimension(totalDim);
                foreach (var matchId in matches)
                {
                    if (CardDatabase.IsBasicQuantity(matchId)) continue;
                    if (inputCardIds.Contains(matchId)) continue;
                    allValidOutputs.Add(matchId);
                }
            }

            if (allValidOutputs.Count == 0)
            {
                result.errorMessage = "没有物理量匹配任何可能的量纲组合（或结果均为输入材料）";
                return result;
            }

            result.success = true;
            result.possibleOutputs = allValidOutputs.ToList();

            if (result.possibleOutputs.Count == 1)
                result.chosenOutput = result.possibleOutputs[0];

            return result;
        }

        /// <summary>
        /// 执行合成：消耗选中的卡牌，给予玩家产出的卡牌
        /// </summary>
        public CardInstance ExecuteSynthesis(
            PlayerState player,
            List<CardInstance> selectedCards,
            PhysicsCardId outputCardId)
        {
            foreach (var card in selectedCards)
            {
                player.RemoveCard(card);
                Debug.Log($"[合成] 消耗：{CardDatabase.Get(card.cardId)?.nameZH}");
            }

            // === 创举2：浮力定律 - 利用密度+加速度+长度合成力 ===
            if (outputCardId == PhysicsCardId.Force)
            {
                var usedIds = selectedCards.Select(c => c.cardId).ToList();
                if (usedIds.Contains(PhysicsCardId.Density) &&
                    usedIds.Contains(PhysicsCardId.Acceleration) &&
                    usedIds.Contains(PhysicsCardId.Length))
                {
                    player.buoyancySynthesisCompleted = true;
                    Debug.Log("[合成] 浮力定律合成标记完成！（密度+加速度+长度→力）");
                }
            }

            // === 创举8：位移电流 - 阶段1: 合成电位移矢量 ===
            if (outputCardId == PhysicsCardId.DisplacementVector)
            {
                player.hasCompletedDisplacementVector = true;
                Debug.Log("[合成] 位移电流阶段1标记：电位移矢量已合成");
            }

            // === 创举8：位移电流 - 阶段2: 合成电位移通量 ===
            if (outputCardId == PhysicsCardId.DisplacementFlux)
            {
                player.hasCompletedDisplacementFlux = true;
                Debug.Log("[合成] 位移电流阶段2标记：电位移通量已合成");
            }

            // === 创举8：位移电流 - 利用电位移通量合成电流 ===
            if (outputCardId == PhysicsCardId.Current)
            {
                var usedIds = selectedCards.Select(c => c.cardId).ToList();
                if (usedIds.Contains(PhysicsCardId.DisplacementFlux))
                {
                    player.displacementCurrentCompleted = true;
                    Debug.Log("[合成] 位移电流合成标记完成！（电位移通量→电流）");
                }
            }

            var output = player.GiveCard(outputCardId);
            Debug.Log($"[合成] 获得：{CardDatabase.Get(outputCardId)?.nameZH}");
            return output;
        }

        /// <summary>
        /// 检查特殊的同量纲转换
        /// 电压↔电势、能量↔力矩 需要消耗两张同量纲牌转换
        /// 功/能量/热量 之间可以任意互换
        /// </summary>
        public bool CanConvert(PhysicsCardId from, PhysicsCardId to)
        {
            if ((from == PhysicsCardId.Voltage && to == PhysicsCardId.ElectricPotential) ||
                (from == PhysicsCardId.ElectricPotential && to == PhysicsCardId.Voltage))
                return true;

            if ((from == PhysicsCardId.Energy && to == PhysicsCardId.Torque) ||
                (from == PhysicsCardId.Torque && to == PhysicsCardId.Energy))
                return true;

            if ((from == PhysicsCardId.Work && to == PhysicsCardId.Torque) ||
                (from == PhysicsCardId.Torque && to == PhysicsCardId.Work))
                return true;

            var energyGroup = new HashSet<PhysicsCardId>
            {
                PhysicsCardId.Work, PhysicsCardId.Energy, PhysicsCardId.Heat
            };
            if (energyGroup.Contains(from) && energyGroup.Contains(to) && from != to)
                return true;

            return false;
        }

        /// <summary>执行同量纲转换：消耗两张同量纲牌，获得目标卡牌</summary>
        public CardInstance ExecuteConversion(
            PlayerState player, CardInstance card1, CardInstance card2, PhysicsCardId targetId)
        {
            player.RemoveCard(card1);
            player.RemoveCard(card2);
            return player.GiveCard(targetId);
        }

        /// <summary>
        /// 给定选中的材料卡，列出所有可能的合成产物
        /// 用于"先选材料，再选产物"的UI流程
        /// 当相同量纲有多种产物（如能量/力矩）时，玩家在此步选择
        /// </summary>
        public List<PhysicsCardId> GetPossibleOutputs(List<CardInstance> selectedCards)
        {
            var result = TrySynthesize(selectedCards);
            return result.success ? result.possibleOutputs : new List<PhysicsCardId>();
        }

        /// <summary>获取合成路径提示</summary>
        public List<string> GetSynthesisHints(PlayerState player)
        {
            var hints = new List<string>();
            var cards = player.handCards.Where(c => !c.isUsed).ToList();
            if (cards.Count < 2) return hints;

            var dimCounts = new Dictionary<Dimension, int>();
            foreach (var card in cards)
            {
                var def = CardDatabase.Get(card.cardId);
                if (def != null)
                {
                    if (!dimCounts.ContainsKey(def.dimension))
                        dimCounts[def.dimension] = 0;
                    dimCounts[def.dimension]++;
                }
            }

            hints.Add("当前可用手牌量纲统计：");
            foreach (var kvp in dimCounts)
            {
                var possibleCards = CardDatabase.FindByDimension(kvp.Key);
                string cardNames = string.Join(", ",
                    possibleCards.Select(id => CardDatabase.Get(id)?.nameZH));
                hints.Add($"  {kvp.Key} x{kvp.Value} → {cardNames}");
            }
            return hints;
        }

        /// <summary>
        /// 查找给定手牌可以合成的所有可能结果
        /// 遍历所有2~n张牌的组合，每个组合用2^n符号遍历
        /// </summary>
        public List<SynthesisResult> FindPossibleSyntheses(List<PhysicsCardId> handCardIds)
        {
            var results = new List<SynthesisResult>();
            if (handCardIds.Count < 2) return results;

            var foundOutputs = new HashSet<PhysicsCardId>();

            // 遍历所有可能的组合大小（2张到全部手牌）
            // 最多10张手牌，总计算量约5万次量纲运算，完全可接受
            int maxComboSize = handCardIds.Count;

            for (int comboSize = 2; comboSize <= maxComboSize; comboSize++)
            {
                foreach (var combo in GetCombinations(handCardIds, comboSize))
                {
                    var r = TrySynthesize(combo);
                    if (r.success)
                    {
                        foreach (var outId in r.possibleOutputs)
                        {
                            if (!foundOutputs.Contains(outId))
                            {
                                foundOutputs.Add(outId);
                                results.Add(new SynthesisResult
                                {
                                    success = true,
                                    possibleOutputs = new List<PhysicsCardId> { outId },
                                    targetId = outId,
                                    materialIds = new List<PhysicsCardId>(combo)
                                });
                            }
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>从列表中获取所有大小为k的组合</summary>
        private IEnumerable<List<PhysicsCardId>> GetCombinations(List<PhysicsCardId> source, int k)
        {
            if (k == 0) { yield return new List<PhysicsCardId>(); yield break; }
            if (k > source.Count) yield break;

            int[] indices = new int[k];
            for (int i = 0; i < k; i++) indices[i] = i;

            while (true)
            {
                var combo = new List<PhysicsCardId>();
                for (int i = 0; i < k; i++) combo.Add(source[indices[i]]);
                yield return combo;

                int pos = k - 1;
                while (pos >= 0 && indices[pos] == source.Count - k + pos) pos--;
                if (pos < 0) break;

                indices[pos]++;
                for (int i = pos + 1; i < k; i++) indices[i] = indices[i - 1] + 1;
            }
        }
    }
}
