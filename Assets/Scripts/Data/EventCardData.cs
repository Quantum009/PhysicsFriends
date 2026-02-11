// ============================================================
// EventCardData.cs — 事件牌定义：全部26张事件牌的数据与效果
// ============================================================
using System;
using System.Collections.Generic;

namespace PhysicsFriends.Data
{
    /// <summary>事件牌ID枚举</summary>
    public enum EventCardId
    {
        Singularity = 1,            // 奇点：掷骰<5失去所有手牌
        HalfLife = 2,               // 半衰期：所有玩家mol减半
        Annihilation = 3,           // 湮灭：掷骰<5失去所有物质的量牌
        GrandUnification = 4,       // 大一统理论：获得所有基本物理量各一
        AbsoluteZero = 5,           // 绝对零度：跳过3回合
        Superconductor = 6,         // 超导：摧毁全场电阻/电阻率和路障
        TimeMachine = 7,            // 时间机器：本回合操作无效，回退到上一玩家结束时
        EMShield = 8,               // 电磁屏蔽：本回合和下回合无法使用电磁卡
        QuantumTunneling = 9,       // 量子隧穿：传送到任意位置
        FeynmanBet = 10,            // 费曼的赌注：猜奇偶，对+10mol，错-10mol
        MillikanOilDrop = 11,       // 密立根油滴实验：获得质量数量的电流(上限3)
        MichelsonMorley = 12,       // 迈克尔逊-莫雷：3回合步数固定为6
        NewtonApple = 13,           // 牛顿的苹果：牛顿专属获得质量+加速度
        EntropyIncrease = 14,       // 熵增：再抽两张事件牌
        EnergyConservation = 15,    // 能量守恒：所有玩家mol汇总并平均分配
        SchrodingerCat = 16,        // 薛定谔的猫：掷骰决定丢弃
        EinsteinMiracleYear = 17,   // 爱因斯坦奇迹年：爱因斯坦专属获得2张非基本物理量
        Collapse = 18,              // 坍缩：掷骰<5失去所有基本物理量牌
        BlackHole = 19,             // 黑洞：掷骰<5失去所有非基本物理量牌
        PhaseTransition = 20,       // 相变：选择固/液/气态
        ResearchFunding = 21,       // 基金项目：获得10mol
        AcademicPlagiarism = 22,    // 学术剽窃：掠夺他人一张物理量牌
        EMInduction = 23,           // 电磁感应：同时有电学量和磁学量获得20mol
        Wormhole = 24,              // 虫洞：传送到起点(不算经过)，mol减半
        FranckHertz = 25,           // 弗兰克-赫兹：连投3次骰子，递增/递减则+20mol
        NuclearReactor = 26         // 核反应堆：翻倍赌博机制
    }

    /// <summary>事件牌的静态定义</summary>
    [Serializable]
    public class EventCardDefinition
    {
        public EventCardId id;          // 事件牌唯一ID
        public string nameZH;           // 中文名称
        public string descriptionZH;    // 效果描述
        public bool isNegative;         // 是否为不利事件（用于"金皇冠"免疫判断）

        public EventCardDefinition(EventCardId id, string name, string desc, bool negative)
        {
            this.id = id;
            this.nameZH = name;
            this.descriptionZH = desc;
            this.isNegative = negative;
        }
    }

    /// <summary>事件牌数据库</summary>
    public static class EventCardDatabase
    {
        private static Dictionary<EventCardId, EventCardDefinition> _definitions;

