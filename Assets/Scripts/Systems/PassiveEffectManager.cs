// ============================================================
// PassiveEffectManager.cs — 被动效果管理器：处理经过起点/回合触发的被动效果
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
    /// 被动效果管理器：
    /// - 经过起点时触发的被动效果（电流、电压、功率、面积、体积、电容、电荷等）
    /// - 磁场方向效果（每回合）
    /// - 电位跳回合效果
    /// - 光照强度增益计算
    /// </summary>
    public class PassiveEffectManager
    {
        /// <summary>
        /// 玩家经过起点时触发所有被动效果
        /// </summary>
        public void OnPassStart(PlayerState player)
        {
            Debug.Log($"[被动] {player.playerName} 经过起点，触发被动效果");

            // === 电流：经过起点+1mol（不受光强影响）===
            int currentCount = player.CountCards(PhysicsCardId.Current);
            if (currentCount > 0)
            {
                int molGain = currentCount; // 每张+1mol，电流不受光强影响
                player.mol += molGain;
                Debug.Log($"[被动] 电流×{currentCount}：经过起点+{molGain}mol");
            }

            // === 电压：经过起点+1电流卡（受光照影响）===
            int voltageCount = player.CountCards(PhysicsCardId.Voltage);
            if (voltageCount > 0)
            {
                int lightBonus = player.CalculateLightBonus();
                int cardGainPerVoltage = 1 + lightBonus; // 每张电压获得(1+光照增益)张电流
                int totalCardGain = voltageCount * cardGainPerVoltage;
                for (int i = 0; i < totalCardGain; i++)
                    player.GiveCard(PhysicsCardId.Current);
                Debug.Log($"[被动] 电压×{voltageCount}：经过起点+{totalCardGain}张电流（每张{cardGainPerVoltage}）");
            }

            // === 功率：经过起点+1能量卡 ===
            int powerCount = player.CountCards(PhysicsCardId.Power);
            if (powerCount > 0)
            {
                for (int i = 0; i < powerCount; i++)
                    player.GiveCard(PhysicsCardId.Energy);
                Debug.Log($"[被动] 功率×{powerCount}：经过起点+{powerCount}张能量");
            }

            // === 面积（容器至多2mol）：经过起点翻倍 ===
            // 规则书："一个储存至多2mol的容器。每经过一次起点，容器中物质的量翻倍"
            // 如果玩家选择存入0mol，翻倍后仍为0，这是合理的
            foreach (var card in player.handCards.Where(
                c => c.cardId == PhysicsCardId.Area && !c.isUsed))
            {
                card.containerMol *= 2; // 容器mol翻倍
                if (card.containerMol > 2) card.containerMol = 2; // 规则书：至多2mol
                Debug.Log($"[被动] 面积容器：翻倍至{card.containerMol}mol（上限2）");
            }

            // === 体积（容器至多5mol）：经过起点翻倍 ===
            foreach (var card in player.handCards.Where(
                c => c.cardId == PhysicsCardId.Volume && !c.isUsed))
            {
                card.containerMol *= 2; // 容器mol翻倍
                if (card.containerMol > 5) card.containerMol = 5; // 规则书：至多5mol
                Debug.Log($"[被动] 体积容器：翻倍至{card.containerMol}mol（上限5）");
            }

            // === 电容（存储1张电学卡）：经过起点存储量效果加倍 ===
            foreach (var card in player.handCards.Where(
                c => c.cardId == PhysicsCardId.Capacitance && !c.isUsed))
            {
                // 电容的被动效果：经过起点时如果有存储卡，获得额外奖励
                if (card.containerCard != null)
                {
                    player.GiveCard(card.containerCard.cardId); // 复制存储的卡
                    Debug.Log($"[被动] 电容：经过起点获得额外{CardDatabase.Get(card.containerCard.cardId)?.nameZH}");
                }
            }

            // === 电荷（已激活）：经过起点+2电流 ===
            var chargeCards = player.handCards.Where(
                c => c.cardId == PhysicsCardId.Charge && c.chargeActivated).ToList();
            foreach (var card in chargeCards)
            {
                player.GiveCard(PhysicsCardId.Current);
                player.GiveCard(PhysicsCardId.Current);
                card.chargeActivated = false; // 一次性效果
                Debug.Log("[被动] 电荷：经过起点+2电流");
            }

            // === 电势：经过起点后跳过1回合，然后连续行动3回合 ===
            if (player.HasCard(PhysicsCardId.ElectricPotential))
            {
                player.potentialCharging = true;    // 标记下回合跳过
                // 设为4：TickBuffs在跳过回合也会递减一次(4→3)，之后3回合行动(3→2→1→0)
                player.potentialExtraTurns = 4;
                Debug.Log("[被动] 电势：下回合跳过，之后连续行动3回合");
            }

            // === 《墨经》：每经过一次起点获得1张光照强度 ===
            if (player.hasMoJing)
            {
                player.GiveCard(PhysicsCardId.LuminousIntensity);
                Debug.Log("[被动] 《墨经》：经过起点+1光照强度");
            }
        }

        /// <summary>
        /// 每回合开始时触发的效果
        /// </summary>
        public void OnTurnStart(PlayerState player, List<PlayerState> allPlayers)
        {
            // === 磁感应强度：沿卡牌选定方向行动+1mol，逆方向-1mol（可叠加）===
            // 规则书：选择一个方向，沿该方向行动获得1mol，逆该方向行动失去1mol
            var magnetCards = player.handCards.FindAll(c =>
                c.cardId == PhysicsCardId.MagneticField && !c.isUsed && !c.isDisabled);
            foreach (var mc in magnetCards)
            {
                // 梦溪笔谈：磁感应强度增益/惩罚翻倍
                int multiplier = player.hasMengXiBiTan ? 2 : 1;

                if (mc.chosenDirection == player.moveDirection)
                {
                    player.mol += 1 * multiplier;
                    Debug.Log($"[被动] 磁感应强度(顺方向)：+{1 * multiplier}mol");
                }
                else
                {
                    int loss = 1 * multiplier;
                    player.mol = Math.Max(0, player.mol - loss);
                    Debug.Log($"[被动] 磁感应强度(逆方向)：-{loss}mol");
                }
            }
        }

        /// <summary>
        /// 处理buff/debuff回合倒计时
        /// </summary>
        public void TickBuffs(PlayerState player)
        {
            // 绝对零度
            if (player.absoluteZeroTurns > 0)
                player.absoluteZeroTurns--;

            // 迈克尔逊-莫雷（固定步数6）
            if (player.michelsonMorleyTurns > 0)
                player.michelsonMorleyTurns--;

            // 电磁屏蔽
            if (player.emShieldTurns > 0)
                player.emShieldTurns--;

            // 电磁铁
            if (player.electromagnetTurns > 0)
                player.electromagnetTurns--;

            // 密度-沉重
            if (player.densityHeavyTurns > 0)
                player.densityHeavyTurns--;

            // 密度-轻盈
            if (player.densityLightTurns > 0)
                player.densityLightTurns--;

            // 眩晕
            if (player.stunTurns > 0)
                player.stunTurns--;

            // 相变-固态跳过
            if (player.phaseSkipNext)
            {
                player.phaseSkipNext = false;
                player.phaseState = PhaseState.None;
            }

            // 电位连续行动
            if (player.potentialExtraTurns > 0)
                player.potentialExtraTurns--;
        }

        /// <summary>
        /// 检查玩家是否需要跳过本回合
        /// </summary>
        public bool ShouldSkipTurn(PlayerState player)
        {
            // 眩晕（被动量撞击）
            if (player.stunTurns > 0)
            {
                Debug.Log($"[跳过] {player.playerName} 被眩晕，跳过回合（剩余{player.stunTurns}回合）");
                return true;
            }

            // 绝对零度
            if (player.absoluteZeroTurns > 0)
            {
                Debug.Log($"[跳过] {player.playerName} 绝对零度冻结，跳过回合（剩余{player.absoluteZeroTurns}回合）");
                return true;
            }

            // 电位跳过
            if (player.potentialCharging)
            {
                Debug.Log($"[跳过] {player.playerName} 电位效果，跳过1回合");
                player.potentialCharging = false;
                return true;
            }

            // 相变-固态跳过
            if (player.phaseState == PhaseState.Solid && player.phaseSkipNext)
            {
                Debug.Log($"[跳过] {player.playerName} 相变固态，跳过回合");
                return true;
            }

            // 路障停留 - 已统一由 PlayerState.CheckSkip() 通过 roadblockSkipTurns 处理
            // 不再在此重复检查 stoppedByRoadblock

            return false;
        }

        /// <summary>
        /// 计算步数修正（沉重/轻盈层）
        /// 规则书：多个沉重或轻盈的效果可以相互抵消。如果有多层沉重/轻盈，只结算一次。
        /// 来源：密度(3回合)、电阻率(永久)、电场强度(永久)、气态(临时)
        /// </summary>
        public int CalculateActualSteps(PlayerState player, int baseSteps)
        {
            int steps = baseSteps;

            // 统计所有沉重层数
            int totalHeavy = 0;
            if (player.densityHeavyTurns > 0) totalHeavy++;  // 密度-沉重（3回合）
            totalHeavy += player.heavyLayers;                  // 电阻率等永久沉重

            // 统计所有轻盈层数
            int totalLight = 0;
            if (player.densityLightTurns > 0) totalLight++;    // 密度-轻盈（3回合）
            totalLight += player.lightLayers;                   // 电场强度等永久轻盈
            if (player.phaseState == PhaseState.Gas) totalLight++; // 气态轻盈

            // 规则书：沉重和轻盈相互抵消
            int netEffect = totalHeavy - totalLight;

            if (netEffect > 0)
            {
                // 净沉重：只结算一次（步数减半，向下取整）
                steps = steps / 2;
                Debug.Log($"[步数] 沉重（{totalHeavy}层沉重-{totalLight}层轻盈）：步数减半为{steps}");
            }
            else if (netEffect < 0)
            {
                // 净轻盈：只结算一次（步数翻倍）
                steps = steps * 2;
                Debug.Log($"[步数] 轻盈（{totalLight}层轻盈-{totalHeavy}层沉重）：步数翻倍为{steps}");
            }
            else if (totalHeavy > 0)
            {
                Debug.Log($"[步数] 沉重与轻盈互相抵消（各{totalHeavy}层）");
            }

            return steps;
        }

        /// <summary>
        /// 检查是否应该获得额外回合（电位连续行动）
        /// </summary>
        public bool HasExtraTurn(PlayerState player)
        {
            if (player.potentialExtraTurns > 0)
            {
                Debug.Log($"[电位] {player.playerName} 电位连续行动（剩余{player.potentialExtraTurns}回合）");
                return true;
            }
            return false;
        }

        /// <summary>获取容器卡的最大容量</summary>
        public static int GetContainerCapacity(PhysicsCardId cardId)
        {
            switch (cardId)
            {
                case PhysicsCardId.Area: return 2;   // 面积容器至多2mol
                case PhysicsCardId.Volume: return 5; // 体积容器至多5mol
                default: return 0;
            }
        }

        /// <summary>
        /// 处理玩家经过某个格子时的建筑效果
        /// </summary>
        public void OnPassTile(PlayerState player, int tileIndex,
            Board.BoardManager board, List<PlayerState> allPlayers)
        {
            var tile = board.GetTile(tileIndex);
            if (tile == null) return;

            // 气态：不触发地块效果
            if (player.phaseState == PhaseState.Gas)
                return;

            // 检查建筑效果
            foreach (var building in tile.buildings)
            {
                // 建筑不影响其所有者自己
                if (building.ownerIndex == player.playerIndex) continue;

                var owner = allPlayers.FirstOrDefault(p => p.playerIndex == building.ownerIndex);
                if (owner == null) continue;

                switch (building.type)
                {
                    case BuildingType.Laboratory:
                        // 实验室：经过者向拥有者上交1mol（不足由银行垫付）
                        player.mol = Math.Max(0, player.mol - 1);
                        owner.mol += 1; // 拥有者始终获得全额
                        Debug.Log($"[建筑] {player.playerName} 向{owner.playerName}的实验室上交1mol");
                        break;

                    case BuildingType.ResearchInstitute:
                        // 研究所：经过者向拥有者上交2mol（不足由银行垫付）
                        player.mol = Math.Max(0, player.mol - 2);
                        owner.mol += 2; // 拥有者始终获得全额
                        Debug.Log($"[建筑] {player.playerName} 向{owner.playerName}的研究所上交2mol");
                        break;

                    case BuildingType.LargeCollider:
                        // 大型对撞机：经过者向拥有者上交5mol（不足由银行垫付）
                        player.mol = Math.Max(0, player.mol - 5);
                        owner.mol += 5; // 拥有者始终获得全额
                        Debug.Log($"[建筑] {player.playerName} 向{owner.playerName}的大型对撞机上交5mol");
                        break;

                    case BuildingType.Observatory:
                        // 天文台：经过者向拥有者上交一张他拥有而拥有者未拥有的物理量牌
                        var cardToGive = player.handCards.FirstOrDefault(c =>
                            !c.isUsed && !owner.HasCard(c.cardId));
                        if (cardToGive != null)
                        {
                            player.RemoveCard(cardToGive);
                            owner.GiveCard(cardToGive.cardId);
                            Debug.Log($"[建筑] {player.playerName} 向{owner.playerName}的天文台上交{CardDatabase.Get(cardToGive.cardId)?.nameZH}");
                        }
                        break;
                }
            }

            // 检查路障
            if (tile.hasRoadblock && player.emShieldTurns <= 0)
            {
                player.roadblockSkipTurns = 1; // 统一使用roadblockSkipTurns，避免双重处理
                Debug.Log($"[路障] {player.playerName} 被路障阻挡在格子{tileIndex}");
            }

            // 检查电磁铁
            foreach (var other in allPlayers)
            {
                if (other.playerIndex != player.playerIndex &&
                    other.electromagnetTurns > 0 &&
                    other.electromagnetPosition == tileIndex)
                {
                    // 被电磁铁阻挡，停在电磁铁前一格或后一格
                    Debug.Log($"[电磁铁] {player.playerName} 被{other.playerName}的电磁铁阻挡");
                    player.roadblockSkipTurns = 1; // 统一使用roadblockSkipTurns
                    break;
                }
            }
        }
    }
}
