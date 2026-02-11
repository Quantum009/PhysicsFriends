// ============================================================
// GameConfig.cs — 游戏配置：根据模式（快速/标准/慢速）加载数值
// ============================================================
using System;
using PhysicsFriends.Core;

namespace PhysicsFriends.Data
{
    /// <summary>
    /// 游戏数值配置，根据不同模式有不同的参数
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public GameMode mode;           // 当前模式
        public int wealthVictoryMol;    // 财富胜利所需mol数
        public int achievementVictoryPts; // 创举胜利所需点数
        public int initialMol;          // 初始mol数
        public int achievementsPerEra;  // 每个时代抽取的创举数量
        public int maxRoundsPerEra;     // 每个时代的最大轮次
        public int innerBoardSize;      // 内圈格子数（自然哲学时期）
        public int outerBoardSize;      // 外圈格子数（经典物理学及之后）

        /// <summary>根据模式创建对应配置</summary>
        public static GameConfig Create(GameMode mode)
        {
            var config = new GameConfig();
            config.mode = mode;                 // 记录模式
            config.maxRoundsPerEra = 15;        // 所有模式都是15轮上限
            config.innerBoardSize = 24;         // 内圈固定24格
            config.outerBoardSize = 72;         // 外圈固定72格

            switch (mode)
            {
                case GameMode.Fast:             // 快速模式
                    config.wealthVictoryMol = 30;    // 30mol胜利
                    config.achievementVictoryPts = 4; // 4创举点胜利
                    config.initialMol = 5;           // 初始5mol
                    config.achievementsPerEra = 2;    // 每时代抽2张创举
                    break;

                case GameMode.Standard:         // 标准模式
                    config.wealthVictoryMol = 60;    // 60mol胜利
                    config.achievementVictoryPts = 6; // 6创举点胜利
                    config.initialMol = 10;          // 初始10mol
                    config.achievementsPerEra = 2;    // 每时代抽2张创举
                    break;

                case GameMode.Slow:             // 慢速模式
                    config.wealthVictoryMol = 120;   // 120mol胜利
                    config.achievementVictoryPts = 8; // 8创举点胜利
                    config.initialMol = 15;          // 初始15mol
                    config.achievementsPerEra = 3;    // 慢速模式每时代抽3张
                    break;
            }
            return config;
        }

        /// <summary>
        /// 根据模式调整数值（规则书说标准=基准，快速=减半，慢速=翻倍）
        /// 例如：标准模式下15mol的门槛，快速=7mol，慢速=30mol
        /// </summary>
        public int AdjustValue(int standardValue)
        {
            switch (mode)
            {
                case GameMode.Fast:
                    return standardValue / 2;   // 快速模式减半
                case GameMode.Slow:
                    return standardValue * 2;   // 慢速模式翻倍
                default:
                    return standardValue;       // 标准模式不变
            }
        }

        /// <summary>获取购买随机基本物理量的价格</summary>
        public int GetRandomCardPrice(Era era)
        {
            switch (era)
            {
                case Era.NaturalPhilosophy: return 2;   // 自然哲学时期：2mol
                case Era.ClassicalPhysics: return 3;     // 经典物理学时期：3mol
                case Era.ModernPhysics: return 5;        // 现代物理学时期：5mol
                default: return 3;
            }
        }

        /// <summary>获取购买任选基本物理量的价格</summary>
        public int GetChosenCardPrice(Era era)
        {
            switch (era)
            {
                case Era.NaturalPhilosophy: return 5;   // 自然哲学时期：5mol
                case Era.ClassicalPhysics: return 6;     // 经典物理学时期：6mol
                case Era.ModernPhysics: return 8;        // 现代物理学时期：8mol
                default: return 6;
            }
        }

        /// <summary>Create的别名，兼容GameManager中的调用</summary>
        public static GameConfig GetConfig(GameMode mode)
        {
            return Create(mode);
        }
    }
}