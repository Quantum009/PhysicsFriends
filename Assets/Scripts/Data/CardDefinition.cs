// ============================================================
// CardDefinition.cs — 卡牌定义：所有物理量牌的静态数据
// 包含量纲、效果类型、描述等
// ============================================================
using System;
using System.Collections.Generic;
using PhysicsFriends.Core;

namespace PhysicsFriends.Data
{
    /// <summary>物理量卡牌的唯一标识</summary>
    public enum PhysicsCardId
    {
        // === 基本物理量（6种，开局各发1张） ===
        Time,               // 时间 [s]
        Length,             // 长度 [m]
        Mass,               // 质量 [kg]
        Current,            // 电流 [A]
        Temperature,        // 温度 [K]
        LuminousIntensity,  // 光照强度 [cd]

        // === 非基本物理量 - 力学量 ===
        Area,               // 面积 [m²]
        Volume,             // 体积 [m³]
        Velocity,           // 速度 [m·s⁻¹]
        Momentum,           // 动量 [m·s⁻¹·kg]
        Pressure,           // 压强 [m⁻¹·s⁻²·kg]
        Power,              // 功率 [m²·s⁻³·kg]
        Density,            // 密度 [m⁻³·kg]
        Acceleration,       // 加速度 [m·s⁻²]
        Force,              // 力 [m·s⁻²·kg]
        Work,               // 功 [m²·s⁻²·kg]（与能量同量纲）
        Energy,             // 能量 [m²·s⁻²·kg]（与功同量纲）
        Torque,             // 力矩 [m²·s⁻²·kg]（与能量同量纲，但效果不同）
        MomentOfInertia,    // 转动惯量 [m²·kg]
        AngularMomentum,    // 角动量 [m²·s⁻¹·kg]
        SpringConstant,     // 劲度系数 [s⁻²·kg]

        // === 非基本物理量 - 电磁学量 ===
        Voltage,            // 电压 [m²·s⁻³·kg·A⁻¹]
        Resistance,         // 电阻 [m²·s⁻³·kg·A⁻²]
        Resistivity,        // 电阻率 [m³·s⁻³·kg·A⁻²]（sec=-3）
        Capacitance,        // 电容 [m⁻²·s⁴·kg⁻¹·A²]
        ElectricPotential,  // 电势 [m²·s⁻³·kg·A⁻¹]（与电压同量纲，效果不同）
        ElectricField,      // 电场强度 [m·s⁻³·kg·A⁻¹]
        MagneticField,      // 磁感应强度 [s⁻²·kg·A⁻¹]
        MagneticFlux,       // 磁通量 [m²·s⁻²·kg·A⁻¹]
        Charge,             // 电荷 [s·A]
        DisplacementVector, // 电位移矢量 [m⁻²·s·A]
        DisplacementFlux,   // 电位移通量 [s·A]

        // === 非基本物理量 - 热学量 ===
        SpecificHeat,       // 比热容 [m²·s⁻²·K⁻¹]
        CalorificValue,     // 热值(固体) [m²·s⁻²]
        Heat,               // 热量 [m²·s⁻²·kg]（与能量同量纲）
        Entropy,            // 熵 [m²·s⁻²·kg·K⁻¹]

        // === 非基本物理量 - 特殊量 ===
        PlanckConstant,     // 普朗克常量 [m²·s⁻¹·kg]
        Frequency,          // 频率 [s⁻¹]（无效果，仅用于合成中间产物）
        AngularVelocity,    // 角速度 [s⁻¹]（无效果，仅用于合成中间产物）
    }

    /// <summary>
    /// 物理量卡牌的静态定义数据
    /// 每张卡牌有唯一的量纲、效果类型和描述
    /// </summary>
    [Serializable]
    public class CardDefinition
    {
        public PhysicsCardId id;        // 卡牌唯一标识
        public string nameZH;           // 中文名称
        public string nameEN;           // 英文名称
        public Dimension dimension;     // 量纲
        public CardEffectType effectType; // 效果类型（被动/主动/抉择/无）
        public PhysicsBranch branch;    // 学科分类
        public int totalCount;          // 游戏中的总数量
        public int startCount;          // 开局时每人获得的数量
        public string effectDescription; // 效果描述

