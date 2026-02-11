// ============================================================
// AchievementData.cs — 创举牌定义：全部15张创举牌
// 分为三个时代，每个时代5张
// ============================================================
using System;
using System.Collections.Generic;
using PhysicsFriends.Core;

namespace PhysicsFriends.Data
{
    /// <summary>创举牌ID枚举（1~15）</summary>
    public enum AchievementId
    {
        // === 第一时代：自然哲学时期 ===
        LeverPrinciple = 1,         // 创举1：杠杆原理
        BuoyancyLaw = 2,            // 创举2：浮力定律
        Atomism = 3,                 // 创举3：原子论
        GeomagneticDeclination = 4,  // 创举4：地磁偏角
        PinholeImaging = 5,          // 创举5：小孔成像

        // === 第二时代：经典物理学时期 ===
        KeplerLaws = 6,             // 创举6：开普勒定律
        NewtonMechanics = 7,        // 创举7：牛顿力学
        DisplacementCurrent = 8,    // 创举8：位移电流
        EnergyConservation = 9,     // 创举9：能量守恒定律
        ThermodynamicLaws = 10,     // 创举10：热力学定律

        // === 第三时代：现代物理学时期 ===
        BigBang = 11,               // 创举11：宇宙大爆炸
        StandardModel = 12,         // 创举12：粒子标准模型
        Relativity = 13,            // 创举13：相对论
        QuantumMechanics = 14,      // 创举14：量子力学
        Superconductivity = 15      // 创举15：超导现象
    }

    /// <summary>创举的检查时机</summary>
    public enum AchievementCheckTiming
    {
        TurnStart,      // 回合开始时检查（原子论、小孔成像、粒子标准模型需要掷骰）
        Instant,        // 即时检查（满足条件立即完成：杠杆原理、地磁偏角等持有型）
        TurnEnd,        // 回合结束时检查（牛顿力学的匀速运动、相对论的连续6格）
        EventTriggered  // 事件触发时检查（宇宙大爆炸、量子力学、超导现象）
    }

    /// <summary>创举牌的静态定义</summary>
    [Serializable]
    public class AchievementDefinition
    {
        public AchievementId id;            // 创举唯一ID
        public string nameZH;               // 中文名称
        public Era era;                     // 所属时代
        public int points;                  // 完成后获得的创举点数
        public string goalDescription;      // 目标描述
        public string rewardDescription;    // 奖励描述
        public AchievementCheckTiming timing; // 检查时机

        public AchievementDefinition(AchievementId id, string name, Era era,
            int pts, string goal, string reward, AchievementCheckTiming timing)
        {
            this.id = id;
            this.nameZH = name;
            this.era = era;
            this.points = pts;
            this.goalDescription = goal;
            this.rewardDescription = reward;
            this.timing = timing;
        }
    }

    /// <summary>创举数据库</summary>
    public static class AchievementDatabase
    {
        private static Dictionary<AchievementId, AchievementDefinition> _definitions;

        /// <summary>初始化所有创举定义</summary>
        public static void Initialize()
        {
            _definitions = new Dictionary<AchievementId, AchievementDefinition>();

            // ====== 第一时代：自然哲学时期 ======
            Reg(AchievementId.LeverPrinciple, "杠杆原理",
                Era.NaturalPhilosophy, 1,
                "同时拥有两张力、两张长度",
                "获得两张力矩。创举+1",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.BuoyancyLaw, "浮力定律",
                Era.NaturalPhilosophy, 1,
                "利用密度、加速度、长度合成力",
                "获得金皇冠（免于一次不幸事件）。创举+1",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.Atomism, "原子论",
                Era.NaturalPhilosophy, 2,
                "回合开始时≥15mol(标准)，掷骰=6则完成",
                "获得1mol。创举+2",
                AchievementCheckTiming.TurnStart);

            Reg(AchievementId.GeomagneticDeclination, "地磁偏角",
                Era.NaturalPhilosophy, 1,
                "同时拥有两张磁感应强度",
                "获得《梦溪笔谈》（磁感应强度增益/惩罚翻倍）。创举+1",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.PinholeImaging, "小孔成像",
                Era.NaturalPhilosophy, 1,
                "回合开始时≥2张光照强度，掷骰=1则完成",
                "获得《墨经》（每过起点+1光照强度）。创举+1",
                AchievementCheckTiming.TurnStart);

            // ====== 第二时代：经典物理学时期 ======
            Reg(AchievementId.KeplerLaws, "开普勒定律",
                Era.ClassicalPhysics, 2,
                "同时拥有一张角动量、两张时间、三张长度",
                "在同色地块建两个天文台。创举+2",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.NewtonMechanics, "牛顿力学",
                Era.ClassicalPhysics, 3,
                "①连续3回合匀速运动 ②同时有质量+加速度+力 ③用力抵消他人的力",
                "获得《自然哲学的数学原理》（力改为+2/+1/-1/-2）。创举+3",
                AchievementCheckTiming.TurnEnd);

            Reg(AchievementId.DisplacementCurrent, "位移电流",
                Era.ClassicalPhysics, 2,
                "依次合成电位移矢量→电位移通量→用其合成电流",
                "选一张非电流电学量和一张磁学量。创举+2",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.EnergyConservation, "能量守恒定律",
                Era.ClassicalPhysics, 3,
                "同时拥有三张能量量纲的牌（功/能量/热量）",
                "获得一张能量。创举+3",
                AchievementCheckTiming.Instant);

            Reg(AchievementId.ThermodynamicLaws, "热力学定律",
                Era.ClassicalPhysics, 2,
                "同时拥有温度+热量+熵各一张",
                "获得热机（两张温度→一张能量）。创举+2",
                AchievementCheckTiming.Instant);

            // ====== 第三时代：现代物理学时期 ======
            Reg(AchievementId.BigBang, "宇宙大爆炸",
                Era.ModernPhysics, 3,
                "在\"奇点\"事件中失去一切",
                "创举+3",
                AchievementCheckTiming.EventTriggered);

            Reg(AchievementId.StandardModel, "粒子标准模型",
                Era.ModernPhysics, 2,
                "回合开始时≥40mol(标准)，掷骰=6则完成",
                "创举+2",
                AchievementCheckTiming.TurnStart);

            Reg(AchievementId.Relativity, "相对论",
                Era.ModernPhysics, 2,
                "连续3回合以6格或以上速度运动",
                "创举+2",
                AchievementCheckTiming.TurnEnd);

            Reg(AchievementId.QuantumMechanics, "量子力学",
                Era.ModernPhysics, 2,
                "触发事件\"量子隧穿\"",
                "创举+2",
                AchievementCheckTiming.EventTriggered);

            Reg(AchievementId.Superconductivity, "超导现象",
                Era.ModernPhysics, 2,
                "触发事件\"超导\"",
                "创举+2",
                AchievementCheckTiming.EventTriggered);
        }

        // 注册辅助方法
        private static void Reg(AchievementId id, string name, Era era,
            int pts, string goal, string reward, AchievementCheckTiming timing)
        {
            _definitions[id] = new AchievementDefinition(id, name, era, pts, goal, reward, timing);
        }

        /// <summary>根据ID获取创举定义</summary>
        public static AchievementDefinition Get(AchievementId id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>获取指定时代的所有创举ID</summary>
        public static List<AchievementId> GetByEra(Era era)
        {
            var result = new List<AchievementId>(); // 存储结果
            foreach (var kvp in _definitions)
            {
                if (kvp.Value.era == era) // 如果属于目标时代
                    result.Add(kvp.Key);   // 加入结果列表
            }
            return result;
        }
    }
}
