// ============================================================
// GameEnums.cs — 游戏中所有枚举类型的定义
// ============================================================
namespace PhysicsFriends.Core
{
    /// <summary>游戏全局状态机</summary>
    public enum GameState
    {
        Setup,          // 准备阶段：选择模式、角色、颜色
        Initializing,   // 初始化中
        RollForFirst,   // 掷骰子决定先手
        Playing,        // 游戏进行中（回合循环）
        PlayerTurn,     // 玩家回合进行中
        GameOver        // 游戏结束
    }

    /// <summary>单个玩家回合内的子阶段</summary>
    public enum TurnPhase
    {
        SkipCheck,              // 1. 检查是否需要跳过本回合
        AchievementRollCheck,   // 2. 回合开始时创举掷骰检查
        RollDice,               // 3. 掷骰子
        DiceModification,       // 4. 骰子修正链（其他玩家依次使用"力"等）
        HeavyLightCalc,         // 5. 沉重/轻盈结算
        Movement,               // 6. 逐格移动
        LandingEffect,          // 7. 落点效果
        FreeAction,             // 8. 自由行动阶段（使用主动卡牌）
        TurnEnd                 // 9. 回合结束（检查胜利等）
    }

    /// <summary>游戏速度模式</summary>
    public enum GameMode
    {
        Fast,       // 快速模式：约15分钟
        Standard,   // 标准模式：约30分钟
        Slow        // 慢速模式：约45分钟
    }

    /// <summary>物理学时代</summary>
    public enum Era
    {
        NaturalPhilosophy,  // 第一时代：自然哲学时期（至多15轮）
        ClassicalPhysics,   // 第二时代：经典物理学时期（至多15轮）
        ModernPhysics       // 第三时代：现代物理学时期
    }

    /// <summary>玩家颜色</summary>
    public enum PlayerColor
    {
        Red,    // 红色
        Blue,   // 蓝色
        Green,  // 绿色
        Yellow  // 黄色
    }

    /// <summary>人物角色</summary>
    public enum Character
    {
        Newton,     // 牛顿：任务完成后，所有主动效果可释放两次
        Maxwell,    // 麦克斯韦：任务完成后，可同时选择两项抉择效果
        Einstein,   // 爱因斯坦：任务完成后，能量量纲牌奖励翻倍
        Schrodinger // 薛定谔：任务完成后，所有骰子可重投一次
    }

    /// <summary>行进方向</summary>
    public enum MoveDirection
    {
        Clockwise,      // 顺时针
        CounterClockwise // 逆时针
    }

    /// <summary>棋盘格子类型</summary>
    public enum TileType
    {
        Start,      // 起点格
        Event,      // 事件格
        Reward,     // 奖励格
        Territory,  // 领地格（带颜色归属）
        Shop,       // 商店格（带颜色归属）
        Supply      // 补给格（带颜色归属）—— 仅内圈有，展开后被事件格取代
    }

    /// <summary>卡牌大类</summary>
    public enum CardCategory
    {
        BasicPhysical,      // 基本物理量牌（浅绿色）
        NonBasicPhysical,   // 非基本物理量牌（多种颜色）
        Event,              // 事件牌
        Reward,             // 奖励牌
        Achievement,        // 创举牌
        CharacterCard       // 人物牌
    }

    /// <summary>物理量卡牌的效果类型</summary>
    public enum CardEffectType
    {
        None,       // 无效果（基本物理量中的时间、长度、质量、温度）
        Passive,    // 被动效果：始终生效
        Active,     // 主动效果：使用后消耗
        Choice      // 抉择效果：从两个选项中选一
    }

    /// <summary>物理量的学科分类</summary>
    public enum PhysicsBranch
    {
        Basic,              // 基本物理量
        Mechanics,          // 力学量
        Electromagnetics,   // 电磁学量
        Thermodynamics,     // 热学量
        Optics,             // 光学量（光照强度）
        Special             // 特殊量（物质的量/频率/角速度等）
    }

    /// <summary>建筑类型</summary>
    public enum BuildingType
    {
        Laboratory,         // 实验室：经过交1mol
        ResearchInstitute,  // 研究所：经过交2mol
        LargeCollider,      // 大型对撞机：经过交5mol
        Observatory         // 天文台：经过交一张对方没有的物理量牌
    }

    /// <summary>胜利类型</summary>
    public enum VictoryType
    {
        Wealth,         // 财富胜利：mol达到阈值
        Achievement,    // 创举胜利：创举点数达到阈值
        None            // 未胜利
    }

    /// <summary>相变状态（事件牌#20）</summary>
    public enum PhaseState
    {
        None,   // 无相变
        Solid,  // 固态：跳过下一回合，获得8mol
        Liquid, // 液态：调转前进方向
        Gas     // 气态：始终轻盈，不触发地块/建筑效果，直到下次经过起点
    }

    /// <summary>跳过回合的原因</summary>
    public enum SkipReason
    {
        Stun,           // 动量撞晕（3回合）
        AbsoluteZero,   // 绝对零度（3回合）
        Potential,       // 电势（经过起点后跳过1回合）
        PhaseSolid,     // 相变-固态（跳过下一回合）
        Roadblock       // 路障（跳过下一回合）
    }
}