        public CardDefinition(PhysicsCardId id, string nameZH, string nameEN,
            Dimension dim, CardEffectType effect, PhysicsBranch branch,
            int total, int start, string desc)
        {
            this.id = id;
            this.nameZH = nameZH;
            this.nameEN = nameEN;
            this.dimension = dim;
            this.effectType = effect;
            this.branch = branch;
            this.totalCount = total;
            this.startCount = start;
            this.effectDescription = desc;
        }
    }

    /// <summary>
    /// 卡牌数据库：存储所有物理量卡牌的静态定义
    /// 游戏初始化时加载，运行时只读查询
    /// </summary>
    public static class CardDatabase
    {
        // 所有卡牌定义的字典，按ID索引
        private static Dictionary<PhysicsCardId, CardDefinition> _definitions;

        // 按量纲索引的反查表：给定量纲找到可能的卡牌（合成用）
        private static Dictionary<Dimension, List<PhysicsCardId>> _dimensionLookup;

        /// <summary>初始化卡牌数据库</summary>
        public static void Initialize()
        {
            _definitions = new Dictionary<PhysicsCardId, CardDefinition>();   // 创建定义字典
            _dimensionLookup = new Dictionary<Dimension, List<PhysicsCardId>>(); // 创建量纲查找表

            // ====== 基本物理量牌（6种） ======
            Register(new CardDefinition(
                PhysicsCardId.Time, "时间", "Time",
                new Dimension(0, 1, 0, 0, 0, 0),       // [s]
                CardEffectType.None, PhysicsBranch.Basic,
                16, 1, "无效果"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Length, "长度", "Length",
                new Dimension(1, 0, 0, 0, 0, 0),       // [m]
                CardEffectType.None, PhysicsBranch.Basic,
                16, 1, "无效果"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Mass, "质量", "Mass",
                new Dimension(0, 0, 1, 0, 0, 0),       // [kg]
                CardEffectType.None, PhysicsBranch.Basic,
                16, 1, "无效果"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Current, "电流", "Current",
                new Dimension(0, 0, 0, 0, 1, 0),       // [A]
                CardEffectType.Passive, PhysicsBranch.Basic,
                32, 1, "被动：每经过一次起点，获得1mol"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Temperature, "温度", "Temperature",
                new Dimension(0, 0, 0, 1, 0, 0),       // [K]
                CardEffectType.None, PhysicsBranch.Basic,
                16, 1, "无效果"
            ));

            Register(new CardDefinition(
                PhysicsCardId.LuminousIntensity, "光照强度", "Luminous Intensity",
                new Dimension(0, 0, 0, 0, 0, 1),       // [cd]
                CardEffectType.Passive, PhysicsBranch.Optics,
                16, 1, "被动：为所有\"受光强影响\"的效果数值+1"
            ));

            // ====== 非基本物理量 - 力学量 ======
            Register(new CardDefinition(
                PhysicsCardId.Area, "面积", "Area",
                new Dimension(2, 0, 0, 0, 0, 0),       // [m²]
                CardEffectType.Passive, PhysicsBranch.Mechanics,
                8, 0, "被动：一个储存至多2mol的容器。每经过一次起点，容器中mol翻倍"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Volume, "体积", "Volume",
                new Dimension(3, 0, 0, 0, 0, 0),       // [m³]
                CardEffectType.Passive, PhysicsBranch.Mechanics,
                8, 0, "被动：一个储存至多5mol的容器。每经过一次起点，容器中mol翻倍"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Velocity, "速度", "Velocity",
                new Dimension(1, -1, 0, 0, 0, 0),      // [m·s⁻¹]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：可以增加一回合行动"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Momentum, "动量", "Momentum",
                new Dimension(1, -1, 1, 0, 0, 0),      // [m·s⁻¹·kg]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：与其他玩家位于同一格时，将其撞晕3回合"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Pressure, "压强", "Pressure",
                new Dimension(-1, -2, 1, 0, 0, 0),     // [m⁻¹·s⁻²·kg]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：压在一张任意的牌（物质的量除外）上，使其失效"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Power, "功率", "Power",
                new Dimension(2, -3, 1, 0, 0, 0),      // [m²·s⁻³·kg]
                CardEffectType.Passive, PhysicsBranch.Mechanics,
                8, 0, "被动：每经过一次起点，获得1张能量"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Density, "密度", "Density",
                new Dimension(-3, 0, 1, 0, 0, 0),      // [m⁻³·kg]
                CardEffectType.Choice, PhysicsBranch.Mechanics,
                8, 0, "抉择：使一个角色获得3回合沉重；或者3回合轻盈"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Acceleration, "加速度", "Acceleration",
                new Dimension(1, -2, 0, 0, 0, 0),      // [m·s⁻²]
                CardEffectType.Passive, PhysicsBranch.Mechanics,
                8, 0, "被动：可以在自己投出骰子点数的基础上+1/-1"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Force, "力", "Force",
                new Dimension(1, -2, 1, 0, 0, 0),      // [m·s⁻²·kg]
                CardEffectType.Passive, PhysicsBranch.Mechanics,
                8, 0, "被动：每轮限一次，可以在自己或他人投出骰子点数的基础上+1/-1"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Work, "功", "Work",
                new Dimension(2, -2, 1, 0, 0, 0),      // [m²·s⁻²·kg]
                CardEffectType.Choice, PhysicsBranch.Mechanics,
                8, 0, "抉择：兑换6mol(受光强影响)；或者任选一张基本物理量牌"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Energy, "能量", "Energy",
                new Dimension(2, -2, 1, 0, 0, 0),      // [m²·s⁻²·kg]（与功同量纲）
                CardEffectType.Choice, PhysicsBranch.Mechanics,
                8, 0, "抉择：兑换6mol(受光强影响)；或者任选一张基本物理量牌"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Torque, "力矩", "Torque",
                new Dimension(2, -2, 1, 0, 0, 0),      // [m²·s⁻²·kg]（与能量同量纲，效果不同）
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：选定一个方向，所有玩家给下一位玩家一张手牌。你可以选择想要的卡牌"
            ));

            Register(new CardDefinition(
                PhysicsCardId.MomentOfInertia, "转动惯量", "Moment of Inertia",
                new Dimension(2, 0, 1, 0, 0, 0),       // [m²·kg]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：调转自己前进的方向"
            ));

            Register(new CardDefinition(
                PhysicsCardId.AngularMomentum, "角动量", "Angular Momentum",
                new Dimension(2, -1, 1, 0, 0, 0),      // [m²·s⁻¹·kg]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：调转自己或他人前进的方向"
            ));

            Register(new CardDefinition(
                PhysicsCardId.SpringConstant, "劲度系数", "Spring Constant",
                new Dimension(0, -2, 1, 0, 0, 0),      // [s⁻²·kg]
                CardEffectType.Active, PhysicsBranch.Mechanics,
                8, 0, "主动：若骰子结果>4则=4，若<3则=3"
            ));

            // ====== 非基本物理量 - 电磁学量 ======
            Register(new CardDefinition(
                PhysicsCardId.Voltage, "电压", "Voltage",
                new Dimension(2, -3, 1, 0, -1, 0),     // [m²·s⁻³·kg·A⁻¹]
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：每经过一次起点，获得1张电流（受光强影响）"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Resistance, "电阻", "Resistance",
                new Dimension(2, -3, 1, 0, -2, 0),     // [m²·s⁻³·kg·A⁻²]
                CardEffectType.Active, PhysicsBranch.Electromagnetics,
                8, 0, "主动：释放1个拦路的路障（受光强影响）"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Resistivity, "电阻率", "Resistivity",
                new Dimension(3, -3, 1, 0, -2, 0),     // [m³·s⁻³·kg·A⁻²]（sec=-3）
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：选择一名玩家，使其获得沉重"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Capacitance, "电容", "Capacitance",
                new Dimension(-2, 4, -1, 0, 2, 0),     // [m⁻²·s⁴·kg⁻¹·A²]
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：储存一张电学量的容器。每经过起点，容器中卡牌翻倍"
            ));

            Register(new CardDefinition(
                PhysicsCardId.ElectricPotential, "电势", "Electric Potential",
                new Dimension(2, -3, 1, 0, -1, 0),     // [m²·s⁻³·kg·A⁻¹]（与电压同量纲）
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：经过起点后跳过1回合，随后连续行动3回合"
            ));

            Register(new CardDefinition(
                PhysicsCardId.ElectricField, "电场强度", "Electric Field",
                new Dimension(1, -3, 1, 0, -1, 0),     // [m·s⁻³·kg·A⁻¹]
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：选择一名玩家，使其获得轻盈（行动距离为投出骰子的两倍）"
            ));

            Register(new CardDefinition(
                PhysicsCardId.MagneticField, "磁感应强度", "Magnetic Field",
                new Dimension(0, -2, 1, 0, -1, 0),     // [s⁻²·kg·A⁻¹]
                CardEffectType.Passive, PhysicsBranch.Electromagnetics,
                8, 0, "被动：选择方向，沿向+1mol，逆向-1mol"
            ));

            Register(new CardDefinition(
                PhysicsCardId.MagneticFlux, "磁通量", "Magnetic Flux",
                new Dimension(2, -2, 1, 0, -1, 0),     // [m²·s⁻²·kg·A⁻¹]
                CardEffectType.Active, PhysicsBranch.Electromagnetics,
                8, 0, "放置磁通量面，计数达到玩家数×2时获得20mol"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Charge, "电荷", "Charge",
                new Dimension(0, 1, 0, 0, 1, 0),       // [s·A]
                CardEffectType.Active, PhysicsBranch.Electromagnetics,
                8, 0, "主动：激活后经过起点获得两张电流"
            ));

            Register(new CardDefinition(
                PhysicsCardId.DisplacementVector, "电位移矢量", "Displacement Vector",
                new Dimension(-2, 1, 0, 0, 1, 0),      // [m⁻²·s·A]
                CardEffectType.None, PhysicsBranch.Electromagnetics,
                4, 0, "发现位移电流的第一步"
            ));

            Register(new CardDefinition(
                PhysicsCardId.DisplacementFlux, "电位移通量", "Displacement Flux",
                new Dimension(0, 1, 0, 0, 1, 0),       // [s·A]（与电荷同量纲）
                CardEffectType.None, PhysicsBranch.Electromagnetics,
                4, 0, "发现位移电流的第二步"
            ));

            // ====== 非基本物理量 - 热学量 ======
            Register(new CardDefinition(
                PhysicsCardId.SpecificHeat, "比热容", "Specific Heat",
                new Dimension(2, -2, 0, -1, 0, 0),     // [m²·s⁻²·K⁻¹]
                CardEffectType.Choice, PhysicsBranch.Thermodynamics,
                8, 0, "抉择：掠夺mol更多的玩家4mol；或给予mol更少的玩家4mol并任选两张基本物理量"
            ));

            Register(new CardDefinition(
                PhysicsCardId.CalorificValue, "热值", "Calorific Value",
                new Dimension(2, -2, 0, 0, 0, 0),      // [m²·s⁻²]
                CardEffectType.Active, PhysicsBranch.Thermodynamics,
                8, 0, "主动：燃烧所有物理量牌，每一张给予2mol"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Heat, "热量", "Heat",
                new Dimension(2, -2, 1, 0, 0, 0),      // [m²·s⁻²·kg]（与能量同量纲）
                CardEffectType.Choice, PhysicsBranch.Thermodynamics,
                8, 0, "抉择：兑换6mol(受光强影响)；或者任选一张基本物理量牌"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Entropy, "熵", "Entropy",
                new Dimension(2, -2, 1, -1, 0, 0),     // [m²·s⁻²·kg·K⁻¹]
                CardEffectType.Active, PhysicsBranch.Thermodynamics,
                8, 0, "主动：连续抽取5次事件牌，可从中弃掉最多一张"
            ));

            // ====== 特殊量 ======
            Register(new CardDefinition(
                PhysicsCardId.PlanckConstant, "普朗克常量", "Planck Constant",
                new Dimension(2, -1, 1, 0, 0, 0),      // [m²·s⁻¹·kg]
                CardEffectType.Passive, PhysicsBranch.Special,
                8, 0, "被动：光照强度提供的增益翻倍"
            ));

            Register(new CardDefinition(
                PhysicsCardId.Frequency, "频率", "Frequency",
                new Dimension(0, -1, 0, 0, 0, 0),      // [s⁻¹]
                CardEffectType.None, PhysicsBranch.Special,
                0, 0, "无效果（合成中间产物）"
            ));

            Register(new CardDefinition(
                PhysicsCardId.AngularVelocity, "角速度", "Angular Velocity",
                new Dimension(0, -1, 0, 0, 0, 0),      // [s⁻¹]（与频率同量纲）
                CardEffectType.None, PhysicsBranch.Special,
                0, 0, "无效果（合成中间产物）"
            ));

            // 初始化完成后构建量纲查找表
            BuildDimensionLookup();
        }

