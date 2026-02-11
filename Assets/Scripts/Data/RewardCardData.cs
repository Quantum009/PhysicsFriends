// ============================================================
// RewardCardData.cs — 奖励牌定义：全部24张奖励牌的数据与效果
// ============================================================
using System;
using System.Collections.Generic;

namespace PhysicsFriends.Data
{
    /// <summary>奖励牌ID枚举</summary>
    public enum RewardCardId
    {
        Accelerator = 1,        // 加速器：增加一次投骰次数
        HeatEngine = 2,         // 热机：掷骰子，每点=1mol
        Electromagnet = 3,      // 电磁铁：消耗电流激活，他人不能越过你
        TreasureSmall = 4,      // 宝箱：+1mol
        TreasureMedium = 5,     // 稀有宝箱：+2mol
        TreasureLarge = 6,      // 传奇宝箱：+5mol
        Paper = 7,              // 论文：随机获得一张基本物理量
        TopPaper = 8,           // 顶级论文：选择并获得一张基本物理量
        LegendaryPaper = 9,     // 传奇论文：随机获得一张非基本物理量
        AncientPaper = 10,      // 古老论文：从弃牌堆随机获得一张
        Stopwatch = 11,         // 秒表：+1时间
        AtomicClock = 12,       // 原子钟：+2时间
        Balance = 13,           // 托盘天平：+1质量
        KilogramPrototype = 14, // 千克原器：+2质量
        Thermometer = 15,       // 温度计：+1温度
        Battery = 16,           // 干电池：+1电流
        Generator = 17,         // 发电机：消耗一张力学量，+2电流
        LightBulb = 18,         // 电灯泡：+1光照强度
        Ruler = 19,             // 刻度尺：+1长度
        Microscope = 20,        // 工具显微镜：+2长度
        Laboratory = 21,        // 实验室：建造，经过者交1mol
        ResearchInstitute = 22, // 研究所：建造，经过者交2mol
        LargeCollider = 23,     // 大型对撞机：建造，经过者交5mol
        Square = 24             // 平方：选择并复制一张手牌中的物理量牌
    }

    /// <summary>奖励牌的静态定义</summary>
    [Serializable]
    public class RewardCardDefinition
    {
        public RewardCardId id;         // 奖励牌唯一ID
        public string nameZH;           // 中文名称
        public string descriptionZH;    // 效果描述
        public bool isBuilding;         // 是否为建筑类奖励（需要放置在格子上）
        public bool returnToDeck;       // 使用后是否洗回牌堆

        public RewardCardDefinition(RewardCardId id, string name, string desc,
            bool building = false, bool returnDeck = false)
        {
            this.id = id;
            this.nameZH = name;
            this.descriptionZH = desc;
            this.isBuilding = building;
            this.returnToDeck = returnDeck;
        }
    }

    /// <summary>奖励牌数据库</summary>
    public static class RewardCardDatabase
    {
        private static Dictionary<RewardCardId, RewardCardDefinition> _definitions;

        /// <summary>初始化所有奖励牌定义</summary>
        public static void Initialize()
        {
            _definitions = new Dictionary<RewardCardId, RewardCardDefinition>();

            // 逐一注册25张奖励牌
            Reg(RewardCardId.Accelerator, "加速器",
                "可以增加一次投骰次数");

            Reg(RewardCardId.HeatEngine, "热机",
                "掷骰子，每投出一点获得1mol");

            Reg(RewardCardId.Electromagnet, "电磁铁",
                "消耗一张电流激活。其他玩家不能越过你，强制停留前/后一格。持续3回合");

            Reg(RewardCardId.TreasureSmall, "宝箱",
                "你获得1mol");

            Reg(RewardCardId.TreasureMedium, "稀有宝箱",
                "你获得2mol");

            Reg(RewardCardId.TreasureLarge, "传奇宝箱",
                "你获得5mol");

            Reg(RewardCardId.Paper, "论文",
                "你随机获得一张基本物理量");

            Reg(RewardCardId.TopPaper, "顶级论文",
                "你选择并获得一张基本物理量");

            Reg(RewardCardId.LegendaryPaper, "传奇论文",
                "你随机获得一张非基本物理量");

            Reg(RewardCardId.AncientPaper, "古老论文",
                "你随机从弃牌堆中获得一张物理量");

            Reg(RewardCardId.Stopwatch, "秒表", "获得一张时间");
            Reg(RewardCardId.AtomicClock, "原子钟", "获得两张时间");
            Reg(RewardCardId.Balance, "托盘天平", "获得一张质量");
            Reg(RewardCardId.KilogramPrototype, "千克原器", "获得两张质量");
            Reg(RewardCardId.Thermometer, "温度计", "获得一张温度");
            Reg(RewardCardId.Battery, "干电池", "获得一张电流");

            Reg(RewardCardId.Generator, "发电机",
                "消耗一张任意力学量牌来激活，获得两张电流");

            Reg(RewardCardId.LightBulb, "电灯泡", "获得一张光照强度");
            Reg(RewardCardId.Ruler, "刻度尺", "获得一张长度");
            Reg(RewardCardId.Microscope, "工具显微镜", "获得两张长度");

            Reg(RewardCardId.Laboratory, "实验室",
                "在同色地块搭建实验室，经过者交1mol", true);

            Reg(RewardCardId.ResearchInstitute, "研究所",
                "在同色地块搭建研究所，经过者交2mol", true);

            Reg(RewardCardId.LargeCollider, "大型对撞机",
                "在同色地块搭建大型对撞机，经过者交5mol", true);

            Reg(RewardCardId.Square, "平方",
                "选择并复制一张手牌中的物理量牌");
        }

        // 注册辅助方法
        private static void Reg(RewardCardId id, string name, string desc,
            bool building = false, bool returnDeck = false)
        {
            _definitions[id] = new RewardCardDefinition(id, name, desc, building, returnDeck);
        }

        /// <summary>根据ID获取奖励牌定义</summary>
        public static RewardCardDefinition Get(RewardCardId id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>获取所有奖励牌ID</summary>
        public static List<RewardCardId> GetAllIds()
        {
            return new List<RewardCardId>(_definitions.Keys);
        }
    }
}
