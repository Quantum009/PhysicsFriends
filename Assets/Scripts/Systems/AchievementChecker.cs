// ============================================================
// AchievementChecker.cs — 创举检查器：验证15个创举的达成条件
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
    /// 创举检查器：在适当时机检查玩家是否达成各创举条件
    /// 有且仅有一个玩家可以完成创举。创举点数不可交易。
    /// </summary>
    public class AchievementChecker
    {
        private Era _currentEra;
        private GameMode _gameMode;
        private GameConfig _config;

        // 当前时代激活的创举列表（每时代从5张中抽2张，慢速3张）
        private List<AchievementId> _activeAchievements = new List<AchievementId>();

        // 全局已完成的创举（任何玩家完成后其他人不能再完成）
        private HashSet<AchievementId> _globalCompleted = new HashSet<AchievementId>();

        public AchievementChecker(GameMode mode, GameConfig config)
        {
            _gameMode = mode;
            _config = config;
            _currentEra = Era.NaturalPhilosophy;
        }

        public AchievementChecker(GameMode mode)
        {
            _gameMode = mode;
            _config = GameConfig.Create(mode);
            _currentEra = Era.NaturalPhilosophy;
        }

        public void SetEra(Era era) => _currentEra = era;
        public List<AchievementId> ActiveAchievements => _activeAchievements;
        public HashSet<AchievementId> GlobalCompleted => _globalCompleted;

        /// <summary>设置当前时代的激活创举</summary>
        public void SetActiveAchievements(List<AchievementId> active)
        {
            _activeAchievements = new List<AchievementId>(active);
        }

        // =============================================================
        // 主检查入口
        // =============================================================

        /// <summary>检查当前激活的创举</summary>
        public List<AchievementId> CheckAll(PlayerState player, List<PlayerState> allPlayers,
            DiceSystem dice, int lastDiceRoll)
        {
            var achieved = new List<AchievementId>();

            foreach (var achId in _activeAchievements)
            {
                if (_globalCompleted.Contains(achId)) continue;
                if (player.completedAchievements.Contains(achId)) continue;

                bool completed = false;
                var def = AchievementDatabase.Get(achId);
                if (def == null) continue;

                switch (achId)
                {
                    case AchievementId.LeverPrinciple:
                        completed = player.CountCards(PhysicsCardId.Force) >= 2 &&
                                    player.CountCards(PhysicsCardId.Length) >= 2;
                        break;
                    case AchievementId.BuoyancyLaw:
                        completed = player.buoyancySynthesisCompleted;
                        break;
                    case AchievementId.Atomism:
                        // 规则书：回合开始时掷骰=6（由Phase2独立掷骰检查）
                        completed = player.achievementDiceCheckPassed.Contains(AchievementId.Atomism);
                        break;
                    case AchievementId.GeomagneticDeclination:
                        completed = player.CountCards(PhysicsCardId.MagneticField) >= 2;
                        break;
                    case AchievementId.PinholeImaging:
                        // 规则书：回合开始时掷骰=1（由Phase2独立掷骰检查）
                        completed = player.achievementDiceCheckPassed.Contains(AchievementId.PinholeImaging);
                        break;
                    case AchievementId.KeplerLaws:
                        completed = player.CountCards(PhysicsCardId.AngularMomentum) >= 1 &&
                                    player.CountCards(PhysicsCardId.Time) >= 2 &&
                                    player.CountCards(PhysicsCardId.Length) >= 3;
                        break;
                    case AchievementId.NewtonMechanics:
                        UpdateNewtonSubtasks(player);
                        completed = player.newtonTask_Inertia &&
                                    player.newtonTask_Acceleration &&
                                    player.newtonTask_Reaction;
                        break;
                    case AchievementId.DisplacementCurrent:
                        completed = player.hasCompletedDisplacementVector &&
                                    player.hasCompletedDisplacementFlux &&
                                    player.displacementCurrentCompleted;
                        break;
                    case AchievementId.EnergyConservation:
                        completed = player.CountEnergyCards() >= 3;
                        break;
                    case AchievementId.ThermodynamicLaws:
                        completed = player.HasCard(PhysicsCardId.Temperature) &&
                                    player.HasCard(PhysicsCardId.Heat) &&
                                    player.HasCard(PhysicsCardId.Entropy);
                        break;
                    case AchievementId.StandardModel:
                        // 规则书：回合开始时掷骰=6（由Phase2独立掷骰检查）
                        completed = player.achievementDiceCheckPassed.Contains(AchievementId.StandardModel);
                        break;
                    case AchievementId.Relativity:
                        if (player.recentMoveSteps.Count >= 3)
                        {
                            var last3 = player.recentMoveSteps.Skip(
                                Math.Max(0, player.recentMoveSteps.Count - 3)).Take(3).ToList();
                            completed = last3.Count == 3 && last3.All(s => s >= 6);
                        }
                        break;
                    // BigBang, QuantumMechanics, Superconductivity are event-triggered
                }

                if (completed)
                {
                    player.completedAchievements.Add(achId);
                    _globalCompleted.Add(achId);
                    player.achievementPoints += def.points;
                    achieved.Add(achId);
                    Debug.Log($"[创举] {player.playerName} 达成创举：{def.nameZH} (+{def.points}分)");
                }
            }

            return achieved;
        }

        /// <summary>更新牛顿力学子任务进度</summary>
        private void UpdateNewtonSubtasks(PlayerState player)
        {
            // ①连续3回合匀速运动（实际运动格子数相等，被跳过的回合也计入）
            if (!player.newtonTask_Inertia && player.recentMoveSteps.Count >= 3)
            {
                var last3 = player.recentMoveSteps.Skip(
                    Math.Max(0, player.recentMoveSteps.Count - 3)).Take(3).ToList();
                if (last3.Count == 3 && last3[0] == last3[1] && last3[1] == last3[2])
                {
                    player.newtonTask_Inertia = true;
                    Debug.Log($"[创举] 牛顿力学①完成：惯性定律");
                }
            }
            // ②同时拥有质量+加速度+力
            if (!player.newtonTask_Acceleration &&
                player.HasCard(PhysicsCardId.Mass) &&
                player.HasCard(PhysicsCardId.Acceleration) &&
                player.HasCard(PhysicsCardId.Force))
            {
                player.newtonTask_Acceleration = true;
                Debug.Log($"[创举] 牛顿力学②完成：加速度定律");
            }
            // ③由力的修正链中标记 newtonTask_Reaction
        }

        // =============================================================
        // 事件触发型创举
        // =============================================================

        public bool CheckBigBang(PlayerState player)
        {
            if (_globalCompleted.Contains(AchievementId.BigBang)) return false;
            if (!_activeAchievements.Contains(AchievementId.BigBang)) return false;

            player.completedAchievements.Add(AchievementId.BigBang);
            _globalCompleted.Add(AchievementId.BigBang);
            var def = AchievementDatabase.Get(AchievementId.BigBang);
            player.achievementPoints += def?.points ?? 3;
            Debug.Log($"[创举] {player.playerName} 达成宇宙大爆炸 (+{def?.points}分)");
            return true;
        }

        public bool CheckQuantumMechanics(PlayerState player)
        {
            if (_globalCompleted.Contains(AchievementId.QuantumMechanics)) return false;
            if (!_activeAchievements.Contains(AchievementId.QuantumMechanics)) return false;

            player.completedAchievements.Add(AchievementId.QuantumMechanics);
            _globalCompleted.Add(AchievementId.QuantumMechanics);
            var def = AchievementDatabase.Get(AchievementId.QuantumMechanics);
            player.achievementPoints += def?.points ?? 2;
            Debug.Log($"[创举] {player.playerName} 达成量子力学 (+{def?.points}分)");
            return true;
        }

        public bool CheckSuperconductivity(PlayerState player)
        {
            if (_globalCompleted.Contains(AchievementId.Superconductivity)) return false;
            if (!_activeAchievements.Contains(AchievementId.Superconductivity)) return false;

            player.completedAchievements.Add(AchievementId.Superconductivity);
            _globalCompleted.Add(AchievementId.Superconductivity);
            var def = AchievementDatabase.Get(AchievementId.Superconductivity);
            player.achievementPoints += def?.points ?? 2;
            Debug.Log($"[创举] {player.playerName} 达成超导现象 (+{def?.points}分)");
            return true;
        }

        // =============================================================
        // 创举奖励发放（严格按规则书）
        // =============================================================

        public void GrantAchievementReward(AchievementId id, PlayerState player)
        {
            switch (id)
            {
                case AchievementId.LeverPrinciple:
                    player.GiveCard(PhysicsCardId.Torque);
                    player.GiveCard(PhysicsCardId.Torque);
                    Debug.Log("[创举奖励] 杠杆原理：获得2张力矩");
                    break;
                case AchievementId.BuoyancyLaw:
                    player.hasGoldenCrown = true;
                    Debug.Log("[创举奖励] 浮力定律：获得金皇冠");
                    break;
                case AchievementId.Atomism:
                    player.mol += 1;
                    Debug.Log("[创举奖励] 原子论：获得1mol");
                    break;
                case AchievementId.GeomagneticDeclination:
                    player.hasMengXiBiTan = true;
                    Debug.Log("[创举奖励] 地磁偏角：获得《梦溪笔谈》");
                    break;
                case AchievementId.PinholeImaging:
                    player.hasMoJing = true;
                    Debug.Log("[创举奖励] 小孔成像：获得《墨经》");
                    break;
                case AchievementId.KeplerLaws:
                    // 建造两个天文台（需UI选位置）
                    Debug.Log("[创举奖励] 开普勒定律：建造两个天文台");
                    break;
                case AchievementId.NewtonMechanics:
                    player.hasPrincipia = true;
                    Debug.Log("[创举奖励] 牛顿力学：获得《自然哲学的数学原理》");
                    break;
                case AchievementId.DisplacementCurrent:
                    // 选择一张非电流电学量和一张磁学量（需UI选择）
                    Debug.Log("[创举奖励] 位移电流：等待选择卡牌");
                    break;
                case AchievementId.EnergyConservation:
                    player.GiveCard(PhysicsCardId.Energy);
                    Debug.Log("[创举奖励] 能量守恒：获得1张能量");
                    break;
                case AchievementId.ThermodynamicLaws:
                    player.hasHeatEngine = true;
                    Debug.Log("[创举奖励] 热力学定律：获得热机");
                    break;
                case AchievementId.BigBang:
                case AchievementId.StandardModel:
                case AchievementId.Relativity:
                case AchievementId.QuantumMechanics:
                case AchievementId.Superconductivity:
                    Debug.Log($"[创举奖励] {AchievementDatabase.Get(id)?.nameZH}：仅获得创举点数");
                    break;
            }
        }
    }
}