        /// <summary>注册一张卡牌定义到数据库</summary>
        private static void Register(CardDefinition def)
        {
            _definitions[def.id] = def; // 将定义存入字典
        }

        /// <summary>构建量纲→卡牌ID的反查表</summary>
        private static void BuildDimensionLookup()
        {
            foreach (var kvp in _definitions) // 遍历所有卡牌定义
            {
                var dim = kvp.Value.dimension; // 获取该卡牌的量纲
                if (!_dimensionLookup.ContainsKey(dim)) // 如果该量纲尚未记录
                    _dimensionLookup[dim] = new List<PhysicsCardId>(); // 创建新列表
                _dimensionLookup[dim].Add(kvp.Key); // 将卡牌ID加入该量纲的列表
            }
        }

        /// <summary>根据ID获取卡牌定义</summary>
        public static CardDefinition Get(PhysicsCardId id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>根据量纲查找所有匹配的卡牌ID（用于合成）</summary>
        public static List<PhysicsCardId> FindByDimension(Dimension dim)
        {
            return _dimensionLookup.TryGetValue(dim, out var list)
                ? list       // 找到匹配的卡牌列表
                : new List<PhysicsCardId>(); // 没有匹配返回空列表
        }

        /// <summary>获取所有卡牌定义</summary>
        public static IReadOnlyDictionary<PhysicsCardId, CardDefinition> GetAll()
        {
            return _definitions;
        }

        /// <summary>判断一个卡牌ID是否为基本物理量</summary>
        public static bool IsBasicQuantity(PhysicsCardId id)
        {
            // 基本物理量共6种（不含物质的量）
            return id == PhysicsCardId.Time ||
                   id == PhysicsCardId.Length ||
                   id == PhysicsCardId.Mass ||
                   id == PhysicsCardId.Current ||
                   id == PhysicsCardId.Temperature ||
                   id == PhysicsCardId.LuminousIntensity;
        }

        /// <summary>判断一个卡牌是否属于电学量</summary>
        public static bool IsElectrical(PhysicsCardId id)
        {
            var def = Get(id);
            if (def == null) return false;
            // 电学量 = 电磁学量中非磁学的部分
            return def.branch == PhysicsBranch.Electromagnetics &&
                   id != PhysicsCardId.MagneticField &&
                   id != PhysicsCardId.MagneticFlux;
        }

        /// <summary>判断一个卡牌是否属于磁学量</summary>
        public static bool IsMagnetic(PhysicsCardId id)
        {
            return id == PhysicsCardId.MagneticField ||
                   id == PhysicsCardId.MagneticFlux;
        }

        /// <summary>判断一个卡牌是否具有能量量纲（功/能量/热量/力矩）</summary>
        public static bool IsEnergyDimension(PhysicsCardId id)
        {
            return id == PhysicsCardId.Work ||
                   id == PhysicsCardId.Energy ||
                   id == PhysicsCardId.Heat ||
                   id == PhysicsCardId.Torque;
        }
    }
}