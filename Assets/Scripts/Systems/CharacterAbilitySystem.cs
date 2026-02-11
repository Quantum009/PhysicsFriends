// ============================================================
// CharacterAbilitySystem.cs — 角色任务与能力系统
// 4个角色：牛顿、麦克斯韦、爱因斯坦、薛定谔
// 每个角色有专属任务，完成后解锁永久能力
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
    public class CharacterAbilitySystem
    {
        private readonly IUIProvider _ui;

        public CharacterAbilitySystem(IUIProvider ui)
        {
            _ui = ui;
        }

        // ================================================================
        // 任务检查（每回合结束时调用）
        // ================================================================

        /// <summary>检查角色任务是否完成</summary>
        public void CheckCharacterTask(PlayerState player)
        {
            if (player.characterTaskCompleted) return; // 已完成

            bool completed = player.character switch
            {
                Character.Newton     => CheckNewtonTask(player),
                Character.Maxwell    => CheckMaxwellTask(player),
                Character.Einstein   => CheckEinsteinTask(player),
                Character.Schrodinger => CheckSchrodingerTask(player),
                _ => false
            };

            if (completed)
            {
                player.characterTaskCompleted = true;
                _ui.SendNotification(new GameNotification(NotificationType.Achievement,
                    $"角色任务完成！{GetCharacterName(player.character)}能力已解锁", player));
                Debug.Log($"[Character] Player {player.playerIndex} ({player.character}) task completed!");
            }
        }

        /// <summary>
        /// 牛顿任务：牛顿力学三段目标
        /// 1. 连续3回合匀速运动（每回合移动格数相同）
        /// 2. 拥有力牌时使用力牌修正自己的骰子
        /// 3. 同时持有力、质量、加速度三张牌
        /// </summary>
        private bool CheckNewtonTask(PlayerState player)
        {
            // 三段目标：
            // 第1段：连续匀速运动3回合 → 由 recentMoveSteps 追踪
            bool stage1 = player.newtonStage1Done;

            // 第2段：使用过力牌修正自己 → 由骰子修正阶段设置
            bool stage2 = player.newtonStage2Done;

            // 第3段：同时持有力+质量+加速度
            bool stage3 = player.handCards.Any(c => c.cardId == PhysicsCardId.Force && !c.isUsed) &&
                          player.handCards.Any(c => c.cardId == PhysicsCardId.Mass && !c.isUsed) &&
                          player.handCards.Any(c => c.cardId == PhysicsCardId.Acceleration && !c.isUsed);

            return stage1 && stage2 && stage3;
        }

        /// <summary>
        /// 麦克斯韦任务：同时持有电场强度、磁感应强度、电荷、电流 四张牌
        /// </summary>
        private bool CheckMaxwellTask(PlayerState player)
        {
            var requiredCards = new[]
            {
                PhysicsCardId.ElectricField,
                PhysicsCardId.MagneticFlux,
                PhysicsCardId.Charge,
                PhysicsCardId.Current
            };

            return requiredCards.All(id =>
                player.handCards.Any(c => c.cardId == id && !c.isUsed));
        }

        /// <summary>
        /// 爱因斯坦任务：连续3回合移动6格以上
        /// </summary>
        private bool CheckEinsteinTask(PlayerState player)
        {
            return player.consecutiveHighSpeedTurns >= 3;
        }

        /// <summary>
        /// 薛定谔任务：在同一回合内持有相反效果的状态
        /// （沉重+轻盈 或 同时有正负方向选择）
        /// 简化：拥有5张不同类别的物理量牌
        /// </summary>
        private bool CheckSchrodingerTask(PlayerState player)
        {
            var categories = player.handCards
                .Where(c => !c.isUsed)
                .Select(c => CardDatabase.Get(c.cardId)?.branch)
                .Where(cat => cat.HasValue)
                .Select(cat => cat.Value)
                .Distinct()
                .Count();

            return categories >= 5;
        }

        // ================================================================
        // 能力效果（已完成任务后的被动增益）
        // ================================================================

        /// <summary>牛顿能力：力牌修正量变为±2</summary>
        public int GetForceModifierBonus(PlayerState player)
        {
            if (player.character == Character.Newton && player.characterTaskCompleted)
                return 1; // +1 额外修正（即 ±1 → ±2）
            return 0;
        }

        /// <summary>麦克斯韦能力：经过起点时额外获得1张电流</summary>
        public void OnPassStartBonus(PlayerState player)
        {
            if (player.character == Character.Maxwell && player.characterTaskCompleted)
            {
                player.GiveCard(PhysicsCardId.Current);
                _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                    "麦克斯韦能力：获得1张电流", player));
            }
        }

        /// <summary>爱因斯坦能力：能量量纲牌（能量/功/热量）的mol奖励翻倍</summary>
        /// 此效果在 CardEffectProcessor.ProcessEnergyChoice 中处理

        /// <summary>薛定谔能力：投骰后可选择重投一次</summary>
        /// 此效果在 DiceSystem / TurnManagerAsync 的投骰阶段处理

        // ================================================================
        // 角色信息
        // ================================================================

        public static string GetCharacterName(Character c)
        {
            return c switch
            {
                Character.Newton     => "牛顿",
                Character.Maxwell    => "麦克斯韦",
                Character.Einstein   => "爱因斯坦",
                Character.Schrodinger => "薛定谔",
                _ => "未知"
            };
        }

        public static string GetTaskDescription(Character c)
        {
            return c switch
            {
                Character.Newton =>
                    "牛顿力学三段目标：\n" +
                    "① 连续3回合匀速运动\n" +
                    "② 使用力牌修正自己的骰子\n" +
                    "③ 同时持有力、质量、加速度",

                Character.Maxwell =>
                    "麦克斯韦方程组：\n" +
                    "同时持有电场强度、磁感应强度、电荷、电流",

                Character.Einstein =>
                    "狭义相对论：\n" +
                    "连续3回合移动6格以上",

                Character.Schrodinger =>
                    "量子叠加态：\n" +
                    "同时持有5种不同类别的物理量牌",

                _ => ""
            };
        }

        public static string GetAbilityDescription(Character c)
        {
            return c switch
            {
                Character.Newton     => "力牌修正量增强为 ±2",
                Character.Maxwell    => "经过起点额外获得1张电流",
                Character.Einstein   => "能量/功/热量的mol奖励翻倍",
                Character.Schrodinger => "投骰后可重投一次",
                _ => ""
            };
        }
    }
}