        /// <summary>初始化所有事件牌定义</summary>
        public static void Initialize()
        {
            _definitions = new Dictionary<EventCardId, EventCardDefinition>();

            // 逐一注册26张事件牌
            Reg(EventCardId.Singularity, "奇点",
                "掷骰子，若小于5，你失去所有手牌", true);

            Reg(EventCardId.HalfLife, "半衰期",
                "所有玩家mol数减半（四舍五入）", true);

            Reg(EventCardId.Annihilation, "湮灭",
                "掷骰子，若小于5，你失去所有物质的量牌", true);

            Reg(EventCardId.GrandUnification, "大一统理论",
                "你获得所有基本物理量牌各一张", false);

            Reg(EventCardId.AbsoluteZero, "绝对零度",
                "跳过你接下来的3个回合", true);

            Reg(EventCardId.Superconductor, "超导",
                "摧毁全场所有\"电阻\"\"电阻率\"卡牌以及路障", false);

            Reg(EventCardId.TimeMachine, "时间机器",
                "本回合所有操作无效，回退到上一个玩家结束回合时", false);

            Reg(EventCardId.EMShield, "电磁屏蔽",
                "本回合与下回合，不能合成电磁卡，电磁卡失效", true);

            Reg(EventCardId.QuantumTunneling, "量子隧穿",
                "传送到任一位置（不算经过起点，不触发地块效果）", false);

            Reg(EventCardId.FeynmanBet, "费曼的赌注",
                "猜下一骰子奇偶。猜对+10mol，猜错-10mol", false);

            Reg(EventCardId.MillikanOilDrop, "密立根油滴实验",
                "获得等于\"质量\"卡牌数量的\"电流\"牌（至多3张）", false);

            Reg(EventCardId.MichelsonMorley, "迈克尔逊-莫雷实验",
                "接下来3回合移动步数固定为6（不受任何修正影响）", false);

            Reg(EventCardId.NewtonApple, "牛顿的苹果",
                "如果你是牛顿，获得一张\"质量\"和一张\"加速度\"", false);

            Reg(EventCardId.EntropyIncrease, "熵增",
                "你再抽两张事件牌", false);

            Reg(EventCardId.EnergyConservation, "能量守恒",
                "所有玩家mol汇总并平均分配（余数舍弃）", false);

            Reg(EventCardId.SchrodingerCat, "薛定谔的猫",
                "掷骰(不受修正)。1~3:丢弃一张非基本物理量；4~6:丢弃1mol", true);

            Reg(EventCardId.EinsteinMiracleYear, "爱因斯坦奇迹年",
                "如果你是爱因斯坦，随机获得两张非基本物理量", false);

            Reg(EventCardId.Collapse, "坍缩",
                "掷骰子，若小于5，你失去所有基本物理量牌", true);

            Reg(EventCardId.BlackHole, "黑洞",
                "掷骰子，若小于5，你失去所有非基本物理量牌", true);

            Reg(EventCardId.PhaseTransition, "相变",
                "选择：固态(跳过一回合+8mol)/液态(调转方向)/气态(轻盈+不触发地块直到过起点)", false);

            Reg(EventCardId.ResearchFunding, "基金项目",
                "你获得10mol", false);

            Reg(EventCardId.AcademicPlagiarism, "学术剽窃",
                "掠夺一名其他玩家的任意一张物理量牌", false);

            Reg(EventCardId.EMInduction, "电磁感应",
                "若同时拥有非电流的电学量和磁学量，获得20mol", false);

            Reg(EventCardId.Wormhole, "虫洞",
                "传送到起点（不算经过起点），mol减半", true);

            Reg(EventCardId.FranckHertz, "弗兰克-赫兹实验",
                "连投3次骰子，严格递增或递减+20mol，否则-5mol", false);

            Reg(EventCardId.NuclearReactor, "核反应堆",
                "获得1mol，可持续掷骰：1~4翻倍，5~6失去奖励与所有mol", false);
        }

        // 注册辅助方法
        private static void Reg(EventCardId id, string name, string desc, bool negative)
        {
            _definitions[id] = new EventCardDefinition(id, name, desc, negative);
        }

        /// <summary>根据ID获取事件牌定义</summary>
        public static EventCardDefinition Get(EventCardId id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>获取所有事件牌ID列表（用于构建牌堆）</summary>
        public static List<EventCardId> GetAllIds()
        {
            return new List<EventCardId>(_definitions.Keys);
        }
    }
}
